"""Phase 1b scanner: rule-based classification for raw Phase 1a hits."""

from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from datetime import UTC, datetime
from functools import lru_cache
from pathlib import Path

if __package__ in {None, ""}:
    _PROJECT_ROOT = Path(__file__).resolve().parents[3]
    _PROJECT_ROOT_STR = str(_PROJECT_ROOT)
    if _PROJECT_ROOT_STR not in sys.path:
        sys.path.insert(0, _PROJECT_ROOT_STR)

from scripts.legacies.scanner.inventory import (
    Confidence,
    FixedLeafRejectionReason,
    InventoryDraft,
    InventorySite,
    InventoryStats,
    OwnershipClass,
    RawHit,
    SiteType,
    default_destination_dictionary_for_route,
    read_raw_hits_jsonl,
    write_inventory_draft_json,
)

DEFAULT_GAME_VERSION = "2.0.4"
DEFAULT_RAW_HITS_PATH = Path(".scanner-cache/raw_hits.jsonl")
DEFAULT_OUTPUT_PATH = Path(".scanner-cache/inventory_draft.json")
_INTERPOLATED_STRING_RE = re.compile(r'(?:\$@|@\$|\$)"')
_STRING_LITERAL_RE = re.compile(r'^\s*(?:"(?:[^"\\]|\\.)*"|@"(?:[^"]|"")*")\s*$')
_IDENTIFIER_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
_METHOD_NAME_RE = re.compile(
    r"\b(?P<method>DidXToYWithZ|DidXToY|DidX|XDidYToZ|XDidY|WDidXToYWithZ)\s*\(",
)
_FALSE_POSITIVE_DOES_PATTERNS = (
    re.compile(r'\bwho\.Does\("were"'),
    re.compile(r"\bContext\.Target\.Does\("),
    re.compile(r"\bMainText\s*="),
    re.compile(r"\bFailureMessage\s*="),
    re.compile(r'\bParentObject\.Does\("seem"'),
    re.compile(r'\breturn\s+Actor\.Does\("have"'),
    re.compile(r'\+\s*" you of"'),
)
_STRING_BUILDER_TYPES = ("StringBuilder", "Utf16ValueStringBuilder", "ValueStringBuilder")
_MESSAGE_FRAME_SPECS = {
    "DidX": (0, None, 1, None),
    "DidXToY": (0, 1, 2, 3),
    "DidXToYWithZ": (0, 1, 4, 5),
    "XDidY": (1, None, 2, None),
    "XDidYToZ": (1, 2, 3, 4),
    "WDidXToYWithZ": (1, 2, 5, 6),
}


def classify_raw_hits_file(
    raw_hits_path: Path,
    source_root: Path,
    *,
    output_path: Path | None = None,
) -> InventoryDraft:
    """Read raw hits, classify them, and optionally persist the draft JSON."""
    draft = classify_raw_hits(read_raw_hits_jsonl(raw_hits_path), source_root)
    if output_path is not None:
        write_inventory_draft_json(output_path, draft)
    return draft


def classify_raw_hits(raw_hits: list[RawHit], source_root: Path) -> InventoryDraft:
    """Classify a list of raw hits into an inventory draft."""
    sites: list[InventorySite] = []
    filtered_hits = 0
    for hit in raw_hits:
        site = classify_raw_hit(hit, source_root)
        if site is None:
            filtered_hits += 1
            continue
        sites.append(site)

    confidence_counts = Counter(site.confidence for site in sites)
    stats = InventoryStats(
        input_hits=len(raw_hits),
        filtered_hits=filtered_hits,
        output_sites=len(sites),
        high_confidence=confidence_counts[Confidence.HIGH],
        medium_confidence=confidence_counts[Confidence.MEDIUM],
        low_confidence=confidence_counts[Confidence.LOW],
        needs_review=sum(site.needs_review for site in sites),
        needs_runtime=sum(site.needs_runtime for site in sites),
        proven_fixed_leaf=sum(site.is_proven_fixed_leaf for site in sites),
        rejected_fixed_leaf=sum(site.rejection_reason is not None for site in sites),
    )
    return InventoryDraft(
        version="1.0",
        game_version=DEFAULT_GAME_VERSION,
        scan_date=datetime.now(UTC).date().isoformat(),
        stats=stats,
        sites=tuple(sites),
    )


