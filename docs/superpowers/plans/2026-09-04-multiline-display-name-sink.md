# Multiline Display-Name Sink Implementation Plan

> **For agentic workers:** The implementation and independent reviews below are complete. The subsequent user request authorizes the coordinator to commit, push, and create a draft PR after `polishment` and `ai-slop-cleaner`. Do not merge, deploy, publish a release, or alter other worktrees.

**Goal:** Exclude multiline UI blocks from the two display-name fallback branches while preserving single-line names and other sink contracts.

**Architecture:** Add a CR/LF check at each existing display-name branch boundary using the stripped visible string. Retain the rest of `TranslatePreservingColors`, upstream owners, and rendering unchanged.

**Tech Stack:** C# net48 production, net10.0 NUnit tests, existing Harmony integration fixture.

## Task 1: Regression and bounded guard

**Files:**
- Modify `Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs`.
- Extend `Mods/QudJP/Assemblies/QudJP.Tests/L2/UITextSkinTranslationPatchTests.cs`.

- [x] Run baseline `just test-l2` in this worktree; stop if it is not green. Result: 5,080 passed.
- [x] Add regression cases to the existing fixture. Seed its existing scoped adjective/relic dictionaries and assert multiline inputs are unchanged, while single-line controls keep their exact Japanese output. Cover LF, CRLF, lone CR, colored with-clause and mixed sequence paths, and direct-marker removal. Use the real `TranslatePreservingColors`/Prefix, not a mock. For the unchanged-output long-summary case, use a warmed allocation check or existing entry observability to prove the expensive branch is not entered; do not use wall-clock timing as an assertion.

Representative input contract:

```csharp
var source = "ゲームサマリー\nアイゲンライフル with beamsplitter";
var actual = UITextSkinTranslationPatch.TranslatePreservingColors(source, nameof(UITextSkinTranslationPatch));
Assert.That(actual, Is.EqualTo(source));
```

- [x] Build an isolated Release test artifact with analyzers disabled, matching `just test-l2`, and run `dotnet test <artifact> --filter FullyQualifiedName~UITextSkinTranslationPatchTests`. Record the intended RED failures before editing production.
- [x] Add the following predicate to the conditions in both `TryTranslateDisplayNameWithClauseUiText` and `TryTranslateMixedDisplayNameSinkText`, before their classifiers:

```csharp
|| ContainsCharacter(stripped, '\n')
|| ContainsCharacter(stripped, '\r')
```

- [x] Re-run the focused test artifact build/test for GREEN. Run `rg` for the new predicates and confirm they occur only in the intended two branch guards. Self-review scope, marker behavior, and single-line compatibility.

## Coordinator verification

- [x] Review actual diff against the approved design, then obtain independent spec compliance and code-quality review. Both reviewers approved after the lone-CR regression gap was resolved; final quality review had no findings.
- [x] Rebuild the prior disposable benchmark against this worktree and repeat the same synthetic owner/sink scenarios. Compare cumulative managed allocations and keep runtime limitations explicit. Evidence: `.artifacts/multiline-bench/REPORT.md`; with-clause allocation approximately 4.179 GB → 7.97 MB, output unchanged.
- [x] Run `just test-csharp` and analyzer-inclusive `just build`. Run `just ci-dotnet-no-game` if a game dependency boundary changes, or as additional confidence when practical. No dependency boundary changed; no-game was not rerun.
- [x] Verify `git diff --check` and status. Record evidence and remaining live-runtime uncertainty. Keep changes uncommitted in the worktree.

## Verification evidence

