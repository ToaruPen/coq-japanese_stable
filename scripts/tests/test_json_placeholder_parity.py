"""Issue #403 — every JSON dictionary entry must preserve `{N}` numeric placeholders."""

from __future__ import annotations

import json
import re
from collections import Counter
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
DICTIONARIES_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"

# Index-aware: matches `{0}`, `{12}`, `{0:+#;-#}`, etc. Captures the index only.
PLACEHOLDER = re.compile(r"\{([0-9]+)(?::[^{}]*)?\}")


def _placeholder_multiset(value: str) -> Counter[str]:
    return Counter(match.group(1) for match in PLACEHOLDER.finditer(value))


def _all_dictionary_files() -> list[Path]:
    return sorted(DICTIONARIES_ROOT.rglob("*.ja.json"))


def _entries_with_placeholder_keys(path: Path) -> list[tuple[str, str]]:
    """Return [(key, text), ...] for entries whose key contains a `{N}` placeholder."""
    data = json.loads(path.read_text(encoding="utf-8"))
    raw_entries = data.get("entries", []) if isinstance(data, dict) else data
    if not isinstance(raw_entries, list):
        msg = (
            f"Malformed dictionary structure in {path}: expected 'entries' to be a list, "
            f"got {type(raw_entries).__name__}. data={data!r}, raw_entries={raw_entries!r}"
        )
        raise TypeError(msg)
    pairs: list[tuple[str, str]] = []
    for entry in raw_entries:
        if not isinstance(entry, dict):
            continue
        key = entry.get("key", "")
        text = entry.get("text", "")
        if not isinstance(key, str) or not isinstance(text, str):
            continue
        if PLACEHOLDER.search(key):
            pairs.append((key, text))
    return pairs


@pytest.mark.parametrize("path", _all_dictionary_files(), ids=lambda p: p.relative_to(REPO_ROOT).as_posix())
def test_json_dictionary_numeric_placeholders_match(path: Path) -> None:
    """Every dictionary entry must preserve the multiset of `{N}` placeholders from key to text."""
    mismatches: list[str] = []
    for key, text in _entries_with_placeholder_keys(path):
        key_slots = _placeholder_multiset(key)
        text_slots = _placeholder_multiset(text)
        if key_slots != text_slots:
            mismatches.append(
                f"  key={key!r}\n    key_slots={dict(key_slots)} text_slots={dict(text_slots)}\n    text={text!r}"
            )
    assert not mismatches, f"Placeholder multiset mismatch in {path.relative_to(REPO_ROOT)}:\n" + "\n".join(mismatches)
