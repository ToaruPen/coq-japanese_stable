"""Refresh candidate-inventory statuses against current translation assets.

This reconciler keeps the legacy candidate inventory usable as a bridge/view-only
surface for current static consumers, not the source of truth. The first-PR
static consumer boundary stays explicit: Roslyn pilot surfaces are pilot-aware,
scanner inventory consumers stay bridge-only, and runtime/triage work is
deferred.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass, replace
from pathlib import Path

if __package__ in {None, ""}:
    _PROJECT_ROOT = Path(__file__).resolve().parents[2]
    _PROJECT_ROOT_STR = str(_PROJECT_ROOT)
    if _PROJECT_ROOT_STR not in sys.path:
        sys.path.insert(0, _PROJECT_ROOT_STR)

from scripts.legacies.scanner.cross_reference import cross_reference_inventory
from scripts.legacies.scanner.inventory import (
    InventoryDraft,
    InventorySite,
    SiteStatus,
    SiteType,
    describe_first_pr_static_consumer_boundary,
    write_candidate_inventory_json,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_INVENTORY_PATH = Path("docs/candidate-inventory.json")
DEFAULT_OUTPUT_PATH = DEFAULT_INVENTORY_PATH
DEFAULT_SOURCE_ROOT = None
_PLACEHOLDER_RE = re.compile(r"\{(?P<index>\d+)\}")
_TIER_TWO = 2


@dataclass(frozen=True, slots=True)
class VerbPattern:
    """One compiled verb asset pattern."""

    verb: str
    regex: re.Pattern[str]


@dataclass(frozen=True, slots=True)
class VerbAssetIndex:
    """Compiled index of message-frame verb assets."""

    tier1_verbs: frozenset[str]
    tier2_patterns: tuple[VerbPattern, ...]
    tier3_patterns: tuple[VerbPattern, ...]
    known_verbs: frozenset[str]


@dataclass(frozen=True, slots=True)
class ReconciliationSummary:
    """Summary of status changes made during reconciliation."""

    total_sites: int
    translated_before: int
    translated_after: int
    baseline_promotions: int
    message_frame_promotions: int
    does_verb_promotions: int
    normalized_excluded: int

    @property
    def translated_delta(self) -> int:
        """Return the net change in translated sites."""
        return self.translated_after - self.translated_before


def read_candidate_inventory_with_legacy_statuses(path: Path) -> tuple[InventoryDraft, int]:
    """Read candidate-inventory JSON while normalizing legacy status aliases."""
    payload = json.loads(path.read_text(encoding="utf-8"))
    normalized_excluded = 0
    for site in payload.get("sites", []):
        if site.get("status") == "exclude":
            site["status"] = SiteStatus.EXCLUDED.value
            normalized_excluded += 1
    return InventoryDraft.from_dict(payload), normalized_excluded


def build_verb_asset_index(path: Path) -> VerbAssetIndex:
    """Build a compiled lookup index from MessageFrames/verbs.ja.json."""
    payload = json.loads(path.read_text(encoding="utf-8"))
    tier1_verbs = frozenset(entry["verb"] for entry in payload.get("tier1", []) if isinstance(entry.get("verb"), str))
    tier2_patterns = _compile_verb_patterns(payload.get("tier2", []))
    tier3_patterns = _compile_verb_patterns(payload.get("tier3", []))
    known_verbs = frozenset(pattern.verb for pattern in (*tier2_patterns, *tier3_patterns)) | tier1_verbs
    return VerbAssetIndex(
        tier1_verbs=tier1_verbs,
        tier2_patterns=tier2_patterns,
        tier3_patterns=tier3_patterns,
        known_verbs=known_verbs,
    )


def reconcile_inventory(
    draft: InventoryDraft,
    repo_root: Path,
    *,
    source_root: Path | None = None,
) -> tuple[InventoryDraft, ReconciliationSummary]:
    """Promote inventory sites to translated when current assets prove coverage."""
    baseline = cross_reference_inventory(draft, repo_root, source_root=source_root)
    verb_index = build_verb_asset_index(
        repo_root / "Mods" / "QudJP" / "Localization" / "MessageFrames" / "verbs.ja.json"
    )
    reconciled_sites: list[InventorySite] = []

    translated_before = sum(site.status is SiteStatus.TRANSLATED for site in draft.sites)
    baseline_promotions = 0
    message_frame_promotions = 0
    does_verb_promotions = 0

    for current_site, baseline_site in zip(draft.sites, baseline.sites, strict=True):
        reason = _promotion_reason(current_site, baseline_site, verb_index)
        if reason is None:
            reconciled_sites.append(current_site)
            continue

        if current_site.status is not SiteStatus.TRANSLATED:
            if reason == "baseline":
                baseline_promotions += 1
            elif reason == "message-frame":
                message_frame_promotions += 1
            else:
                does_verb_promotions += 1

        # Verb-based evidence is recorded even for baseline/already-translated sites
        # that happen to match verb assets (hybrid coverage).
        # Skip redundant verb asset checks if reason already determined translation via verb evidence.
        if reason in {"message-frame", "does-verb"}:
            translated_by_verbs = True
        else:
            translated_by_verbs = _matches_message_frame_assets(current_site, verb_index) or _matches_does_verb_assets(
                current_site, verb_index
            )

        reconciled_sites.append(
            replace(
                current_site,
                status=SiteStatus.TRANSLATED,
                existing_dictionary=_merge_csv_values(
                    current_site.existing_dictionary,
                    baseline_site.existing_dictionary,
                    "MessageFrames/verbs.ja.json" if translated_by_verbs else None,
                ),
                existing_patch=_merge_csv_values(
                    current_site.existing_patch,
                    baseline_site.existing_patch,
                ),
                existing_xml=_merge_csv_values(
                    current_site.existing_xml,
                    baseline_site.existing_xml,
                ),
            )
        )

    translated_after = sum(site.status is SiteStatus.TRANSLATED for site in reconciled_sites)
    summary = ReconciliationSummary(
        total_sites=len(reconciled_sites),
        translated_before=translated_before,
        translated_after=translated_after,
        baseline_promotions=baseline_promotions,
        message_frame_promotions=message_frame_promotions,
        does_verb_promotions=does_verb_promotions,
        normalized_excluded=0,
    )
    return replace(draft, sites=tuple(reconciled_sites)), summary


def reconcile_inventory_file(
    inventory_path: Path,
    *,
    repo_root: Path,
    output_path: Path,
    source_root: Path | None = None,
) -> tuple[InventoryDraft, ReconciliationSummary]:
    """Read an inventory, reconcile statuses, and persist the updated JSON."""
    draft, normalized_excluded = read_candidate_inventory_with_legacy_statuses(inventory_path)
    reconciled, summary = reconcile_inventory(draft, repo_root, source_root=source_root)
    final_summary = replace(summary, normalized_excluded=normalized_excluded)
    write_candidate_inventory_json(output_path, reconciled)
    return reconciled, final_summary


def _promotion_reason(
    current_site: InventorySite,
    baseline_site: InventorySite,
    verb_index: VerbAssetIndex,
) -> str | None:
    """Return the reason this site should be considered translated."""
    if current_site.status is SiteStatus.TRANSLATED:
        return "already-translated"
    if baseline_site.status is SiteStatus.TRANSLATED:
        return "baseline"
    if _matches_message_frame_assets(current_site, verb_index):
        return "message-frame"
    if _matches_does_verb_assets(current_site, verb_index):
        return "does-verb"
    return None


def _matches_message_frame_assets(site: InventorySite, verb_index: VerbAssetIndex) -> bool:
    """Return whether the site is covered by MessageFrames/verbs.ja.json."""
    if site.type is not SiteType.MESSAGE_FRAME or site.verb is None:
        return False
    if site.lookup_tier == 1:
        return site.verb in verb_index.tier1_verbs
    normalized_extra = site.extra or ""
    patterns = verb_index.tier2_patterns
    if site.lookup_tier != _TIER_TWO:
        normalized_extra = normalize_csharp_template(site.extra)
        patterns = verb_index.tier3_patterns
    return any(pattern.verb == site.verb and pattern.regex.fullmatch(normalized_extra) for pattern in patterns)


def _matches_does_verb_assets(site: InventorySite, verb_index: VerbAssetIndex) -> bool:
    """Return whether a Does()-based site uses a verb known to the route translator."""
    return site.type is SiteType.VERB_COMPOSITION and site.verb in verb_index.known_verbs


def _compile_verb_patterns(entries: object) -> tuple[VerbPattern, ...]:
    """Compile tier2/tier3 verb asset entries into reusable regex patterns."""
    if not isinstance(entries, list):
        return ()
    patterns: list[VerbPattern] = []
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        verb = entry.get("verb")
        extra = entry.get("extra")
        if not isinstance(verb, str) or not isinstance(extra, str):
            continue
        patterns.append(VerbPattern(verb=verb, regex=_compile_placeholder_regex(extra)))
    return tuple(patterns)


def _compile_placeholder_regex(template: str) -> re.Pattern[str]:
    """Compile a `{0}`-style placeholder template into a regex with backreferences for repeated indices."""
    builder: list[str] = ["^"]
    last_index = 0
    # Map each placeholder index to its capture group number (1-indexed)
    placeholder_to_group: dict[str, int] = {}
    next_group = 1

    for match in _PLACEHOLDER_RE.finditer(template):
        builder.append(re.escape(template[last_index : match.start()]))
        placeholder_idx = match.group("index")

        if placeholder_idx not in placeholder_to_group:
            # First occurrence: create a new capture group
            placeholder_to_group[placeholder_idx] = next_group
            builder.append("(.+?)")
            next_group += 1
        else:
            # Repeated occurrence: use a backreference to the same group
            builder.append(f"\\{placeholder_to_group[placeholder_idx]}")

        last_index = match.end()

    builder.append(re.escape(template[last_index:]))
    builder.append("$")
    return re.compile("".join(builder))


def normalize_csharp_template(expression: str | None) -> str:
    """Convert a simple C# concatenation expression into a placeholder template."""
    if expression is None:
        return ""
    stripped = expression.strip()
    if not stripped:
        return ""

    normalized: list[str] = []
    # Map each distinct part (non-string-literal, non-verb) to a placeholder index
    # to maintain positional equality for repeated references
    part_to_placeholder: dict[str, int] = {}
    next_placeholder_index = 0

    for part in _split_top_level_concat(stripped):
        if _is_string_literal(part):
            normalized.append(_unquote_string(part))
            continue

        base_verb = _extract_get_verb_argument(part)
        if base_verb is not None:
            normalized.append(base_verb)
            continue

        if part not in part_to_placeholder:
            part_to_placeholder[part] = next_placeholder_index
            next_placeholder_index += 1
        normalized.append(f"{{{part_to_placeholder[part]}}}")

    return "".join(normalized)