def classify_raw_hit(raw_hit: RawHit, source_root: Path) -> InventorySite | None:
    """Classify a single raw hit, or return None if it should be filtered."""
    if raw_hit.family == "DidX":
        site = _classify_message_frame(raw_hit)
    elif raw_hit.family == "Does":
        site = _classify_does(raw_hit, source_root)
    elif raw_hit.family == "ReplaceBuilder":
        site = _build_site(raw_hit, SiteType.VARIABLE_TEMPLATE, Confidence.HIGH)
    elif raw_hit.family == "HistoricStringExpander":
        site = _build_site(
            raw_hit,
            SiteType.PROCEDURAL_TEXT,
            Confidence.LOW,
            {"needs_runtime": True},
        )
    elif raw_hit.family == "JournalAPI":
        site = _build_site(
            raw_hit,
            SiteType.NARRATIVE_TEMPLATE,
            Confidence.MEDIUM,
            {"needs_review": True},
        )
    elif raw_hit.family == "GetDisplayName":
        site = _build_site(raw_hit, SiteType.BUILDER, Confidence.HIGH)
    else:
        site = _classify_generic_sink(raw_hit, source_root)
    return site


def _classify_generic_sink(raw_hit: RawHit, source_root: Path) -> InventorySite:
    """Classify SetText/Popup/AddPlayerMessage/EmitMessage-style sink calls."""
    first_argument = _first_argument(raw_hit.matched_code)
    if first_argument is None:
        return _unresolved_site(raw_hit)
    if _is_string_literal(first_argument):
        return _build_site(
            raw_hit,
            SiteType.LEAF,
            Confidence.HIGH,
            {"key": _unquote_string(first_argument)},
        )
    if _contains_template_syntax(first_argument):
        return _build_site(raw_hit, SiteType.TEMPLATE, Confidence.HIGH)
    if ".GetDisplayName(" in first_argument:
        return _build_site(raw_hit, SiteType.BUILDER, Confidence.HIGH)
    if _is_string_builder_to_string(first_argument, raw_hit, source_root):
        return _build_site(
            raw_hit,
            SiteType.TEMPLATE,
            Confidence.MEDIUM,
            {"needs_review": True},
        )
    return _unresolved_site(raw_hit)


def _classify_message_frame(raw_hit: RawHit) -> InventorySite:
    """Classify DidX-family hits as MessageFrame sites."""
    frame = _message_frame_name(raw_hit.matched_code)
    arguments = _top_level_arguments(raw_hit.matched_code)
    verb_expression = _message_frame_argument(arguments, frame, "verb")
    extra_expression = _message_frame_argument(arguments, frame, "extra")
    verb = _clean_message_token(verb_expression)
    extra = _clean_message_token(extra_expression)
    return _build_site(
        raw_hit,
        SiteType.MESSAGE_FRAME,
        Confidence.HIGH,
        {
            "verb": verb,
            "extra": extra,
            "frame": frame,
            "lookup_tier": _lookup_tier(extra_expression),
        },
    )


def _classify_does(raw_hit: RawHit, source_root: Path) -> InventorySite | None:
    """Classify true-positive Does() sites and drop known false positives."""
    source_context = _statement_context(raw_hit, source_root)
    if any(pattern.search(source_context) for pattern in _FALSE_POSITIVE_DOES_PATTERNS):
        return None
    first_argument = _first_argument(raw_hit.matched_code)
    if first_argument is None or not _is_string_literal(first_argument):
        return None
    return _build_site(
        raw_hit,
        SiteType.VERB_COMPOSITION,
        Confidence.HIGH,
        {
            "verb": _unquote_string(first_argument),
            "source_context": source_context,
        },
    )


def _build_site(
    raw_hit: RawHit,
    site_type: SiteType,
    confidence: Confidence,
    fields: dict[str, object] | None = None,
) -> InventorySite:
    """Construct one classified inventory site."""
    site_fields = {
        **_provenance_fields(raw_hit, site_type, confidence, fields or {}),
        **(fields or {}),
    }
    return InventorySite(
        id=f"{raw_hit.file}::L{raw_hit.line}:C{raw_hit.column}",
        file=raw_hit.file,
        line=raw_hit.line,
        column=raw_hit.column,
        sink=raw_hit.family,
        type=site_type,
        confidence=confidence,
        pattern=raw_hit.matched_code,
        **site_fields,
    )


