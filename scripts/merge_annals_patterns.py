"""Merge translated annals candidates into Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json."""
# ruff: noqa: T201, D103, PLR0911

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

# Importable as a library by tests
sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_candidate_schema as schema

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CANDIDATES = REPO_ROOT / "scripts/_artifacts/annals/candidates_pending.json"
DEFAULT_JOURNAL = REPO_ROOT / "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json"
DEFAULT_ANNALS = REPO_ROOT / "Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json"
DEFAULT_CONFLICTS = REPO_ROOT / "scripts/_artifacts/annals/merge_conflicts.json"


def normalize_sample(sample: str, slots: list[dict[str, Any]]) -> str:
    """Replace each slot's raw form in the sample with `SLOT<index>`.

    str.replace() replaces ALL occurrences of the same raw value with the same
    SLOT{index} token.  This is intentional for PR1: each unique raw string maps
    to exactly one slot index, so repeated occurrences of the same raw value
    should normalize to the same placeholder.
    """
    out = sample
    for slot in slots:
        raw = slot.get("raw", "")
        if raw and raw in out:
            out = out.replace(raw, f"SLOT{slot.get('index', 0)}")
    return out


def detect_collisions(
    candidate: dict[str, Any],
    journal_patterns: list[dict[str, str]],
    journal_path: Path,
) -> list[dict[str, Any]]:
    """Return zero or more conflict entries for this candidate against the journal-patterns dict."""
    sample = candidate["sample_source"]
    normalized = normalize_sample(sample, candidate["slots"])
    conflicts: list[dict[str, Any]] = []

    for index, entry in enumerate(journal_patterns):
        existing_pattern = entry.get("pattern")
        if not existing_pattern:
            continue
        try:
            compiled = re.compile(existing_pattern)
        except re.error:
            continue
        match_raw = bool(compiled.search(sample))
        match_normalized = bool(compiled.search(normalized))
        if match_raw or match_normalized:
            conflict_type = "raw" if match_raw else "normalized"
            resolution_first = "skip_candidate" if conflict_type == "raw" else "narrow_candidate_pattern"
            other_resolutions = [
                opt
                for opt in ("skip_candidate", "narrow_candidate_pattern", "replace_existing_after_review")
                if opt != resolution_first
            ]
            conflicts.append(
                {
                    "candidate_id": candidate["id"],
                    "candidate_pattern": candidate["extracted_pattern"],
                    "candidate_pattern_normalized": candidate["extracted_pattern"],  # PR1: same as raw; v2 may differ
                    "candidate_template": candidate["ja_template"],
                    "sample_source": sample,
                    "conflict_type": conflict_type,
                    "conflicts": [
                        {
                            "file": str(journal_path),
                            "pattern_index": index,
                            "pattern": existing_pattern,
                            "pattern_normalized": existing_pattern,
                            "template": entry.get("template", ""),
                        }
                    ],
                    "resolution_options": [resolution_first, *other_resolutions],
                }
            )
    return conflicts


def dedupe_by_pattern(
    candidates: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[str]]:
    """Group candidates by extracted_pattern, return dedup + divergent errors.

    Same regex from multiple ids (one local feeding two SetEventProperty calls,
    branch arms with different slot raws but same regex shape) is legitimate —
    but runtime first-match-wins drops everything past the first occurrence, so
    identical templates collapse silently and divergent ones are surfaced as
    errors so the operator reconciles to a single canonical translation.
    """
    by_pattern: dict[str, list[dict[str, Any]]] = {}
    for candidate in candidates:
        by_pattern.setdefault(candidate["extracted_pattern"], []).append(candidate)
    duplicate_errors: list[str] = []
    deduped: list[dict[str, Any]] = []
    for pattern, members in by_pattern.items():
        if len(members) == 1:
            deduped.append(members[0])
            continue
        templates = {m["ja_template"] for m in members}
        if len(templates) > 1:
            ids_and_templates = "; ".join(f"{m['id']!r}={m['ja_template']!r}" for m in members)
            duplicate_errors.append(
                f"pattern {pattern!r} has divergent ja_templates across ids: {ids_and_templates}. "
                "Reconcile to a single canonical translation or split the pattern."
            )
            continue
        deduped.append(members[0])
    return deduped, duplicate_errors