def _split_top_level_concat(expression: str) -> list[str]:  # noqa: C901
    """Split a C# expression by top-level `+` operators."""
    parts: list[str] = []
    start = 0
    depth_paren = depth_bracket = depth_brace = 0
    index = 0

    while index < len(expression):
        string_info = _string_prefix(expression, index)
        if string_info is not None:
            prefix_length, verbatim = string_info
            index = _consume_string(expression, index + prefix_length, verbatim=verbatim)
            continue

        character = expression[index]
        if character == "(":
            depth_paren += 1
        elif character == ")":
            depth_paren -= 1
        elif character == "[":
            depth_bracket += 1
        elif character == "]":
            depth_bracket -= 1
        elif character == "{":
            depth_brace += 1
        elif character == "}":
            depth_brace -= 1
        elif character == "+" and depth_paren == depth_bracket == depth_brace == 0:
            parts.append(expression[start:index].strip())
            start = index + 1
        index += 1

    tail = expression[start:].strip()
    if tail:
        parts.append(tail)
    return parts


def _string_prefix(text: str, index: int) -> tuple[int, bool] | None:
    """Return the string prefix length and whether the literal is verbatim."""
    for prefix, verbatim in (('$@"', True), ('@$"', True), ('@"', True), ('$"', False), ('"', False)):
        if text.startswith(prefix, index):
            return (len(prefix), verbatim)
    return None