def _provenance_fields(
    raw_hit: RawHit,
    site_type: SiteType,
    confidence: Confidence,
    fields: dict[str, object],
) -> dict[str, object | None]:
    """Build default fixed-leaf provenance for one classified site."""
    needs_review = bool(fields.get("needs_review", False))
    needs_runtime = bool(fields.get("needs_runtime", False))
    source_route = raw_hit.family

    if site_type is SiteType.LEAF and confidence is Confidence.HIGH and not needs_review and not needs_runtime:
        return {
            "source_route": source_route,
            "ownership_class": OwnershipClass.MID_PIPELINE_OWNED,
            "destination_dictionary": default_destination_dictionary_for_route(
                source_route=source_route,
                sink=raw_hit.family,
            ),
            "rejection_reason": None,
        }

    return {
        "source_route": source_route,
        "ownership_class": _ownership_class(site_type),
        "destination_dictionary": None,
        "rejection_reason": _rejection_reason(site_type, needs_runtime=needs_runtime),
    }


def _ownership_class(site_type: SiteType) -> OwnershipClass:
    """Map a site type to its default route ownership class."""
    if site_type is SiteType.UNRESOLVED:
        return OwnershipClass.SINK
    if site_type is SiteType.LEAF:
        return OwnershipClass.MID_PIPELINE_OWNED
    return OwnershipClass.PRODUCER_OWNED


def _rejection_reason(site_type: SiteType, *, needs_runtime: bool) -> FixedLeafRejectionReason:
    """Map a site type to the reason it is excluded from proven fixed-leaf defaults."""
    reasons = {
        SiteType.TEMPLATE: FixedLeafRejectionReason.TEMPLATE,
        SiteType.BUILDER: FixedLeafRejectionReason.BUILDER_DISPLAY_NAME,
        SiteType.MESSAGE_FRAME: FixedLeafRejectionReason.MESSAGE_FRAME,
        SiteType.VERB_COMPOSITION: FixedLeafRejectionReason.VERB_COMPOSITION,
        SiteType.VARIABLE_TEMPLATE: FixedLeafRejectionReason.VARIABLE_TEMPLATE,
        SiteType.PROCEDURAL_TEXT: FixedLeafRejectionReason.PROCEDURAL,
        SiteType.NARRATIVE_TEMPLATE: FixedLeafRejectionReason.NARRATIVE_TEMPLATE,
        SiteType.UNRESOLVED: FixedLeafRejectionReason.UNRESOLVED,
    }
    if site_type in reasons:
        return reasons[site_type]
    if needs_runtime:
        return FixedLeafRejectionReason.NEEDS_RUNTIME
    if site_type is SiteType.LEAF:
        return FixedLeafRejectionReason.NEEDS_REVIEW
    msg = f"No fixed-leaf rejection reason defined for {site_type.value}."
    raise ValueError(msg)


def _unresolved_site(raw_hit: RawHit) -> InventorySite:
    """Build the standard unresolved fallback site."""
    return _build_site(
        raw_hit,
        SiteType.UNRESOLVED,
        Confidence.LOW,
        {"needs_runtime": True},
    )


def _first_argument(code: str) -> str | None:
    """Extract the first top-level call argument from a matched call site."""
    arguments = _top_level_arguments(code)
    if not arguments:
        return None
    return arguments[0]


def _top_level_arguments(code: str) -> list[str]:
    """Extract top-level call arguments for the outermost invocation in code."""
    open_paren = code.find("(")
    if open_paren < 0:
        return []
    close_paren = _matching_paren_index(code, open_paren)
    if close_paren < 0:
        return []
    return _split_top_level(code[open_paren + 1 : close_paren])


