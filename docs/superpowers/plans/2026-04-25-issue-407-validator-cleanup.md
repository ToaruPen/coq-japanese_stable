# Issue #407 — Validator False-Positive Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the generic Name/ID duplicate detection in `scripts/validate_xml.py` with a schema-aware allowlist, restrict empty-text detection to the `<text>` tag only, delete the byte-equal `TombExteriorWall_SW` duplicate in `Widgets.jp.xml`, and regenerate the warning baseline so it shrinks from 242 false-positive-heavy entries to 2 actionable real warnings.

**Architecture:** Two narrow refactors of pure functions inside `scripts/validate_xml.py` plus one data deletion. `_find_duplicate_siblings` becomes a tuple-driven allowlist matcher (parent_tag → child_tag → key_attribute). `_find_empty_text_elements` adds an early `if element.tag != "text": continue` guard. The unused helper `_collect_duplicate_values` is removed for cleanliness. The `_format_element_descriptor` ID branch is retained as-is (removing it was out of scope for this cleanup). Tests follow Red→Green TDD.

**Tech Stack:** Python 3.12, `xml.etree.ElementTree`, pytest, ruff. Production code: `scripts/validate_xml.py`. Test code: `scripts/tests/test_validate_xml.py`. Data: `Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml`, `scripts/validate_xml_warning_baseline.json`.

---

## File Structure

**Modified files:**
- `scripts/validate_xml.py` (323 lines) — refactor `_find_duplicate_siblings` (L126-148) to allowlist-based detection; add tag guard to `_find_empty_text_elements` (L150-165); remove unused `_collect_duplicate_values`. Total expected delta: ~−15/+15 lines.
- `scripts/tests/test_validate_xml.py` (155 lines) — add 7 new tests, update 2 existing tests for new semantics. Expected delta: ~+90 lines.
- `Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml` — delete L671-673 (byte-equal duplicate `TombExteriorWall_SW` definition).
- `scripts/validate_xml_warning_baseline.json` (973 lines, 242 entries) — regenerated. Expected output: 2 entries (Conversations.jp.xml:261 `<text/>`, Creatures.jp.xml:5574 unbalanced color).

**No new files.**

---

## Task 1: Add failing tests for new validator semantics (Red)

**Files:**
- Modify: `scripts/tests/test_validate_xml.py`

This task adds 7 new tests and updates 2 existing tests to encode the new allowlist semantics. All new tests must fail initially (Red), proving they actually exercise the new behavior.

- [ ] **Step 1: Update `test_duplicate_id_in_same_parent_reports_warning` to reflect dropped generic ID detection**

The existing test at `scripts/tests/test_validate_xml.py:51-60` asserts that `<root><item ID="A"/><item ID="A"/></root>` triggers a duplicate warning. Under the new allowlist (`(objects, object, Name)` only), generic ID detection is removed. Rewrite the test as a regression that *no warning* fires for arbitrary parent/child/ID combinations:

Replace lines 51-60 with:

```python
def test_generic_duplicate_id_no_longer_reported(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Duplicate sibling IDs under arbitrary parents are no longer flagged.

    The validator dropped generic Name/ID detection in favor of an explicit
    schema allowlist. This regression test pins that behavior so future
    additions are made consciously.
    """
    xml_path = tmp_path / "duplicate_id.xml"
    _write_xml(xml_path, '<root><item ID="A"/><item ID="A"/></root>')

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out
```

- [ ] **Step 2: Verify the existing `test_empty_text_element_reports_warning` still encodes correct behavior**

The existing test at `scripts/tests/test_validate_xml.py:63-72` uses `<root><text>   </text></root>` which targets a `<text>` tag. Under the new logic (`tag == "text"`), this still flags. **No change needed** — but read it once to confirm coverage matches the spec's "behavior preserved" note.

- [ ] **Step 3: Add `test_duplicate_siblings_with_distinguishing_attribute_not_flagged`**

Add at the end of `scripts/tests/test_validate_xml.py`:

```python
def test_duplicate_siblings_with_distinguishing_attribute_not_flagged(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Same Name on siblings under non-allowlisted parents is not flagged.

    Worlds.jp.xml uses ``<zone Name="..." Level="..." x="..." y="..."/>`` where
    the Name repeats but the (Level, x, y) tuple differentiates entries. The
    new schema-aware validator must not flag these.
    """
    xml_path = tmp_path / "worlds.xml"
    _write_xml(
        xml_path,
        (
            '<worlds><world Name="JoppaWorld">'
            '<zone Name="Lair" Level="10" x="0" y="0"/>'
            '<zone Name="Lair" Level="11" x="0" y="0"/>'
            "</world></worlds>"
        ),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out
```

