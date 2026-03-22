# Scripts

Python tooling for validation, extraction, sync, and deployment.

## Area Map

- `scripts/*.py` — operational utilities (sync, validation, extraction, diff)
- `scripts/*.sh` — shell tools (decompile, launch)
- `scripts/tests/` — pytest coverage
- `pyproject.toml` — Ruff and pytest config

## Commands

```bash
ruff check scripts/              # Lint
ruff format scripts/             # Format
pytest scripts/tests/            # All tests
pytest scripts/tests/ -k <pat>   # Filtered tests

# Decompile game DLL for source reference
scripts/decompile_game_dll.sh          # Text pipeline classes (37)
scripts/decompile_game_dll.sh --list   # List classes only
scripts/decompile_game_dll.sh --all    # All classes (slow)
```

## Editing Rules

- Prefer extending an existing script over creating a new one for the same tool.
- Keep error paths explicit and actionable — these scripts drive validation and deployment.
- Align validation checks with canonical docs, not ad hoc rules.
- Python baseline: `3.12+`. Typed and documented public interfaces.

## Constraints

- Do not add silent fallbacks that hide invalid asset state.
- Script naming: `verb_noun.py` for top-level tools, `verb_noun.sh` for shell scripts.