def _split_top_level(text: str) -> list[str]:  # noqa: C901
    """Split a comma-delimited argument list without descending into nested syntax."""
    arguments: list[str] = []
    start = 0
    depth_paren = depth_bracket = depth_brace = 0
    index = 0
    while index < len(text):
        string_info = _string_prefix(text, index)
        if string_info is not None:
            prefix_length, verbatim = string_info
            index = _consume_string(text, index + prefix_length, verbatim=verbatim)
            continue
        character = text[index]
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
        elif character == "," and depth_paren == depth_bracket == depth_brace == 0:
            arguments.append(text[start:index].strip())
            start = index + 1
        index += 1
    tail = text[start:].strip()
    if tail:
        arguments.append(tail)
    return arguments


def _matching_paren_index(code: str, open_paren: int) -> int:
    """Find the matching close parenthesis for the outermost call."""
    depth = 0
    index = open_paren
    while index < len(code):
        string_info = _string_prefix(code, index)
        if string_info is not None:
            prefix_length, verbatim = string_info
            index = _consume_string(code, index + prefix_length, verbatim=verbatim)
            continue
        character = code[index]
        if character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
            if depth == 0:
                return index
        index += 1
    return -1


def _is_string_literal(expression: str) -> bool:
    """Return whether an expression is a standalone C# string literal."""
    return bool(_STRING_LITERAL_RE.fullmatch(expression))


def _unquote_string(expression: str) -> str:
    """Remove quotes from a C# string literal."""
    stripped = expression.strip()
    if stripped.startswith('@"') and stripped.endswith('"'):
        return stripped[2:-1].replace('""', '"')
    if stripped.startswith('"') and stripped.endswith('"'):
        inner = stripped[1:-1]
        return bytes(inner, "utf-8").decode("unicode_escape")
    return stripped


def _contains_template_syntax(expression: str) -> bool:
    """Return whether an expression is a string.Format or interpolated string."""
    return "string.Format(" in expression or bool(_INTERPOLATED_STRING_RE.search(expression))


def _message_frame_name(code: str) -> str:
    """Extract the DidX-family frame variant from a matched call."""
    match = _METHOD_NAME_RE.search(code)
    if match is None:
        msg = f"Could not determine MessageFrame method for {code!r}."
        raise ValueError(msg)
    return match.group("method")


def _message_frame_argument(arguments: list[str], frame: str, role: str) -> str | None:
    """Select the requested argument expression from a MessageFrame call."""
    verb_index, extra_index = _message_frame_indices(arguments, frame)
    index = verb_index if role == "verb" else extra_index
    if index is None or index >= len(arguments):
        return None
    argument = arguments[index]
    return None if argument == "null" else argument


def _message_frame_indices(arguments: list[str], frame: str) -> tuple[int, int | None]:
    """Determine verb/extra argument positions for one MessageFrame signature."""
    verb_index, object_index, object_extra_index, direct_extra_index = _MESSAGE_FRAME_SPECS[frame]
    if object_index is None:
        return (verb_index, _extra_index(arguments, object_extra_index))
    if len(arguments) > object_index and _is_probable_object_expression(arguments[object_index]):
        return (verb_index, _extra_index(arguments, object_extra_index))
    return (verb_index, _extra_index(arguments, direct_extra_index))


def _clean_message_token(expression: str | None) -> str | None:
    """Normalize extracted MessageFrame tokens for JSON output."""
    if expression is None:
        return None
    if _is_string_literal(expression):
        return _unquote_string(expression)
    return expression.strip()


def _lookup_tier(extra_expression: str | None) -> int:
    """Assign the MessageFrame lookup tier from the extracted Extra expression."""
    if extra_expression is None:
        return 1
    if not _is_string_literal(extra_expression):
        return 3
    extra_value = _unquote_string(extra_expression)
    if any(marker in extra_value for marker in ("{", "}", "<", "=")):
        return 3
    return 2


def _looks_extra_expression(argument: str) -> bool:
    """Return whether an argument expression could be a MessageFrame Extra."""
    prefixes = (
        "UseFullNames:",
        "Indefinite",
        "Possessive",
        "Subject",
        "Color",
        "Describe",
        "AlwaysVisible",
        "FromDialog",
        "UsePopup",
        "Reference",
    )
    return not argument.startswith(prefixes)


def _is_probable_object_expression(argument: str) -> bool:
    """Return whether an argument looks like a GameObject/object expression."""
    return not _is_string_literal(argument) and not _looks_extra_expression(argument)


