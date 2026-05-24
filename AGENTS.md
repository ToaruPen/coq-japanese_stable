# QudJP

## Why

QudJP is the Japanese localization mod for Caves of Qud `1.0.4`. The repo contains the shipped DLL, localization assets, and the tooling used to validate and deploy them.

## What

- Read the scoped guide for the area you are changing:
  - `Mods/QudJP/Assemblies/AGENTS.md` for C# patches, helpers, and tests
  - `Mods/QudJP/Localization/AGENTS.md` for XML and JSON localization assets
  - `scripts/AGENTS.md` for Python and shell tooling
- Source of truth:
  - behavior: `Mods/QudJP/Assemblies/QudJP.Tests/`
  - layer boundaries: `docs/test-architecture.md`
  - translation ownership and route decision rules: `docs/RULES.md`
  - PR and runtime procedures: `docs/workflows/pr-review.md`,
    `docs/workflows/runtime-evidence.md`
  - runtime evidence: fresh logs under `~/Library/Logs/Freehold Games/CavesOfQud/`
  - Steam Workshop release procedure: `docs/release.md`
    - Codex local workflow shortcut: `~/.codex/skills/ship-steam-workshop/SKILL.md`

## How

- If a stale note conflicts with tests or fresh runtime evidence, follow tests first.
- Use `docs/RULES.md` when deciding where a translation belongs. Use
  `docs/workflows/` when executing PR, runtime-log, sync, or deployment
  procedures.
- For decompiled C# exploration, use structural search with `ast-grep` before
  or alongside `rg` when call shape, argument structure, producer/sink routes,
  wrappers, assignments, or attributes matter. Prefer `just ast-search-cs
  'Popup.Show($$$ARGS)'` for common C# searches; plain `rg` is still fine for
  literal text, symbol names, and file discovery.
- When ad hoc C# exploration depends on type, receiver, overload, alias,
  inheritance, generic owner identity, or Unity/TMP property ownership, promote
  the candidate set to `just semantic-probe ...` and keep `candidate` /
  `unresolved` rows visible as uncertainty, not resolved owner proof.
- Use `just lsp-check` as a selective C# solution-load check, not as a
  default read-time tax. Run it after `.sln`/`.csproj`/reference-stub/dotnet
  tool changes, when editor diagnostics disagree with build output, or before
  relying on language-server feedback for type/overload/receiver decisions.
- The repo-local Codex `PostToolUse` hook in `.codex/hooks.json` is a guardrail
  for that same route: its matcher is intentionally broad, while
  `.codex/hooks/lsp-check-after-tool.sh` does the path/tool filtering. It runs
  `just lsp-check` only after relevant C# write-like tool use, skips plain reads
  and shell-command reads by default, and debounces repeated checks.
- For authoritative C# static inventories or scanner changes, use the
  repo-local Roslyn static analysis skill at
  `.codex/skills/roslyn-static-analysis/SKILL.md`. Purpose-built Roslyn
  inventories are still the source-of-truth path for durable or tracked
  artifacts; runtime evidence is still required for live route proof.
- For unused private/internal QudJP C# declaration candidates, run
  `just unused-code-preview`. Use `just unused-code-gate` when a task needs a
  zero-candidate check, and `just unused-code-check` when changing the scanner
  implementation itself. The unused-code scanner resolves configured external
  metadata references from `COQ_MANAGED_DIR`, the stable reference install, or
  an explicit `managed_dir` recipe argument.
- Prefer `just` recipes for routine validation so local runs match the repo task runner.
  Raw commands below document what the recipes execute.
- Create and use a git worktree for implementation work by default. Use the
  main checkout for coordination, cleanup, and read-only inspection unless the
  user explicitly asks for an in-place edit.
- Before opening or updating a PR that changes `Mods/QudJP/Localization/`, add
  a release-note fragment under `docs/release-notes/unreleased/*.md` and run
  `just release-note-check origin/main HEAD`.
- Python tools should run through `uv run python` or `just` recipes. The repo
  pins the preferred local interpreter with `.python-version`, while
  `pyproject.toml` keeps the compatibility floor at Python `3.12+`.
- Treat ad hoc `Mods/QudJP/manifest.json` version bumps used only for local
  in-game verification as temporary dirty state. Restore the manifest to the
  merged/default-branch value during post-merge cleanup unless the user
  explicitly asks to keep, release, or commit that version bump.
- Core commands:

```bash
just build
just test-l1
just test-l2
just test-l2g
just python-check
just python-test
just localization-check
just translation-token-check
just deploy-mod
just sync-mod
```

- Decompiled game source lives in `~/dev/coq-decompiled_stable/` and must never be committed.
- Do not commit `Assembly-CSharp.dll` or other game binaries.
- Only the built DLL and localization assets ship; `.cs` source files do not.
