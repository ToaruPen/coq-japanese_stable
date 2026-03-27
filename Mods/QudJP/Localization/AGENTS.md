# Localization

Shipped localization assets: XML merge files and JSON dictionaries.

## Area Map

- `*.jp.xml` — XML localization overlays (game merge system, `Load="Merge"`)
- `Dictionaries/*.ja.json` — dictionary assets for UI text, messages, display names
- `Dictionaries/README.md` — local dictionary handling notes

## Validation

```bash
xmllint --noout <file>         # XML well-formedness
file <file>                    # Encoding check
hexdump -C <file> | head -1   # BOM check
```

## Source of truth

- Tests and current runtime evidence determine whether a route should use localization assets at all.
- Use `docs/test-architecture.md` for layer boundaries and fresh game logs for current sink/runtime findings.

Do not treat old design notes as authority for adding new asset entries.

## Translation Workflow Gate

Before adding a dictionary entry:

1. Confirm from tests and current runtime evidence that the text is a stable leaf string
2. If the text is dynamic, procedural, or already covered by translator/patch tests, do not add a sink-side asset entry
3. Investigate the upstream producer first when route ownership is unclear

## Editing Rules

- Use dictionary/XML for true stable leaf strings, fixed labels, and atomic names only.
- If tests show a route is observation-only at the sink, do not add a compensating dictionary entry there.
- Verify target object/conversation IDs match game version `2.0.4`.
- Preserve markup and placeholders exactly:
  - `{{...}}`, `&X`, `^x`, `&&`, `^^` — game color codes
  - `=variable.name=` — runtime variable tokens

## Constraints

- UTF-8 without BOM, LF line endings.
- Do not alter color-code structure or placeholder syntax while translating.
- Treat mojibake as an encoding failure, not source text.
