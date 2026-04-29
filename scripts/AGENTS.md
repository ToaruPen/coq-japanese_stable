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
python3.12 scripts/check_glossary_consistency.py Mods/QudJP/Localization
python3.12 scripts/check_translation_tokens.py Mods/QudJP/Localization
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

## Annals pattern extraction pipeline (issue #420)

The four-script pipeline at `scripts/extract_annals_patterns.py`,
`scripts/validate_candidate_schema.py`, `scripts/translate_annals_patterns.py`,
and `scripts/merge_annals_patterns.py` extracts, translates, and merges regex /
template pairs from decompiled `XRL.Annals/*.cs` into
`Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json`.

**Operator workflow** (see also: design spec at
`docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md`):

```bash
python3.12 scripts/extract_annals_patterns.py \
  --source-root ~/dev/coq-decompiled_stable/XRL.Annals \
  --include "Resheph*.cs" \
  --output scripts/_artifacts/annals/candidates_pending.json

$EDITOR scripts/_artifacts/annals/candidates_pending.json   # human review

python3.12 scripts/validate_candidate_schema.py \
  scripts/_artifacts/annals/candidates_pending.json

python3.12 scripts/translate_annals_patterns.py \
  scripts/_artifacts/annals/candidates_pending.json

$EDITOR scripts/_artifacts/annals/candidates_pending.json   # translation review (optional)

python3.12 scripts/merge_annals_patterns.py \
  scripts/_artifacts/annals/candidates_pending.json
```

**Prerequisites:** dotnet 10.0.x SDK, Python 3.12 with `pytest`/`ruff`, Node.js
with `@ast-grep/cli`, `codex` CLI authenticated via `codex login`, decompiled
game source under `~/dev/coq-decompiled_stable/`. Apple Silicon hosts need
Rosetta for the live verification flow.

The `translate` step requires Codex CLI access and is **not** part of CI. The
other three steps are dev-local but can be re-run in CI for QA. The Roslyn
console at `scripts/tools/AnnalsPatternExtractor/` IS built in CI to catch
csproj rot.
