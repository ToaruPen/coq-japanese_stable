# Issue #403 — JSON dictionary `{N}` placeholder parity plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore `{N}` numeric-placeholder parity in four shipped dictionary entries (one in `ui-popup.ja.json`, three in `ui-trade.ja.json`) and lock the invariant dict-wide via a new pytest contract.

**Architecture:** JSON dictionary fixes plus a pytest scanner enforcing dict-wide multiset-equality plus a minimal production C# correction in `PopupTranslationPatch.cs` (added `TranslateCampfirePoisonToken`). No production Python changes.

**Tech Stack:** Python 3.12 + pytest, `re` for the placeholder regex, `json` for parsing.

**Spec:** `docs/superpowers/specs/2026-04-25-issue-403-json-placeholder-parity-design.md`

---

## File map

| Action | Path | Responsibility |
| --- | --- | --- |
| Create | `scripts/tests/test_json_placeholder_parity.py` | Dict-wide pytest: every `*.ja.json` entry whose `key` contains `{N}` must have a matching `{N}` multiset in `text` |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json:2583` | Translate `text` to include `{0}` |
| Modify | `Mods/QudJP/Localization/Dictionaries/ui-trade.ja.json:59,64,69` | Translate `text` on three entries to include `{3}` and reorder `{1}` |

No new module files outside the test. No fixtures shared with other test modules (the test itself is small enough that a session-scoped fixture is unnecessary; one pass over ~64 small JSON files is sub-second).

---

### Task 1: Failing pytest

**Files:**
- Create: `scripts/tests/test_json_placeholder_parity.py`

- [ ] **Step 1: Write the failing test**

```python
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
        return []
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
    assert not mismatches, (
        f"Placeholder multiset mismatch in {path.relative_to(REPO_ROOT)}:\n"
        + "\n".join(mismatches)
    )
```

- [ ] **Step 2: Run test to verify it fails on exactly the expected entries**

Run: `uv run pytest scripts/tests/test_json_placeholder_parity.py -v`

Expected:
- Most parametrized cases (~62 of them, one per JSON file) PASS.
- `test_json_dictionary_numeric_placeholders_match[Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json]` FAILS with mismatch on the `You cure the {0}...` key (missing `{0}` in text).
- `test_json_dictionary_numeric_placeholders_match[Mods/QudJP/Localization/Dictionaries/ui-trade.ja.json]` FAILS, listing all three trade-debt entries (each missing `{3}`).

If any other JSON file fails, stop. The spec says exactly four entries mismatch; if a fifth surfaces, that is a new finding and must be triaged separately, not silently absorbed.

---

### Task 2: Fix `ui-popup.ja.json:2583`

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json:2583`

- [ ] **Step 1: Edit line 2583**

Replace this exact text-field value:

```json
            "text": "{2}で作った塗り薬で{1}を蝕む毒を治した。"
```

with:

```json
            "text": "{2}で作った塗り薬で{1}を蝕む{0}を治した。"
```

The surrounding entry (key, context, etc.) is unchanged. Only the `text` field is updated.

- [ ] **Step 2: Re-run the test**

Run: `uv run pytest scripts/tests/test_json_placeholder_parity.py -v`

Expected: `ui-popup.ja.json` case PASSES; `ui-trade.ja.json` case still FAILS.

---

### Task 3: Fix `ui-trade.ja.json:59` (the no-suffix variant)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/ui-trade.ja.json:59`

- [ ] **Step 1: Edit line 59**

Replace this exact text-field value:

```json
      "text": "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。"
```

with:

```json
      "text": "{0}は、あなたが{3}に借りている{2}を{1}に支払うまで取引してくれない。"
```

- [ ] **Step 2: Confirm exactly the next two entries are still red**

Don't run the test yet — Task 4 fixes the other two and runs the test once.

---

### Task 4: Fix `ui-trade.ja.json:64` and `:69` and verify

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/ui-trade.ja.json:64,69`

- [ ] **Step 1: Edit line 64 (the "give your N drams now" variant)**

Replace this exact text-field value:

```json
      "text": "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。今すぐあなたの{5}を{4}に渡しますか？"
```

with:

```json
      "text": "{0}は、あなたが{3}に借りている{2}を{1}に支払うまで取引してくれない。今すぐあなたの{5}を{4}に渡しますか？"
```

- [ ] **Step 2: Edit line 69 (the "give it to N now" variant)**

Replace this exact text-field value:

```json
      "text": "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。今すぐそれを{4}に渡しますか？"
```

with:

```json
      "text": "{0}は、あなたが{3}に借りている{2}を{1}に支払うまで取引してくれない。今すぐそれを{4}に渡しますか？"
```

- [ ] **Step 3: Run the placeholder-parity test**

Run: `uv run pytest scripts/tests/test_json_placeholder_parity.py -v`

Expected: every parametrized case PASSES.

- [ ] **Step 4: Run full pytest suite**

Run: `uv run pytest scripts/tests/ -q`

Expected: all green, no pre-existing tests newly fail.

- [ ] **Step 5: Run remaining repo checks**

Run each of the following from the repo root; all must succeed:

```bash
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

Expected: each command exits 0 with no new warnings or errors.

- [ ] **Step 6: Stop**

Do not commit. The user reviews the diff and runs the `/codex` review → `/simplify` → PR flow.

---

## Verification summary

After Task 4:

| Check | Command | Expectation |
| --- | --- | --- |
| Placeholder parity | `uv run pytest scripts/tests/test_json_placeholder_parity.py -v` | every parametrized case green |
| Full pytest suite | `uv run pytest scripts/tests/ -q` | all green |
| Strict XML validation | `python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json` | exit 0 |
| Encoding | `python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts` | clean |
| Ruff | `ruff check scripts/` | clean |
| DLL build | `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` | succeeds |

## Out-of-scope reminders

- Do not touch markup tokens (`{{Y|...}}`, `&X`, `^X`, `=var=`).
- Do not modify dictionaries other than the four entries listed.
- Do not alter the test scope to include XML files or other markup classes — that is for #401 and #409.
- Do not allowlist any benign mismatch; the spec asserts there is none.
