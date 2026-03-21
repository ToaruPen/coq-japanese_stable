# Localization AGENTS.md

## WHY

- This area contains the shipped localization assets: XML merge files and JSON dictionaries used by the mod and game.
- It is the main edit surface for stable translated strings, object names, conversation text, and fixed UI labels.

## WHAT

### Area Map

- `*.jp.xml`
  - XML localization overlays loaded by the game's merge system
- `Dictionaries/*.ja.json`
  - dictionary assets for UI text, message templates, and display-name fragments
- `Dictionaries/README.md`
  - local notes for dictionary handling

### Facts That Matter Here

- XML files are expected to be UTF-8 without BOM and LF line endings.
- Most XML localization uses `Load="Merge"` and depends on exact `Name` matching.
- Blueprint and Conversation IDs must match game version `2.0.4` exactly.
- Game color codes and runtime placeholders are data, not prose:
  - preserve `{{...}}`, `&X`, `^x`, `&&`, `^^`
  - preserve `=variable.name=` exactly
- Dynamic/procedural text is not automatically an asset-only problem.
  - If the text is composed upstream, follow `docs/logic-required-policy.md` before adding broad dictionary keys from logs alone.

## HOW

### Validation Commands

- XML well-formedness: `xmllint --noout <file>`
- Encoding check: `file <file>`
- BOM check: `hexdump -C <file> | head -1`
- Python validation/linting from repo root:
  - `ruff check scripts/`
  - `pytest scripts/tests/`

### Asset Editing Workflow

- Use XML or dictionary assets for true stable leaf strings, fixed labels, and atomic names.
- When a runtime string includes slots, states, quantities, or generated titles, confirm whether it belongs to a feature-specific generator before trying to close it with exact keys.
- When editing XML overlays, verify that the target object/conversation ID exists for the intended game version.

### Area-Specific Constraints

- Do not introduce BOM.
- Do not alter color-code structure or placeholder syntax while translating surrounding text.
- Treat mojibake as an encoding failure, not as source text to edit around.
