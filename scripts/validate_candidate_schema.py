"""Validate the candidate JSON schema produced by extract_annals_patterns.py."""
# ruff: noqa: T201, D103, TRY003, EM102, EM101, C901, PLR0912, ANN401

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

EXPECTED_SCHEMA_VERSION = "1"
ALLOWED_TOP_LEVEL_KEYS = {"schema_version", "candidates"}
REQUIRED_CANDIDATE_KEYS = {
    "id",
    "source_file",
    "annal_class",
    "switch_case",
    "event_property",
    "sample_source",
    "extracted_pattern",
    "slots",
    "status",
    "reason",
    "ja_template",
    "route",
    "en_template_hash",
}
ALLOWED_CANDIDATE_KEYS = REQUIRED_CANDIDATE_KEYS | {"review_notes"}
VALID_STATUSES = {"pending", "accepted", "needs_manual", "skip"}
VALID_SLOT_TYPES = {
    "spice",
    "entity-property",
    "grammar-helper",
    "string-format-arg",
    "unresolved-local",
    "hse-expansion",
}
VALID_EVENT_PROPERTIES = {"gospel", "tombInscription"}
PLACEHOLDER_RE = re.compile(r"\{(t?)(\d+)\}")


class ValidationError(Exception):
    """Schema validation failed."""


def validate_doc(doc: dict[str, Any]) -> None:
    """Raise ValidationError on any failure. Returns None on success."""
    if not isinstance(doc, dict):
        raise ValidationError("top-level value must be a JSON object")

    extra = set(doc.keys()) - ALLOWED_TOP_LEVEL_KEYS
    if extra:
        raise ValidationError(f"unknown top-level field(s): {sorted(extra)}")

    schema_version = doc.get("schema_version")
    if schema_version is None:
        raise ValidationError("missing top-level field: schema_version")
    if schema_version != EXPECTED_SCHEMA_VERSION:
        raise ValidationError(f"unsupported schema_version: {schema_version!r} (expected {EXPECTED_SCHEMA_VERSION!r})")

    candidates = doc.get("candidates")
    if not isinstance(candidates, list):
        raise ValidationError("candidates must be a list")

    seen_ids: set[str] = set()
    for index, candidate in enumerate(candidates):
        validate_candidate(candidate, index)
        cid = candidate["id"]
        if cid in seen_ids:
            raise ValidationError(f"duplicate candidate id: {cid!r} (must be unique)")
        seen_ids.add(cid)


def validate_candidate(candidate: Any, index: int) -> None:
    if not isinstance(candidate, dict):
        raise ValidationError(f"candidate[{index}] must be a JSON object")

    missing = REQUIRED_CANDIDATE_KEYS - set(candidate.keys())
    if missing:
        raise ValidationError(f"candidate[{index}] missing required field(s): {sorted(missing)}")

    extra = set(candidate.keys()) - ALLOWED_CANDIDATE_KEYS
    if extra:
        raise ValidationError(f"candidate[{index}] unknown field(s): {sorted(extra)}")

    status = candidate["status"]
    if status not in VALID_STATUSES:
        raise ValidationError(f"candidate[{index}] invalid status: {status!r} (allowed: {sorted(VALID_STATUSES)})")

    if candidate["event_property"] not in VALID_EVENT_PROPERTIES:
        raise ValidationError(f"candidate[{index}] invalid event_property: {candidate['event_property']!r}")

    pattern = candidate["extracted_pattern"]
    capture_count: int
    if pattern == "" and status in {"needs_manual", "skip"}:
        capture_count = 0
    else:
        try:
            compiled = re.compile(pattern)
        except re.error as exc:
            raise ValidationError(f"candidate[{index}] regex compile failed: {exc}") from exc
        capture_count = compiled.groups

    ja_template = candidate["ja_template"]
    if status == "accepted" and not ja_template:
        raise ValidationError(f"candidate[{index}] status=accepted requires non-empty ja_template")
    if ja_template:
        for match in PLACEHOLDER_RE.finditer(ja_template):
            slot_index = int(match.group(2))
            if slot_index >= capture_count:
                raise ValidationError(
                    f"candidate[{index}] ja_template placeholder index {slot_index} "
                    f"exceeds capture count {capture_count} of pattern {pattern!r}"
                )

    slots = candidate["slots"]
    if not isinstance(slots, list):
        raise ValidationError(f"candidate[{index}] slots must be a list")
    for slot_index, slot in enumerate(slots):
        if not isinstance(slot, dict):
            raise ValidationError(f"candidate[{index}].slots[{slot_index}] must be a JSON object")
        for required in ("index", "type", "raw", "default"):
            if required not in slot:
                raise ValidationError(f"candidate[{index}].slots[{slot_index}] missing field: {required}")
        if slot["type"] not in VALID_SLOT_TYPES:
            raise ValidationError(f"candidate[{index}].slots[{slot_index}] invalid type: {slot['type']!r}")

    route = candidate["route"]
    if route != "annals":
        raise ValidationError(f"candidate[{index}] route must be 'annals' (got {route!r})")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate annals candidate JSON.")
    parser.add_argument("path", type=Path, help="Path to candidates JSON file")
    args = parser.parse_args(argv)

    try:
        doc = json.loads(args.path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: could not read JSON from {args.path}: {exc}", file=sys.stderr)
        return 1

    try:
        validate_doc(doc)
    except ValidationError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1

    print(f"[validate] OK — {len(doc['candidates'])} candidate(s) pass schema check")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
