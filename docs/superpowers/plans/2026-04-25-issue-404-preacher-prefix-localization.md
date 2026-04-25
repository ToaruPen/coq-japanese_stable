# Issue #404 — Preacher Prefix/Frozen localization plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Translate `Prefix` and `Frozen` attributes on the five `<part Name="Preacher" ...>` overrides in `Creatures.jp.xml` to Japanese, add explicit `Postfix="'}}"` so the single-attribute color-balance check naturally passes, remove the now-unnecessary baseline entry, and lock the invariants behind a pytest.

**Architecture:** Pure data-side fix in shipped XML plus a pytest contract that owns the resulting invariants. No C# / Harmony / validator changes.

**Tech Stack:** Python 3.12 + pytest, `scripts/validate_xml.py` library API, XML edits via the `Edit` tool.

**Spec:** `docs/superpowers/specs/2026-04-25-issue-404-preacher-prefix-localization-design.md`

---

## File map

| Action | Path | Responsibility |
| --- | --- | --- |
| Create | `scripts/tests/test_preacher_localization.py` | Pytest contract: 5 known Preacher books, Japanese Prefix/Frozen, exact Postfix, no unbalanced-color warning |
| Modify | `Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml:5574,5580,5586,5592,11783` | Translate Prefix + Frozen, add Postfix="'}}" |
| Modify | `scripts/validate_xml_warning_baseline.json` | Remove the `"Unbalanced color code at line 5574"` entry for Creatures.jp.xml |

No new module files, no helpers. The pytest imports `validate_xml_file` from `scripts/validate_xml.py` and parses XML with `xml.etree.ElementTree`.

---

### Task 1: Failing pytest

**Files:**
- Create: `scripts/tests/test_preacher_localization.py`

- [ ] **Step 1: Write the failing test**