def load_inputs(
    candidates_path: Path,
    existing_journal: Path,
    annals_output: Path,
) -> tuple[dict[str, Any], list[dict[str, Any]]] | int:
    """Load + validate candidates JSON and existing journal/annals dicts.

    Returns (candidates_doc, journal_patterns) on success, or an int exit code on
    any failure (with a message already printed to stderr).
    """
    try:
        doc = json.loads(candidates_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: cannot read candidates from {candidates_path}: {exc}", file=sys.stderr)
        return 1
    try:
        schema.validate_doc(doc)
    except schema.ValidationError as exc:
        print(f"error: candidate schema invalid: {exc}", file=sys.stderr)
        return 1
    try:
        journal_doc = json.loads(existing_journal.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: cannot read existing journal-patterns from {existing_journal}: {exc}", file=sys.stderr)
        return 1
    journal_patterns = journal_doc.get("patterns", [])
    if not isinstance(journal_patterns, list):
        print("error: journal-patterns.ja.json has no 'patterns' array", file=sys.stderr)
        return 1
    if annals_output.exists():
        try:
            existing_annals = json.loads(annals_output.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            print(
                f"error: cannot read existing annals-patterns from {annals_output}: {exc}",
                file=sys.stderr,
            )
            return 1
        if "patterns" not in existing_annals:
            print("error: existing annals-patterns.ja.json missing 'patterns' field", file=sys.stderr)
            return 1
    return doc, journal_patterns


def run_merge(
    candidates_path: Path,
    existing_journal: Path,
    annals_output: Path,
    conflicts_output: Path,
) -> int:
    loaded = load_inputs(candidates_path, existing_journal, annals_output)
    if isinstance(loaded, int):
        return loaded
    doc, journal_patterns = loaded

    accepted = [c for c in doc["candidates"] if c["status"] == "accepted" and c["ja_template"]]

    all_conflicts: list[dict[str, Any]] = []
    for candidate in accepted:
        all_conflicts.extend(detect_collisions(candidate, journal_patterns, existing_journal))

    if all_conflicts:
        conflicts_doc = {"schema_version": "1", "conflicts": all_conflicts}
        conflicts_output.parent.mkdir(parents=True, exist_ok=True)
        conflicts_output.write_text(
            json.dumps(conflicts_doc, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(f"error: {len(all_conflicts)} conflict(s); see {conflicts_output}", file=sys.stderr)
        return 1

    # Clean run: remove stale conflicts artifact
    if conflicts_output.exists():
        conflicts_output.unlink()

    # Sort by pattern length descending so concrete (longer) patterns match before
    # general (shorter, more `(.+?)` captures) patterns at runtime. The runtime regex
    # iterates patterns in file order and uses the first match — without this ordering,
    # a generic FoundAsBabe pattern from PR2a would swallow specific Resheph patterns
    # whose literal text is fully covered by the FoundAsBabe template's `(.+?)` slots.
    # Within equal pattern length, sort by id for deterministic output.
    accepted_sorted = sorted(accepted, key=lambda c: (-len(c["extracted_pattern"]), c["id"]))

    deduped, duplicate_errors = dedupe_by_pattern(accepted_sorted)
    if duplicate_errors:
        for err in duplicate_errors:
            print(f"error: {err}", file=sys.stderr)
        return 1
    accepted_sorted = deduped
    annals_doc = {
        "entries": [],
        "patterns": [
            {
                "pattern": c["extracted_pattern"],
                "template": c["ja_template"],
                "route": "annals",
            }
            for c in accepted_sorted
        ],
    }
    annals_output.parent.mkdir(parents=True, exist_ok=True)
    annals_output.write_text(
        json.dumps(annals_doc, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"[merge] OK — wrote {len(accepted_sorted)} pattern(s) to {annals_output}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Merge accepted annals candidates into the dictionary.")
    parser.add_argument("path", type=Path, default=DEFAULT_CANDIDATES, nargs="?", help="Path to candidates JSON")
    parser.add_argument("--journal", type=Path, default=DEFAULT_JOURNAL, help="Existing journal-patterns.ja.json")
    parser.add_argument("--annals-output", type=Path, default=DEFAULT_ANNALS, help="Output annals-patterns.ja.json")
    parser.add_argument("--conflicts-output", type=Path, default=DEFAULT_CONFLICTS, help="Conflict report output")
    args = parser.parse_args(argv)

    return run_merge(
        candidates_path=args.path,
        existing_journal=args.journal,
        annals_output=args.annals_output,
        conflicts_output=args.conflicts_output,
    )


if __name__ == "__main__":
    raise SystemExit(main())