- All logs below are under `.artifacts/multiline-sink-fix/` in this worktree.
- `red.log`: the revised allocation regression failed at 11,594,304 B against an 866,500 B budget; the unchanged-output controls already passed before the fix.
- Spec review identified lone-CR coverage as insufficient; the allocation test now covers LF, CRLF, and CR. `mutate-cr.log` fails only lone CR when CR guards are removed. `mutate-with-independent.log` and `mutate-mixed-independent.log` each fail LF when that branch's LF guard is removed. The final production source restores both complete guards.
- `final-green.log`: 141 focused tests passed, none failed/skipped.
- `final-full-tests.log`: 10,811 tests passed, none failed/skipped.
- `final-production-build.log`: analyzer-inclusive `just build` passed with zero warnings/errors. `QudJPOutputPath` pointed to `.artifacts/multiline-production/` to avoid changing the tracked shipped DLL.
- Final performance artifact and all measured samples: `.artifacts/multiline-bench/REPORT.md`.
- The implementation-only phase ended without a game launch, deployment, publishing, commit, saved-data edit, or changes to the earlier inventory-repair worktree.

## Pre-PR review and cleanup

- Hosting remote: `origin`, `git@github.com:ToaruPen/coq-japanese_stable.git`. The topic branch initially had no upstream; the user explicitly confirmed a draft PR to `ToaruPen/coq-japanese_stable` / `main`. GitHub reports `main` as the default branch; the main checkout tracks `origin/main`. The freshly fetched base is `3dc6b6bb4dbb26a179d829107e484e99147c816d`.
- Scope: the two C# files and these task-specific design/plan documents, plus the separately authorized redaction of two secretlint findings in the existing Workshop release report. No changes from the inventory-repair worktree are included.
- Fresh independent code review: APPROVE, no actionable findings. The synthetic helper optimization does not establish that the reporter's Windows death stall is resolved.
- Cleanup path: `ai-slop-cleaner` only; no separate `simplify` phase.
- `RETAIN`: both same-shaped CR/LF guards protect independent entry branches, as shown by individual guard-removal failures. A new shared helper would not remove an ownership boundary or improve this four-line change.
- `RETAIN`: the allocation budget, warm-up calls, and newline variants detect expensive failed work that unchanged-output assertions miss. These are behavior locks, not debug residue.
- `RETAIN`: explicit dictionary setup in separate tests keeps each scenario independent and follows the existing fixture style.
- `REMOVE`: none. No behavior-changing defect was found in the cleanup review; production and test source hashes remained unchanged during that phase.
- Before/after cleanup: 141 executable test cases; 124 `Assert.That` syntax sites and 33 `Assert.Multiple` grouping calls. No tests or assertions were removed or weakened. AST query: `Assert.$METHOD($$$ARGS)` in the scoped test file.
- Current pre-PR evidence is captured under `.artifacts/polishment-multiline/`; it is local verification output, not a shipped artifact.

### Separate build-compatibility correction

The first full `pr-check` exposed CA2249 in the .NET 10 headless project for the four new `IndexOf(char) >= 0` predicates. The net48 production build and analyzer-disabled behavior-test builds did not detect that target-specific rule. The failing headless build is preserved in `headless-build-diagnostic.log`.

This was treated as an in-scope build defect, not surplus cleanup. The final predicates reuse the class's existing `ContainsCharacter` helper, preserving ordinal character membership without a new helper, suppression, dependency, or test change. The updated independent verdict is APPROVE. The full `pr-check` passed: 10,811 C# tests, 10,207 game-DLL-free C# tests, and 1,596 Python tests with one skip. The final focused run passed all 141 cases. Warm synthetic with-clause calls allocated 7,970,248 B each with unchanged output.

### Separately authorized report redaction

The tracked-content secretlint scan found an account name and a local absolute path in `docs/reports/2026-06-11-workshop-v0.5.01.md`, unchanged from `origin/main`. Publication paused under the polishment stop condition. The user explicitly authorized anonymizing these two entries before creating the draft PR. Keep this documentation-only correction separate from the runtime fix; do not weaken secretlint rules or alter the recorded upload outcome.

### PR review follow-up

PR #844 requested explicit CRLF and standalone CR cases for the mixed relic sequence no-op control. Parameterize that existing LF control without changing its dictionary setup or assertion. This adds two executable cases (141 to 143); the 124 `Assert.That` sites and 33 `Assert.Multiple` grouping calls remain intact. The existing allocation regression remains the separate guard against expensive failed parsing. No production code changes are needed for this coverage extension.
