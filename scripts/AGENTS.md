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
just ast-search-cs '<pattern>' [path]
just ast-search-py '<pattern>' [path]
just lsp-check
.codex/hooks/lsp-check-after-tool.sh
just static-producer-check
just static-producer-preview
just annals-pattern-preview
just text-construction-inventory
just unused-code-preview
just unused-code-gate
just unused-code-check
just ast-grep-check
just ast-grep-smoke
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
- Use `just ast-search-cs` / `just ast-search-py` for structural pattern
  search. The older `sg-cs` / `sg-py` recipes remain aliases, but the
  `ast-search-*` names are the preferred agent-facing entrypoints.
- Use `just lsp-check` for repo-local C# language-server solution-load
  diagnostics via the pinned `csharp-ls` dotnet tool. Use it when C# tooling
  configuration changes or editor diagnostics look suspicious; do not treat it
  as a replacement for `dotnet build`, analyzer tests, or runtime-route tests.
- The Codex hook script `.codex/hooks/lsp-check-after-tool.sh` is intentionally
  path- and tool-filtered behind a broad `.codex/hooks.json` `PostToolUse`
  matcher. Keep it silent for irrelevant tool use, skip ordinary read-only C#
  inspection unless `QUDJP_CODEX_LSP_HOOK_ON_READ=1`, skip shell-command reads
  unless `QUDJP_CODEX_LSP_HOOK_ON_EXEC=1`, and preserve the debounce guard
  unless there is concrete evidence that stale LSP feedback is causing missed
  failures.
- Roslyn tracked artifact recipes are intentionally named `*-tracked`;
  prefer preview recipes for review and validation unless the task explicitly
  owns the generated artifact.
- Use `just unused-code-preview` to inspect unused private/internal QudJP C#
  declaration candidates, `just unused-code-gate` when a zero-candidate check is
  required, and `just unused-code-check` for scanner implementation changes.
  The candidate inventory is cleanup evidence, not deletion proof; review
  Harmony, reflection, conditional compilation, and runtime-only ownership
  before removing code. External metadata references come from
  `scripts/unused_code_inventory_config.json` and resolve against
  `COQ_MANAGED_DIR`, the default stable reference install, or the recipe's
  explicit `managed_dir` argument.
- Repo-local Roslyn wrappers build into the shared
  `QUDJP_DOTNET_ARTIFACTS_ROOT` cache (default: `.artifacts/dotnet`) behind
  per-tool locks, then execute the produced tool DLL. Just validation recipes
  that must be parallel-isolated still create run-scoped artifact roots under
  the same parent.
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