def _is_string_builder_to_string(expression: str, raw_hit: RawHit, source_root: Path) -> bool:
    """Return whether a .ToString() sink argument comes from a string builder."""
    match = re.fullmatch(r"(?P<target>[A-Za-z_][A-Za-z0-9_]*)\.ToString\(\)", expression.strip())
    if match is None:
        return False
    target = match.group("target")
    source_lines = _read_source_lines(source_root, raw_hit.file)
    for line in reversed(source_lines[: raw_hit.line - 1]):
        if re.search(rf"\b(?:{'|'.join(_STRING_BUILDER_TYPES)})\s+{re.escape(target)}\b", line):
            return True
        if re.search(rf"\b{re.escape(target)}\s*=\s*new\s+(?:{'|'.join(_STRING_BUILDER_TYPES)})\b", line):
            return True
        if re.search(rf"\b{re.escape(target)}\.Append\(", line):
            return True
    return target.lower().endswith("builder") or target == "SB"


def _string_prefix(text: str, index: int) -> tuple[int, bool] | None:
    """Return the matched string prefix length and whether it is verbatim."""
    prefixes = (
        ('$@"', True),
        ('@$"', True),
        ('@"', True),
        ('$"', False),
        ('"', False),
    )
    for prefix, verbatim in prefixes:
        if text.startswith(prefix, index):
            return (len(prefix), verbatim)
    return None


def _consume_string(text: str, index: int, *, verbatim: bool) -> int:
    """Advance past the end of a C# string literal."""
    while index < len(text):
        if verbatim and text[index] == '"' and index + 1 < len(text) and text[index + 1] == '"':
            index += 2
            continue
        if text[index] == '"' and (verbatim or text[index - 1] != "\\"):
            return index + 1
        index += 1
    return index


def _extra_index(arguments: list[str], index: int | None) -> int | None:
    """Return a validated extra-argument index, if present."""
    if index is None or len(arguments) <= index:
        return None
    return index if _looks_extra_expression(arguments[index]) else None


def _statement_context(raw_hit: RawHit, source_root: Path) -> str:
    """Extract a best-effort statement-sized source context for the hit line."""
    source_lines = _read_source_lines(source_root, raw_hit.file)
    line_index = max(raw_hit.line - 1, 0)
    start = line_index
    while start > 0 and start >= line_index - 5 and not source_lines[start - 1].rstrip().endswith((";", "{", "}")):
        start -= 1
    end = line_index
    while end + 1 < len(source_lines) and end <= line_index + 5 and ";" not in source_lines[end]:
        end += 1
    return " ".join(line.strip() for line in source_lines[start : end + 1]).strip()


@lru_cache(maxsize=512)
def _read_source_lines(source_root: Path, relative_file: str) -> tuple[str, ...]:
    """Read and cache a source file as individual lines."""
    source_path = source_root.expanduser().resolve() / relative_file
    return tuple(source_path.read_text(encoding="utf-8").splitlines())


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Parse CLI arguments for Phase 1b classification."""
    parser = argparse.ArgumentParser(description="Run Phase 1b rule-based source classification.")
    parser.add_argument(
        "source_root",
        nargs="?",
        default="~/dev/coq-decompiled_stable",
        help="Path to the decompiled C# source root.",
    )
    parser.add_argument(
        "--raw-hits",
        default=str(DEFAULT_RAW_HITS_PATH),
        help="Path to Phase 1a raw_hits.jsonl.",
    )
    parser.add_argument(
        "--output",
        default=str(DEFAULT_OUTPUT_PATH),
        help="Path to write inventory_draft.json.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Execute Phase 1b classification from the command line."""
    args = _parse_args(argv)
    draft = classify_raw_hits_file(
        Path(args.raw_hits),
        Path(args.source_root),
        output_path=Path(args.output),
    )
    sys.stdout.write(
        "Phase 1b classification complete: "
        f"{draft.stats.output_sites} sites, "
        f"{draft.stats.filtered_hits} filtered, "
        f"{draft.stats.high_confidence} high / "
        f"{draft.stats.medium_confidence} medium / "
        f"{draft.stats.low_confidence} low.\n"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