def _consume_string(text: str, index: int, *, verbatim: bool) -> int:
    """Consume a single C# string literal."""
    while index < len(text):
        if verbatim and text[index] == '"' and index + 1 < len(text) and text[index + 1] == '"':
            index += 2
            continue
        if text[index] == '"' and (verbatim or text[index - 1] != "\\"):
            return index + 1
        index += 1
    return index


def _is_string_literal(expression: str) -> bool:
    """Return whether the expression is a standalone C# string literal."""
    stripped = expression.strip()
    return (
        (stripped.startswith('"') and stripped.endswith('"'))
        or (stripped.startswith('@"') and stripped.endswith('"'))
        or (stripped.startswith('$"') and stripped.endswith('"'))
        or (stripped.startswith('$@"') and stripped.endswith('"'))
        or (stripped.startswith('@$"') and stripped.endswith('"'))
    )


def _unquote_string(expression: str) -> str:
    """Remove quotes from a C# string literal."""
    stripped = expression.strip()
    for prefix in ('$@"', '@$"', '@"', '$"', '"'):
        if stripped.startswith(prefix) and stripped.endswith('"'):
            inner = stripped[len(prefix) : -1]
            if prefix in {'$@"', '@$"', '@"'}:
                return inner.replace('""', '"')
            return bytes(inner, "utf-8").decode("unicode_escape")
    return stripped


