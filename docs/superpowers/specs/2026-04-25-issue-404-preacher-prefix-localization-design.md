# Issue #404 — Preacher Prefix/Frozen localization with explicit Postfix

## Why

`Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml` ships five `<part Name="Preacher" ...>` overrides whose `Prefix` and `Frozen` attributes are still in English and byte-equal to the base game. `validate_xml.py` flags `Prefix="The preacher says, {{W|'"` as `Unbalanced color code at line 5574`; the warning is currently suppressed via `scripts/validate_xml_warning_baseline.json`.

The single-attribute imbalance is a runtime non-issue — `XRL.World.Parts.Preacher.Postfix` defaults to `"'}}"` and closes the span — but two real defects remain:

- The preacher line and frozen-state message render as English to a Japanese player.
- The validator warning hides behind the baseline, so any future regression of the same shape elsewhere will also be silently suppressed.

This spec is the data-side fix. Validator-level paired-attribute awareness is deferred to #407 / #409.

## What

Five `<part Name="Preacher" ...>` entries in `Creatures.jp.xml`:

| Line | Object | Book |
| ---: | --- | --- |
| 5574 | Mechanimist Preacher 1 | Preacher1 |
| 5580 | Mechanimist Preacher 2 | Preacher2 |
| 5586 | Mechanimist Preacher 3 | Preacher3 |
| 5592 | Mechanimist Preacher 4 | Preacher4 |
| 11783 | Eschelstadt II, High Priest of the Stilt | HighSermon |

No other `<part Name="Preacher" ...>` exists under `Mods/QudJP/Localization/`.

### New attribute values

For all five entries, the `Prefix` and `Postfix` are uniform:

```text
Prefix="説教者は言う、{{W|'" Postfix="'}}"
```

`Frozen` is per-entry:

- Mechanimist Preacher 1–4: `Frozen="説教者は氷に閉じ込められ、聞き取れない声でぶつぶつ言っている。"`
- High Priest Eschelstadt: `Frozen="スティルトの大司祭エッシェルシュタット II は氷に閉じ込められ、聞き取れない声でぶつぶつ言っている。"`

`Book`, `inOrder`, `ChatWait` stay unchanged.

### Why explicit `Postfix="'}}"`

`Preacher.cs:31` defaults `Postfix = "'}}"`, but the QudJP override does not currently echo it. Adding `Postfix="'}}"` to each XML entry serves two purposes:

1. The single-attribute color-balance check in `validate_xml.py` sees `{{W|` open and `}}` close in the same logical declaration, so the baseline suppression is no longer needed.
2. It documents the runtime shape inline; future translators do not have to know about the C# default to avoid breaking it.

`Postfix` only appears in the rendered chat line via `Preacher.cs:161`. The hardcoded particle-text path at `Preacher.cs:170` (`"{{W|'" + lineText + "'}}"`) is a separate path and is not affected.

### Baseline

Remove this entry from `scripts/validate_xml_warning_baseline.json`:

```json
{
  "path": "Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml",
  "warning": "Unbalanced color code at line 5574"
}
```

After removal, `validate_xml.py --strict --warning-baseline scripts/validate_xml_warning_baseline.json` must succeed without re-introducing this warning.

## How

### Failing test first

Add `scripts/tests/test_preacher_localization.py`. It owns the post-fix invariants and is the file that goes red first.

Assertions:

1. Parse `Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml`.
2. Collect every `<part Name="Preacher" ...>`.
3. The `Book` set is exactly `{Preacher1, Preacher2, Preacher3, Preacher4, HighSermon}` (catches a future Preacher5 surfacing without a translation).
4. Every `Prefix` ends with `{{W|'` and contains at least one non-ASCII character.
5. Every `Postfix` equals `"'}}"` exactly (i.e. `Postfix="'}}"` in XML).
6. Every `Frozen` contains at least one non-ASCII character.
7. `validate_xml.validate_xml_file(creatures_path)` produces zero `Unbalanced color code` warnings.

The validator-call assertion makes the test independent of the baseline file — the test passes only when the data is genuinely balanced.

### Edit Creatures.jp.xml

Five line-precise edits, each adding `Postfix="'}}"` and translating `Prefix` and `Frozen`. No `Replace="true"` semantics change; no surrounding objects touched.

### Edit baseline

Remove the single warning entry described above. Leave neighboring entries untouched.

### Verification commands

Run in this order; all must pass.

```bash
uv run pytest scripts/tests/test_preacher_localization.py -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
```

The dotnet build / test suite is unaffected because no C# changes ship in this PR; we still run it as a smoke check before the PR.

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

## Out of scope

- B-style Harmony patch that swaps `'...'` for `「...」`. `Preacher.cs:170` hardcodes the apostrophe shape in the particle-text path, so a partial swap creates inconsistent rendering between the message log and the floating particle text. Worth filing as a follow-up issue but not blocking #404.
- Generic `Prefix`/`Postfix` paired-attribute awareness in `validate_xml.py`. That belongs to #407 (false-positive cleanup) and #409 (CI gate). The explicit `Postfix="'}}"` here sidesteps the need for now.
- Translating any non-Preacher fields on these objects. Display names, descriptions, and other strings on these objects are out of scope.
- Any change to `Preacher.cs` defaults. The QudJP DLL is unchanged in this PR.

## Risks

- **Translator drift.** A future contributor who edits these entries without the `Postfix="'}}"` invariant will silently re-introduce the imbalance. Mitigation: the test in step 1 owns the invariant.
- **Extra Preacher entry surfaces in 2.0.4+ data.** A future game patch could add `Mechanimist Preacher 5`. The Book-set assertion will fail loudly so it is treated as an explicit decision rather than silent omission.
- **Translation tone.** `説教者は言う、` is a defensible reading of `The preacher says,`. If the project glossary later prefers `司祭は告げる、` or similar, change in one place; the test only asserts non-ASCII content, not exact wording.
