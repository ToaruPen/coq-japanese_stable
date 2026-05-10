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
just python-check
just python-format
just python-test
just python-test-filter '<pattern>'
just roslyn-build
just roslyn-test
just roslyn-check
just semantic-probe --method Show --owner XRL.UI.Popup
just semantic-probe-check
just semantic-probe-real-smoke
just static-producer-check
just static-producer-preview
just annals-pattern-preview
just text-construction-inventory
just ast-grep-smoke
just ast-grep-check
just render-skill-evals <repo-local-skill> <scenario>
DOTFILES_ROOT=/path/to/dotfiles just render-skill-evals <dotfiles-skill> <scenario>
DOTFILES_ROOT=/path/to/dotfiles just summarize-skill-evals /tmp/skill-eval-results.jsonl
just localization-check
just translation-token-check
just translation-token-baseline
scripts/decompile_game_dll.sh
scripts/decompile_game_dll.sh --list
scripts/decompile_game_dll.sh --all
scripts/diagnose_conversation.sh
just sync-mod
```

- Prefer extending an existing script over creating a parallel tool for the same job.
- Keep error paths explicit and actionable; these scripts support validation and deployment.
- Python compatibility baseline is `3.12+`; the preferred local interpreter is
  pinned by the repo `.python-version`. Run tools through `uv run python` or
  `just` recipes instead of a versioned Python executable.
- Use `just semantic-probe` for ad hoc Roslyn owner checks over decompiled C#.
  Keep it exploratory: promote recurring or artifact-grade surfaces into a
  purpose-built inventory instead of treating the generic probe as a tracked
  source of truth.
- Roslyn tracked artifact recipes are intentionally named `*-tracked`;
  prefer preview recipes for review and validation unless the task explicitly
  owns the generated artifact.
- Repo-local Roslyn wrappers and just recipes build into
  `QUDJP_DOTNET_ARTIFACTS_ROOT` (default: `.artifacts/dotnet`) and execute the
  produced tool DLL so parallel local runs do not share build outputs.
- Skill eval execution is orchestrator-owned: render prompts with
  `just render-skill-evals`, run them in fresh Codex subagents from the parent
  session, append results that match `skill-eval-result.schema.json` and
  manifest scenarios, then summarize with `just summarize-skill-evals`.
- If a task touches Phase F observability or triage docs, treat `docs/RULES.md` as the source of truth and keep this guide aligned to it.

## Annals pattern extraction pipeline (issue #420)

The four-script pipeline at `scripts/extract_annals_patterns.py`,
`scripts/validate_candidate_schema.py`, `scripts/translate_annals_patterns.py`,
and `scripts/merge_annals_patterns.py` extracts, translates, and merges regex /
template pairs from decompiled `XRL.Annals/*.cs` into
`Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json`.

**Tracked artifact update workflow** (see also: design spec at
`docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md`):

Use this workflow only when the task explicitly owns the tracked generated
artifact update. For read-only review or validation, prefer preview recipes such
as `just annals-pattern-preview` and `just static-producer-preview`.

```bash
just annals-pattern-extract-tracked

$EDITOR scripts/_artifacts/annals/candidates_pending.json   # human review

uv run python scripts/validate_candidate_schema.py \
  scripts/_artifacts/annals/candidates_pending.json

uv run python scripts/translate_annals_patterns.py \
  scripts/_artifacts/annals/candidates_pending.json

$EDITOR scripts/_artifacts/annals/candidates_pending.json   # translation review (optional)

uv run python scripts/merge_annals_patterns.py \
  scripts/_artifacts/annals/candidates_pending.json
```

**Prerequisites:** dotnet 10.0.x SDK, `uv` with the repo-pinned Python, Node.js
with `@ast-grep/cli`, `codex` CLI authenticated via `codex login`, decompiled
game source under `~/dev/coq-decompiled_stable/`. Apple Silicon hosts need
Rosetta for the live verification flow.

The `translate` step requires Codex CLI access and is **not** part of CI. The
other three steps are dev-local but can be re-run in CI for QA. The Roslyn
console at `scripts/tools/AnnalsPatternExtractor/` IS built in CI to catch
csproj rot.

Preview recipes are the default for review flows. Use
`just static-producer-regenerate-tracked` or `just annals-pattern-extract-tracked`
only when the task explicitly owns the tracked generated artifact being updated.
