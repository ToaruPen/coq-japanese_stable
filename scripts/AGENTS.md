# Scripts AGENTS.md

## WHY

- This area contains Python tooling for validation, extraction, diffing, and deployment support for the localization project.
- Script changes affect contributor workflow and asset correctness rather than game runtime patching directly.

## WHAT

### Area Map

- top-level `scripts/*.py`
  - operational utilities such as sync, validation, extraction, and diff helpers
- `scripts/tests/`
  - pytest coverage for the Python tools
- canonical config
  - `pyproject.toml` defines Ruff and pytest behavior for this area

### Facts That Matter Here

- Python baseline is `3.12+`.
- Ruff is the configured linter for this area.
- Tests run with pytest from `scripts/tests/`.
- Public script interfaces in this repo are typed and documented; existing files follow that pattern.

## HOW

### Common Commands

- Lint: `ruff check scripts/`
- Format, if needed by existing workflow: `ruff format scripts/`
- Tests: `pytest scripts/tests/`
- Narrow test run: `pytest scripts/tests/ -k <pattern>`

### Editing Workflow

- Prefer extending an existing script when the behavior belongs to the same operational tool.
- Keep error paths explicit and actionable because these scripts are used for validation and deployment tasks.
- When a script validates localization assets, align its checks with the canonical repo docs rather than duplicating new rules ad hoc.

### Area-Specific Constraints

- Do not add silent fallbacks that hide invalid asset state.
- Keep script names descriptive; this repo uses `verb_noun.py` style names for top-level tools.