def _extract_get_verb_argument(expression: str) -> str | None:
    r"""Extract the base verb from a `GetVerb("...")` call."""
    match = re.search(r'GetVerb\("(?P<verb>[^"]+)"', expression)
    if match is None:
        return None
    prefix = " " if "PrependSpace: true" in expression or "PrependSpace:true" in expression else ""
    return prefix + match.group("verb")


def _merge_csv_values(*values: str | None) -> str | None:
    """Merge comma-separated evidence fields without duplicates."""
    merged: list[str] = []
    seen: set[str] = set()
    for value in values:
        if value is None or value == "":
            continue
        for item in value.split(", "):
            if item and item not in seen:
                seen.add(item)
                merged.append(item)
    return ", ".join(merged) if merged else None


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Parse CLI arguments for the inventory reconciler."""
    parser = argparse.ArgumentParser(
        formatter_class=argparse.RawDescriptionHelpFormatter,
        description=(
            "Reconcile candidate inventory statuses against current assets. "
            "The inventory remains a legacy bridge/view-only surface for current static consumers "
            "and not the source of truth."
        ),
        epilog=describe_first_pr_static_consumer_boundary(),
    )
    parser.add_argument(
        "--inventory",
        default=str(DEFAULT_INVENTORY_PATH),
        help=(
            "Path to the bridge/view-only candidate-inventory.json used by current static consumers; "
            "not the source of truth."
        ),
    )
    parser.add_argument(
        "--output",
        default=str(DEFAULT_OUTPUT_PATH),
        help="Path to write the reconciled bridge/view-only inventory JSON; not the source of truth.",
    )
    parser.add_argument(
        "--repo-root",
        default=str(REPO_ROOT),
        help="Repository root containing Mods/QudJP/.",
    )
    parser.add_argument(
        "--source-root",
        default=None,
        help="Path to the decompiled C# source root used for patch evidence.",
    )
    return parser.parse_args(argv)


def _print_summary(summary: ReconciliationSummary) -> None:
    """Write a concise reconciliation summary to stdout."""
    counts = Counter(
        {
            "baseline": summary.baseline_promotions,
            "message-frame": summary.message_frame_promotions,
            "does-verb": summary.does_verb_promotions,
        }
    )
    source_counts = ", ".join(f"{name}={counts[name]}" for name in ("baseline", "message-frame", "does-verb"))
    sys.stdout.write(
        "Inventory reconciliation complete: "
        f"{summary.translated_before} -> {summary.translated_after} translated "
        f"(+{summary.translated_delta}); "
        f"legacy exclude -> excluded: {summary.normalized_excluded}; "
        f"sources: {source_counts}\n"
    )


def main(argv: list[str] | None = None) -> int:
    """Run inventory status reconciliation from the command line."""
    args = _parse_args(argv)
    inventory_path = Path(args.inventory).expanduser().resolve()
    output_path = Path(args.output).expanduser().resolve()
    repo_root = Path(args.repo_root).expanduser().resolve()
    source_root = Path(args.source_root).expanduser().resolve() if args.source_root else None

    reconciled, summary = reconcile_inventory_file(
        inventory_path,
        repo_root=repo_root,
        output_path=output_path,
        source_root=source_root,
    )
    _print_summary(summary)
    if len(reconciled.sites) != summary.total_sites:
        msg = "Reconciled inventory site count does not match summary."
        raise RuntimeError(msg)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
