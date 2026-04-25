"""Issue #401 — every JSON dictionary entry must preserve markup tokens carried by `key`.

Two invariants checked, both asymmetric (preservation, not exact equality):

- For every `{{NAME|` opener present in `key`, `text` must carry at least the same
  count under the same color/shader name.
- For every `&&` / `^^` literal escape count in `key`, `text` must carry at least
  the same count.

Adding decoration in `text` that is not in `key` is allowed — some dictionaries
(e.g. `mutation-descriptions.ja.json`, `mutation-ranktext.ja.json`) use
identifier-style keys without markup and style the rendered text. The contract is
"do not LOSE source-side tokens", not "match exactly".

The opener regex captures the name before the pipe; bare `{{phase-conjugate}}`
forms (no pipe) are intentionally ignored because the Caves of Qud runtime
rejects them. The inner content of color spans legitimately changes across
translation, so it is not compared.
"""

from __future__ import annotations

import json
import re
from collections import Counter
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
DICTIONARIES_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"

OPENER = re.compile(r"\{\{(?P<name>[^|}]+)\|")
LITERALS: tuple[str, ...] = ("&&", "^^")


def _opener_multiset(value: str) -> Counter[str]:
    return Counter(match.group("name") for match in OPENER.finditer(value))


def _literal_multiset(value: str) -> Counter[str]:
    return Counter({token: count for token in LITERALS if (count := value.count(token)) > 0})


def _all_dictionary_files() -> list[Path]:
    # Intentionally scans all *.json (not just *.ja.json): the downstream _entries
    # function filters to files with the expected key/text dictionary structure.
    files = sorted(DICTIONARIES_ROOT.rglob("*.json"))
    if not files:
        msg = (
            f"No *.json files found under {DICTIONARIES_ROOT}; "
            "DICTIONARIES_ROOT may be misconfigured or the directory is missing."
        )
        raise AssertionError(msg)
    return files


def _entries(path: Path) -> list[tuple[int, str, str]]:
    """Return [(index, key, text), ...] for every entry in a dictionary file."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        rel = path.relative_to(REPO_ROOT)
        msg = f"Failed to read/parse {rel}: {exc}"
        raise ValueError(msg) from exc
    raw_entries = data.get("entries", []) if isinstance(data, dict) else data
    if not isinstance(raw_entries, list):
        return []
    pairs: list[tuple[int, str, str]] = []
    for index, entry in enumerate(raw_entries, start=1):
        if not isinstance(entry, dict):
            continue
        key = entry.get("key", "")
        text = entry.get("text", "")
        if isinstance(key, str) and isinstance(text, str):
            pairs.append((index, key, text))
    return pairs


@pytest.mark.parametrize("path", _all_dictionary_files(), ids=lambda p: p.relative_to(REPO_ROOT).as_posix())
def test_json_dictionary_markup_openers_preserved(path: Path) -> None:
    """`text` must carry at least the `{{NAME|` openers present in `key`."""
    mismatches: list[str] = []
    for index, key, text in _entries(path):
        key_sig = _opener_multiset(key)
        if not key_sig:
            continue
        text_sig = _opener_multiset(text)
        missing = key_sig - text_sig
        if missing:
            mismatches.append(
                f"  entry #{index}\n    key={key!r}\n    text={text!r}\n"
                f"    missing_openers={dict(missing)} key_openers={dict(key_sig)} text_openers={dict(text_sig)}"
            )
    assert not mismatches, f"Markup opener loss in {path.relative_to(REPO_ROOT)}:\n" + "\n".join(mismatches)


@pytest.mark.parametrize("path", _all_dictionary_files(), ids=lambda p: p.relative_to(REPO_ROOT).as_posix())
def test_json_dictionary_literal_escapes_preserved(path: Path) -> None:
    """`text` must carry at least the `&&` / `^^` literal escape counts present in `key`."""
    mismatches: list[str] = []
    for index, key, text in _entries(path):
        key_sig = _literal_multiset(key)
        if not key_sig:
            continue
        text_sig = _literal_multiset(text)
        missing = key_sig - text_sig
        if missing:
            mismatches.append(
                f"  entry #{index}\n    key={key!r}\n    text={text!r}\n"
                f"    missing_literals={dict(missing)} key_literals={dict(key_sig)} text_literals={dict(text_sig)}"
            )
    assert not mismatches, f"Literal escape loss in {path.relative_to(REPO_ROOT)}:\n" + "\n".join(mismatches)