- [ ] **Step 4: Add `test_byte_equal_object_siblings_flagged`**

Add immediately after the previous test:

```python
def test_byte_equal_object_siblings_flagged(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """``<objects>`` parent with same-Name ``<object>`` siblings is flagged.

    This is the regression case for the TombExteriorWall_SW byte-equal
    duplicate that prompted the validator overhaul. Real ObjectBlueprints
    files use root tag ``<objects>`` so ``parent.tag == "objects"`` matches.
    """
    xml_path = tmp_path / "blueprints.xml"
    _write_xml(
        xml_path,
        (
            '<objects>'
            '<object Name="TombExteriorWall_SW" Inherits="Widget" Replace="true"/>'
            '<object Name="TombExteriorWall_SW" Inherits="Widget" Replace="true"/>'
            "</objects>"
        ),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert 'Duplicate sibling Name="TombExteriorWall_SW"' in captured.out
```

- [ ] **Step 5: Add `test_duplicate_conditional_nodes_not_flagged`**

Add immediately after:

```python
def test_duplicate_conditional_nodes_not_flagged(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Conditional ``<node>`` siblings sharing an ID are not flagged.

    Conversations.jp.xml uses the same ID with different ``IfHaveState``
    branches to express conditional dialogue paths. These must remain silent.
    """
    xml_path = tmp_path / "conversations.xml"
    _write_xml(
        xml_path,
        (
            '<conversations><conversation ID="X">'
            '<node ID="Greet" IfHaveState="A"/>'
            '<node ID="Greet" IfHaveState="B"/>'
            "</conversation></conversations>"
        ),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out
```

- [ ] **Step 6: Add `test_repeated_naming_entries_not_flagged`**

Add immediately after:

```python
def test_repeated_naming_entries_not_flagged(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Naming.jp.xml weight-style repetition is not flagged.

    ``<prefix Name="ニ"/>`` may legitimately appear multiple times to
    weight that candidate higher in random selection.
    """
    xml_path = tmp_path / "naming.xml"
    _write_xml(
        xml_path,
        (
            '<naming><prefixes>'
            '<prefix Name="ニ"/>'
            '<prefix Name="ニ"/>'
            "</prefixes></naming>"
        ),
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Duplicate sibling" not in captured.out
```

- [ ] **Step 7: Add `test_empty_text_only_flagged_for_text_tag`**

Add immediately after:

```python
def test_empty_text_only_flagged_for_text_tag(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Whitespace-only body on non-``<text>`` tags is not flagged.

    Inheritance/stub objects like ``<object Inherits="X" Replace="true">\\n  </object>``
    legitimately have whitespace-only bodies. Only ``<text>`` should be
    checked for emptiness.
    """
    xml_path = tmp_path / "stub.xml"
    _write_xml(
        xml_path,
        '<root><object Name="X" Inherits="Widget" Replace="true">\n  </object></root>',
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Empty text" not in captured.out
```

- [ ] **Step 8: Add `test_empty_text_self_closing_flagged`**

Add immediately after:

```python
def test_empty_text_self_closing_flagged(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Self-closing ``<text/>`` is still flagged as empty.

    Behavior parity with the old detector for the genuine empty-translation
    case found in Conversations.jp.xml:261.
    """
    xml_path = tmp_path / "selfclose.xml"
    _write_xml(xml_path, "<root><text/></root>")

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Empty text in element 'text'" in captured.out
```

- [ ] **Step 9: Add `test_empty_object_stub_not_flagged`**

Add immediately after:

```python
def test_empty_object_stub_not_flagged(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    """Empty inheritance-only ``<object>`` is not flagged."""
    xml_path = tmp_path / "objstub.xml"
    _write_xml(
        xml_path,
        '<root><object Inherits="X" Replace="true"></object></root>',
    )

    result = main([str(xml_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert "Empty text" not in captured.out
```

- [ ] **Step 10: Run tests to verify the new ones fail (Red)**

Run: `uv run pytest scripts/tests/test_validate_xml.py -v`