```python
"""Issue #404 — Mechanimist Preacher / High Sermon Prefix/Frozen localization."""

from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

from scripts.validate_xml import validate_xml_file

REPO_ROOT = Path(__file__).resolve().parents[2]
CREATURES_XML = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "ObjectBlueprints" / "Creatures.jp.xml"

EXPECTED_BOOKS = {"Preacher1", "Preacher2", "Preacher3", "Preacher4", "HighSermon"}


def _preacher_parts() -> list[ET.Element]:
    root = ET.parse(CREATURES_XML).getroot()  # noqa: S314 -- local repository XML
    return [part for part in root.iter("part") if part.attrib.get("Name") == "Preacher"]


def _has_non_ascii(value: str) -> bool:
    return any(ord(ch) > 127 for ch in value)


def test_preacher_book_set_is_exact() -> None:
    """Issue #404: every Preacher part is one of the five known books."""
    parts = _preacher_parts()
    books = {part.attrib.get("Book", "") for part in parts}
    assert books == EXPECTED_BOOKS, (
        f"Expected Preacher books {EXPECTED_BOOKS}, got {books}. "
        "A new Preacher entry surfaced in the data; review and translate before merging."
    )


@pytest.mark.parametrize("book", sorted(EXPECTED_BOOKS))
def test_preacher_prefix_is_japanese_and_ends_with_w_quote(book: str) -> None:
    """Prefix translated to Japanese, still ends with `{{W|'` so the C# Postfix can close the span."""
    parts = [p for p in _preacher_parts() if p.attrib.get("Book") == book]
    assert parts, f"Preacher Book={book!r} not found"
    for part in parts:
        prefix = part.attrib.get("Prefix", "")
        assert prefix.endswith("{{W|'"), f"Prefix for Book={book!r} must end with {{{{W|'. Got: {prefix!r}"
        assert _has_non_ascii(prefix), f"Prefix for Book={book!r} must contain Japanese. Got: {prefix!r}"


@pytest.mark.parametrize("book", sorted(EXPECTED_BOOKS))
def test_preacher_postfix_is_explicit(book: str) -> None:
    """Postfix='}}' is declared explicitly so validate_xml's balance check passes without baseline help."""
    parts = [p for p in _preacher_parts() if p.attrib.get("Book") == book]
    assert parts
    for part in parts:
        postfix = part.attrib.get("Postfix")
        assert postfix == "'}}", f"Postfix for Book={book!r} must be exactly '}}}}. Got: {postfix!r}"


@pytest.mark.parametrize("book", sorted(EXPECTED_BOOKS))
def test_preacher_frozen_is_japanese(book: str) -> None:
    """Frozen attribute must contain Japanese."""
    parts = [p for p in _preacher_parts() if p.attrib.get("Book") == book]
    assert parts
    for part in parts:
        frozen = part.attrib.get("Frozen", "")
        assert _has_non_ascii(frozen), f"Frozen for Book={book!r} must contain Japanese. Got: {frozen!r}"


def test_creatures_xml_has_no_unbalanced_color_warning() -> None:
    """validate_xml.py must produce no 'Unbalanced color code' warning on Creatures.jp.xml."""
    result = validate_xml_file(CREATURES_XML)
    color_warnings = [w for w in result.warnings if "Unbalanced color code" in w]
    assert color_warnings == [], (
        f"Expected no unbalanced-color warnings; got: {color_warnings}. "
        "Make sure every <part Name=\"Preacher\"> declares Postfix=\"'}}\"."
    )
```

- [ ] **Step 2: Run test to verify it fails**

Run: `uv run pytest scripts/tests/test_preacher_localization.py -v`

Expected:
- `test_preacher_book_set_is_exact` — PASS (data already has the 5 books)
- `test_preacher_prefix_is_japanese_and_ends_with_w_quote` — FAIL on every book; current Prefix is `"The preacher says, {{W|'"` which is ASCII-only.
- `test_preacher_postfix_is_explicit` — FAIL on every book; current entries have no `Postfix` attribute.
- `test_preacher_frozen_is_japanese` — FAIL on every book; current Frozen is English.
- `test_creatures_xml_has_no_unbalanced_color_warning` — FAIL; warning `"Unbalanced color code at line 5574"` is currently emitted.

- [ ] **Step 3: Commit the failing test**

(Skip — user CLAUDE.md says "Commit only when explicitly requested". Move on without committing; we will commit at the end of Task 4.)

---

### Task 2: Translate Mechanimist Preacher 1–4

**Files:**
- Modify: `Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml:5574,5580,5586,5592`

- [ ] **Step 1: Edit line 5574 — Mechanimist Preacher 1**

Replace:

```xml
    <part Name="Preacher" Book="Preacher1" Prefix="The preacher says, {{W|'" Frozen="The preacher mumbles inaudibly, encased in ice." inOrder="true" ChatWait="200" />
```

With:

```xml
    <part Name="Preacher" Book="Preacher1" Prefix="説教者は言う、{{W|'" Postfix="'}}" Frozen="説教者は氷に閉じ込められ、聞き取れない声でぶつぶつ言っている。" inOrder="true" ChatWait="200" />
```

- [ ] **Step 2: Edit line 5580 — Mechanimist Preacher 2**

Same shape, only `Book` differs:

```xml
    <part Name="Preacher" Book="Preacher2" Prefix="説教者は言う、{{W|'" Postfix="'}}" Frozen="説教者は氷に閉じ込められ、聞き取れない声でぶつぶつ言っている。" inOrder="true" ChatWait="200" />
```

- [ ] **Step 3: Edit line 5586 — Mechanimist Preacher 3**

```xml
    <part Name="Preacher" Book="Preacher3" Prefix="説教者は言う、{{W|'" Postfix="'}}" Frozen="説教者は氷に閉じ込められ、聞き取れない声でぶつぶつ言っている。" inOrder="true" ChatWait="200" />
```

- [ ] **Step 4: Edit line 5592 — Mechanimist Preacher 4**

```xml
    <part Name="Preacher" Book="Preacher4" Prefix="説教者は言う、{{W|'" Postfix="'}}" Frozen="説教者は氷に閉じ込められ、聞き取れない声でぶつぶつ言っている。" inOrder="true" ChatWait="200" />
```

- [ ] **Step 5: Re-run pytest to confirm partial progress**

Run: `uv run pytest scripts/tests/test_preacher_localization.py -v`

Expected:
- `test_preacher_book_set_is_exact` — PASS
- `test_preacher_prefix_is_japanese_and_ends_with_w_quote[HighSermon]` — still FAIL
- `test_preacher_prefix_is_japanese_and_ends_with_w_quote[Preacher1..4]` — PASS
- `test_preacher_postfix_is_explicit[HighSermon]` — still FAIL
- `test_preacher_postfix_is_explicit[Preacher1..4]` — PASS
- `test_preacher_frozen_is_japanese[HighSermon]` — still FAIL
- `test_preacher_frozen_is_japanese[Preacher1..4]` — PASS
- `test_creatures_xml_has_no_unbalanced_color_warning` — still FAIL (HighSermon line 11783 keeps the file unbalanced)

---

### Task 3: Translate High Priest Eschelstadt (HighSermon)

**Files:**
- Modify: `Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml:11783`

- [ ] **Step 1: Edit line 11783**

Replace:

```xml
    <part Name="Preacher" Book="HighSermon" Prefix="The preacher says, {{W|'" Frozen="Eschelstadt II, High Priest of the Stilt mumbles inaudibly, encased in ice." inOrder="true" ChatWait="75" />
```

With:

```xml
    <part Name="Preacher" Book="HighSermon" Prefix="説教者は言う、{{W|'" Postfix="'}}" Frozen="スティルトの大司祭エッシェルシュタット II は氷に閉じ込められ、聞き取れない声でぶつぶつ言っている。" inOrder="true" ChatWait="75" />
```

- [ ] **Step 2: Re-run pytest**

Run: `uv run pytest scripts/tests/test_preacher_localization.py -v`

Expected: every test PASS, including `test_creatures_xml_has_no_unbalanced_color_warning`.

If `test_creatures_xml_has_no_unbalanced_color_warning` still reports a warning, the line number printed in the warning message is the next imbalance somewhere unrelated in the file — that is a separate issue, not this PR's scope. Investigate and stop; do not silently widen the scope.

---

### Task 4: Remove the obsolete baseline entry

**Files:**
- Modify: `scripts/validate_xml_warning_baseline.json:548-551`

- [ ] **Step 1: Remove the entry**

Delete the object:

```json
    {
      "path": "Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml",
      "warning": "Unbalanced color code at line 5574"
    },
```

The trailing comma on the previous entry must remain consistent with the surrounding JSON formatting; verify the file still parses.

- [ ] **Step 2: Verify baseline JSON is still valid**

Run: `python3.12 -c "import json,pathlib; json.loads(pathlib.Path('scripts/validate_xml_warning_baseline.json').read_text())"`

Expected: no output, exit 0.

- [ ] **Step 3: Run the strict validator**

Run: `python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json`

Expected: exit code 0. No new warnings surfaced. (The strict mode fails on warnings not in the baseline; with our fix the warning no longer occurs, and removing it from the baseline keeps the baseline accurate.)

- [ ] **Step 4: Run the full pytest module**

Run: `uv run pytest scripts/tests/test_preacher_localization.py -v`

Expected: all tests PASS.

- [ ] **Step 5: Run encoding check**

Run: `python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts`

Expected: `Scanned N files: N OK, 0 issue(s)`.

- [ ] **Step 6: Run ruff**

Run: `ruff check scripts/`

Expected: `All checks passed!` (or empty output).

- [ ] **Step 7: Smoke-build the DLL**

Run: `dotnet build Mods/QudJP/Assemblies/QudJP.csproj`

Expected: `Build succeeded`. (No C# changed, but we verify nothing in the build pipeline depends on the touched XML in a fragile way.)

- [ ] **Step 8: Stop here**

Do not commit. The user reviews the diff and decides when to commit. The follow-up flow is `/codex` review → `/simplify` → PR creation, per the user's stated workflow.

---

## Verification summary

After Task 4:

| Check | Command | Expectation |
| --- | --- | --- |
| Pytest contract | `uv run pytest scripts/tests/test_preacher_localization.py -v` | all green |
| Strict XML validation | `python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json` | exit 0 |
| Encoding | `python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts` | clean |
| Ruff | `ruff check scripts/` | clean |
| DLL build | `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` | succeeds |

## Out-of-scope reminders

- Do not touch `Preacher.cs` defaults.
- Do not extend `validate_xml.py`'s color-balance scanner.
- Do not generalize this fix to other Preacher-like patterns elsewhere in the data — file a follow-up issue if any are noticed.
- Do not translate other attributes (`Book`, `inOrder`, `ChatWait`).
