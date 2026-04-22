# Scripts

## Why

This area contains the Python and shell tooling used for validation, extraction, sync, deployment, and runtime diagnostics.

## What

- Main paths:
  - `scripts/*.py` for operational utilities
  - `scripts/*.sh` for shell tooling
  - `scripts/tests/` for pytest coverage
  - `pyproject.toml` for Ruff and pytest configuration
- Operating rules for deployment, Rosetta, logs, runtime evidence, Phase F first-PR boundaries, shared defaults, and required verification commands live in `docs/RULES.md`.

## How

- Main commands:

```bash
ruff check scripts/
ruff format scripts/
uv run pytest scripts/tests/
uv run pytest scripts/tests/ -k <pattern>
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
scripts/decompile_game_dll.sh
scripts/decompile_game_dll.sh --list
scripts/decompile_game_dll.sh --all
scripts/diagnose_conversation.sh
python3.12 scripts/sync_mod.py
```

- Prefer extending an existing script over creating a parallel tool for the same job.
- Keep error paths explicit and actionable; these scripts support validation and deployment.
- Python baseline is `3.12+`, with typed and documented public interfaces.
- If a task touches Phase F observability or triage docs, treat `docs/RULES.md` as the source of truth and keep this guide aligned to it.