Expected: exactly 6 failures.
- `test_generic_duplicate_id_no_longer_reported` — fails because the validator currently *does* report duplicate IDs.
- `test_byte_equal_object_siblings_flagged` — passes currently (generic Name detector catches it) but will need to keep passing after the refactor.
- `test_duplicate_siblings_with_distinguishing_attribute_not_flagged`, `test_duplicate_conditional_nodes_not_flagged`, `test_repeated_naming_entries_not_flagged` — all fail because generic Name/ID detection currently flags them.
- `test_empty_text_only_flagged_for_text_tag`, `test_empty_object_stub_not_flagged` — fail because the current `_find_empty_text_elements` triggers on any tag with whitespace-only body.
- `test_empty_text_self_closing_flagged` — passes currently (existing self-close branch catches it) and must keep passing.

If a test that should fail passes, re-read its body — the assertion may be inverted. Do not proceed to Task 2 until the expected failures are observed.

- [ ] **Step 11: Commit the failing tests**

```bash
git add scripts/tests/test_validate_xml.py
git commit -m "test: add failing cases for schema-aware XML validator (#407)"
```

---

## Task 2: Refactor `_find_duplicate_siblings` to schema-aware allowlist (Green 1)

**Files:**
- Modify: `scripts/validate_xml.py:126-148`

- [ ] **Step 1: Add the allowlist constant near the top of the module**

Open `scripts/validate_xml.py` and insert after the imports/dataclass section (after line 25, before `_collect_xml_files`). The exact insertion point is the blank line at line 25-26.

```python
DUPLICATE_DETECTION_RULES: tuple[tuple[str, str, str], ...] = (
    ("objects", "object", "Name"),
)
"""Schema-aware duplicate-sibling rules: (parent_tag, child_tag, key_attribute).

Only sibling pairs whose parent tag, child tag, and key attribute all match
one of these tuples are reported as duplicates. This avoids false positives
on schema-legitimate repetition (Naming weights, Conversations conditional
branches, Worlds zone differentiation, etc.).
"""
```

- [ ] **Step 2: Replace `_find_duplicate_siblings` and remove `_collect_duplicate_values`**

Replace `scripts/validate_xml.py:126-148` (the existing `_find_duplicate_siblings` plus `_collect_duplicate_values`) with the allowlist-driven implementation:

```python
def _find_duplicate_siblings(root: ET.Element) -> list[str]:
    warnings: list[str] = []
    for parent in root.iter():
        for parent_tag, child_tag, key_attribute in DUPLICATE_DETECTION_RULES:
            if parent.tag != parent_tag:
                continue
            counts: dict[str, int] = {}
            for child in parent:
                if child.tag != child_tag:
                    continue
                if value := child.attrib.get(key_attribute):
                    counts[value] = counts.get(value, 0) + 1
            warnings.extend(
                f"Duplicate sibling {key_attribute}=\"{value}\" under parent '{parent_tag}'"
                for value, count in sorted(counts.items())
                if count > 1
            )
    return warnings
```

`_collect_duplicate_values` is no longer used and is deleted.

- [ ] **Step 3: Run the duplicate-detection tests**

Run: `uv run pytest scripts/tests/test_validate_xml.py -v -k "duplicate or byte_equal or naming or conditional"`

Expected: all duplicate-related tests pass.
- `test_generic_duplicate_id_no_longer_reported` — PASS (no rule for `(root, item, ID)`)
- `test_byte_equal_object_siblings_flagged` — PASS (matches the `(objects, object, Name)` rule)
- `test_duplicate_siblings_with_distinguishing_attribute_not_flagged` — PASS (no rule for `(world, zone, Name)`)
- `test_duplicate_conditional_nodes_not_flagged` — PASS (no rule for `(conversation, node, ID)`)
- `test_repeated_naming_entries_not_flagged` — PASS (no rule for `(prefixes, prefix, Name)`)

If any fail, re-read the rule tuple and the test XML — the parent/child tags must match exactly.

- [ ] **Step 4: Commit**

```bash
git add scripts/validate_xml.py
git commit -m "refactor: replace generic duplicate detection with schema allowlist (#407)"
```

---

## Task 3: Restrict `_find_empty_text_elements` to the `<text>` tag (Green 2)

**Files:**
- Modify: `scripts/validate_xml.py:150-165`

- [ ] **Step 1: Replace `_find_empty_text_elements`**

Replace `scripts/validate_xml.py:150-165` with:

```python
def _find_empty_text_elements(root: ET.Element) -> list[str]:
    warnings: list[str] = []

    for element in root.iter():
        if element.tag != "text":
            continue
        if len(element) > 0:
            continue
        if element.text is None or element.text.strip() == "":
            warnings.append(f"Empty text in element {_format_element_descriptor(element)}")

    return warnings
```

The function now short-circuits on any tag other than `<text>`, then handles both the `text is None` (self-closing) and whitespace-only cases in one branch.

- [ ] **Step 2: Run the empty-text tests**

Run: `uv run pytest scripts/tests/test_validate_xml.py -v -k "empty"`

Expected: all empty-text tests pass.
- `test_empty_text_element_reports_warning` — PASS (`<text>   </text>`)
- `test_empty_text_only_flagged_for_text_tag` — PASS (whitespace-only `<object>` body ignored)
- `test_empty_text_self_closing_flagged` — PASS (`<text/>` still flagged)
- `test_empty_object_stub_not_flagged` — PASS (empty `<object>` ignored)

- [ ] **Step 3: Run the full validate_xml test suite**

Run: `uv run pytest scripts/tests/test_validate_xml.py -v`

Expected: all 18 tests pass (11 original − 1 deleted + 1 added rewrite + 7 new = 18). If any unrelated test breaks, the refactor regressed something — investigate before proceeding.

- [ ] **Step 4: Commit**

```bash
git add scripts/validate_xml.py
git commit -m "refactor: restrict empty-text detection to <text> tag (#407)"
```

---

## Task 4: Delete the `TombExteriorWall_SW` byte-equal duplicate

**Files:**
- Modify: `Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml:671-673`

- [ ] **Step 1: Verify the duplicate is byte-equal to the original**

Run:

```bash
diff <(sed -n '650,652p' Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml) \
     <(sed -n '671,673p' Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml)
```

Expected: empty output (the two blocks are identical).

If the output is non-empty, STOP — this means the duplicate is not byte-equal and the deletion needs human review. The plan assumed byte-equality based on the spec; mismatched payload would be a new issue.

- [ ] **Step 2: Delete lines 671-673**

Use the Edit tool to delete the duplicate block. Read lines 668-675 first for context, then remove:

```xml
  <object Name="TombExteriorWall_SW" Inherits="Widget" Replace="true">
    <part Name="MapChunkPlacement" Map="preset_tile_chunks/TombExteriorWall_SW.rpm" Width="9" Height="6" />
  </object>
```

The block immediately preceding it ends with `TombExteriorWall_ESE` and the block immediately following it begins with `<object Name="CrematoryStairsDown"`.

- [ ] **Step 3: Verify the file still parses**

Run:

```bash
xmllint --noout Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml
```

Expected: no output (clean parse).

- [ ] **Step 4: Verify the validator no longer flags `TombExteriorWall_SW`**

Run:

```bash
python3.12 scripts/validate_xml.py Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml 2>&1 | grep -c "TombExteriorWall_SW" || echo "0"
```

Expected: `0`.

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/ObjectBlueprints/Widgets.jp.xml
git commit -m "fix: remove byte-equal TombExteriorWall_SW duplicate in Widgets.jp.xml (#407)"
```

---

## Task 5: Regenerate the warning baseline

**Files:**
- Modify: `scripts/validate_xml_warning_baseline.json` (regenerated)

- [ ] **Step 1: Regenerate the baseline**

Run:

```bash
python3.12 scripts/validate_xml.py Mods/QudJP/Localization \
  --write-warning-baseline scripts/validate_xml_warning_baseline.json
```

Expected: stderr message `Warning baseline written to scripts/validate_xml_warning_baseline.json`.

- [ ] **Step 2: Inspect the new baseline**

Run:

```bash
cat scripts/validate_xml_warning_baseline.json
```

Expected output (per Codex prediction):

```json
{
  "version": 1,
  "warnings": [
    {
      "path": "Mods/QudJP/Localization/Conversations.jp.xml",
      "warning": "Empty text in element 'text'"
    },
    {
      "path": "Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml",
      "warning": "Unbalanced color code at line 5574"
    }
  ]
}
```

If the baseline contains *more* than 2 entries, examine each. If they are schema-legitimate patterns we missed (e.g., a new parent/child/key combination not in the allowlist), STOP and consult the spec before extending `DUPLICATE_DETECTION_RULES`. Do not silently re-baseline false positives.

If the baseline contains fewer entries (e.g., 0 or 1), one of the two known real warnings has been resolved by another change — verify by re-reading `Conversations.jp.xml:261` and `Creatures.jp.xml:5574` to confirm the actual file content.

- [ ] **Step 3: Verify strict validation passes against the new baseline**

Run:

```bash
python3.12 scripts/validate_xml.py Mods/QudJP/Localization \
  --strict --warning-baseline scripts/validate_xml_warning_baseline.json
