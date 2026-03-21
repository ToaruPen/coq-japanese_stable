"""Audit translation dictionaries for structural anti-patterns."""

from __future__ import annotations

import json
import re
from typing import TYPE_CHECKING

from scripts.provenance.models import AuditFinding, AuditFindingKind, DictEntry

if TYPE_CHECKING:
    from pathlib import Path

_FRAGMENT_TRAILING_WS = re.compile(r"\s+$")
_FRAGMENT_LEADING_WS = re.compile(r"^\s+")
_FRAGMENT_OPEN_BRACKET = re.compile(r"(?:\[|\()$")
_PLACEHOLDER_PATTERN = re.compile(r"\{([A-Za-z_][A-Za-z0-9_]*)\}")


def load_dictionary_entries(dictionaries_dir: Path) -> list[DictEntry]:
    """Load translation entries from every ``*.json`` dictionary file.

    The loader accepts all known repository schema variants:

    - ``meta`` + ``rules`` + ``entries``
    - ``entries`` only
    - ``entries`` + ``patterns``

    The ``patterns`` collection is intentionally ignored because message-pattern
    translation is handled elsewhere and is out of scope for provenance analysis.

    Args:
        dictionaries_dir: Directory containing dictionary JSON files.

    Returns:
        Loaded dictionary entries sorted by file name order.

    Raises:
        FileNotFoundError: If the directory does not exist.
        ValueError: If the path is not a directory.
    """
    if not dictionaries_dir.exists():
        msg = f"Dictionaries directory not found: {dictionaries_dir}"
        raise FileNotFoundError(msg)
    if not dictionaries_dir.is_dir():
        msg = f"Dictionaries path is not a directory: {dictionaries_dir}"
        raise ValueError(msg)

    entries: list[DictEntry] = []
    for path in sorted(dictionaries_dir.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        dict_id = _resolve_dictionary_id(path, data)
        entries.extend(
            DictEntry(
                dictionary_id=dict_id,
                key=str(raw_entry["key"]),
                text=str(raw_entry.get("text", "")),
                context=_normalize_context(raw_entry.get("context")),
            )
            for raw_entry in data.get("entries", [])
        )
    return entries


def audit_dictionaries(dictionaries_dir: Path) -> list[AuditFinding]:
    """Run all dictionary audit checks for a directory.

    Args:
        dictionaries_dir: Directory containing translation dictionaries.

    Returns:
        Aggregated findings from fragment, duplicate, and placeholder checks.
    """
    entries = load_dictionary_entries(dictionaries_dir)
    findings: list[AuditFinding] = []
    findings.extend(_detect_fragments(entries))
    findings.extend(_detect_duplicates(entries))
    findings.extend(_detect_placeholder_issues(entries))
    return findings


def _resolve_dictionary_id(path: Path, data: dict[str, object]) -> str:
    """Return the effective dictionary id for one JSON file."""
    meta = data.get("meta")
    if isinstance(meta, dict):
        meta_id = meta.get("id")
        if isinstance(meta_id, str) and meta_id:
            return meta_id
    return path.name.removesuffix(".ja.json").removesuffix(".json")


def _normalize_context(raw_context: object) -> str | None:
    """Normalize an optional context value from JSON input."""
    if raw_context is None:
        return None
    if isinstance(raw_context, str):
        stripped = raw_context.strip()
        return stripped or None
    return str(raw_context)


def _detect_fragments(entries: list[DictEntry]) -> list[AuditFinding]:
    """Detect keys that look like string fragments instead of complete leaves."""
    findings: list[AuditFinding] = []
    for entry in entries:
        if _FRAGMENT_TRAILING_WS.search(entry.key):
            findings.append(
                AuditFinding(
                    kind=AuditFindingKind.FRAGMENT_KEY,
                    dictionary_id=entry.dictionary_id,
                    key=entry.key,
                    message=f"Key has trailing whitespace and likely represents a string fragment: {entry.key!r}",
                )
            )
            continue
        if _FRAGMENT_LEADING_WS.search(entry.key):
            findings.append(
                AuditFinding(
                    kind=AuditFindingKind.FRAGMENT_KEY,
                    dictionary_id=entry.dictionary_id,
                    key=entry.key,
                    message=f"Key has leading whitespace and likely represents a suffix fragment: {entry.key!r}",
                )
            )
            continue
        if _FRAGMENT_OPEN_BRACKET.search(entry.key):
            findings.append(
                AuditFinding(
                    kind=AuditFindingKind.FRAGMENT_KEY,
                    dictionary_id=entry.dictionary_id,
                    key=entry.key,
                    message=f"Key ends with an open bracket or paren and looks truncated: {entry.key!r}",
                )
            )
    return findings


def _detect_duplicates(entries: list[DictEntry]) -> list[AuditFinding]:
    """Detect duplicate keys across dictionaries."""
    seen: dict[str, DictEntry] = {}
    findings: list[AuditFinding] = []
    for entry in entries:
        previous = seen.get(entry.key)
        if previous is None:
            seen[entry.key] = entry
            continue
        findings.append(
            AuditFinding(
                kind=AuditFindingKind.DUPLICATE_KEY,
                dictionary_id=entry.dictionary_id,
                key=entry.key,
                message=(
                    f"Duplicate key already exists in dictionary {previous.dictionary_id!r}; "
                    "verify ownership before keeping both entries"
                ),
                related_dictionary_id=previous.dictionary_id,
                related_key=previous.key,
            )
        )
    return findings


def _detect_placeholder_issues(entries: list[DictEntry]) -> list[AuditFinding]:
    """Detect placeholder mismatches and dead placeholders."""
    findings: list[AuditFinding] = []
    for entry in entries:
        key_placeholders = _extract_placeholders(entry.key)
        text_placeholders = _extract_placeholders(entry.text)
        if key_placeholders and text_placeholders and key_placeholders != text_placeholders:
            findings.append(
                AuditFinding(
                    kind=AuditFindingKind.PLACEHOLDER_MISMATCH,
                    dictionary_id=entry.dictionary_id,
                    key=entry.key,
                    message=(
                        f"Key placeholders {sorted(key_placeholders)} do not match text placeholders "
                        f"{sorted(text_placeholders)}"
                    ),
                )
            )
            continue
        if not key_placeholders and text_placeholders:
            findings.append(
                AuditFinding(
                    kind=AuditFindingKind.DEAD_PLACEHOLDER,
                    dictionary_id=entry.dictionary_id,
                    key=entry.key,
                    message=(
                        f"Text includes unresolved placeholders {sorted(text_placeholders)} but the key has none"
                    ),
                )
            )
    return findings


def _extract_placeholders(text: str) -> set[str]:
    """Extract ``{name}`` placeholders from a string."""
    return set(_PLACEHOLDER_PATTERN.findall(text))
