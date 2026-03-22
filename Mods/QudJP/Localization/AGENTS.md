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

## Translation Workflow Gate

Before adding a dictionary entry, check `docs/contract-inventory.json`:

1. **Leaf/MarkupLeaf** → dictionary entry is appropriate
2. **Template/Builder/MessageFrame** → needs translation logic, not a dictionary entry
3. **Route not registered** → investigate upstream first per `docs/logic-required-policy.md`

## Editing Rules

- Use dictionary/XML for true stable leaf strings, fixed labels, and atomic names only.
- Verify target object/conversation IDs match game version `2.0.4`.
- Preserve markup and placeholders exactly:
  - `{{...}}`, `&X`, `^x`, `&&`, `^^` — game color codes
  - `=variable.name=` — runtime variable tokens

## Constraints

- UTF-8 without BOM, LF line endings.
- Do not alter color-code structure or placeholder syntax while translating.
- Treat mojibake as an encoding failure, not source text.