echo "Exit code: $?"
```

Expected: `Exit code: 0` and no `NEW WARNING:` lines on stderr.

- [ ] **Step 4: Commit**

```bash
git add scripts/validate_xml_warning_baseline.json
git commit -m "chore: regenerate XML validator baseline after false-positive cleanup (#407)"
```

---

## Task 6: Full repository verification

**Files:** none modified.

- [ ] **Step 1: Run the full pytest suite**

Run:

```bash
uv run pytest scripts/tests/ -q
```

Expected: all tests pass. Current baseline is 343 tests; after this change, expect 343 + 7 (new) − 0 (the rewritten test replaces an existing one in-place) = 350 tests, all green.

If any unrelated test fails, investigate — it likely depends on the validator's old behavior in a way the spec did not anticipate.

- [ ] **Step 2: Run ruff**

Run:

```bash
ruff check scripts/
```

Expected: `All checks passed!` (or no diagnostics).

If ruff complains about the new module-level constant, the docstring style, or line length, fix the cited location. Do not silence with `noqa` unless the rule is genuinely inapplicable.

- [ ] **Step 3: Run encoding check**

Run:

```bash
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
```

Expected: clean exit (no output or "All files OK" depending on the script's idiom).

- [ ] **Step 4: Run the strict validator one more time as a sanity check**

Run:

```bash
python3.12 scripts/validate_xml.py Mods/QudJP/Localization \
  --strict --warning-baseline scripts/validate_xml_warning_baseline.json
echo "Exit code: $?"
```

Expected: `Exit code: 0`, no `NEW WARNING:` output.

- [ ] **Step 5: Build the C# assembly**

Run:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

Expected: `Build succeeded.` with no errors. Warnings related to the existing codebase are acceptable; new warnings are not.

- [ ] **Step 6: Run the L1 test category**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
```

Expected: 1182 tests pass (the documented L1 baseline). No failures or skips beyond what the existing test suite already produces.

- [ ] **Step 7: Run the L2 test category**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

Expected: 713 tests pass (the documented L2 baseline).

L1/L2 are independent of the Python validator; failures here would indicate accidental damage to the C# side or an XML deletion that broke runtime behavior. The `TombExteriorWall_SW` deletion preserves one definition (L650-652) so runtime references remain intact, but the L1/L2 runs confirm.

- [ ] **Step 8: Final commit if anything was adjusted in this task**

If verification surfaced a fix-forward issue (e.g., a ruff cleanup), commit it:

```bash
git add -p
git commit -m "chore: post-verification cleanup for #407"
```

If nothing needed fixing, skip this step.

---

## Self-Review

**Spec coverage:**
- Spec § "1. `_find_duplicate_siblings` の刷新" → Task 2 ✓
- Spec § "2. `_find_empty_text_elements` の `<text>` タグ限定" → Task 3 ✓
- Spec § "データ修正 — `Widgets.jp.xml`" → Task 4 ✓
- Spec § "Baseline 再生成" → Task 5 ✓
- Spec § "テスト追加" (7 new + 2 updated) → Task 1 ✓
- Spec § "Verification" command list → Task 6 ✓

**Type consistency:** The allowlist tuple shape `(parent_tag, child_tag, key_attribute)` is used identically in Task 2 step 1 (declaration) and step 2 (consumption). No name drift.

**Placeholder scan:** No "TBD"/"TODO"/"add validation" patterns. Every code change shows the actual code. Every command shows expected output.

**One adjustment from spec:** The spec listed test #1 as "test_duplicate_siblings_with_distinguishing_attribute_not_flagged" using a Worlds.jp.xml pattern; this plan keeps that name and pattern. The spec's existing-test note said `test_duplicate_id_in_same_parent_reports_warning` could either be rewritten or repurposed — this plan rewrites it to a regression-style "no longer reported" test, which is the cleaner option since the new logic genuinely drops the behavior the old test asserted.
