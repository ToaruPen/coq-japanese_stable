# QudTest Runtime Harness Design

## Goal

Add a small fixture-driven QudTest harness to the stable QudJP mod so an in-game
wish can run representative runtime text routes and write machine-readable
result artifacts for repository-side inspection.

## Non-Goals

- Do not port the lang-experimental official localization API test suite as-is.
- Keep the game-loaded wish bridge as L3 evidence, but expose a headless runner
  for the common "what final text does this route emit?" check.
- Do not claim visual rendering correctness. Font fallback, clipping, wrapping,
  TMP geometry, and actual screen layout stay in L3 runtime smoke evidence.
- Do not change translation ownership or add new translations beyond fixture
  examples that exercise existing behavior.

## Architecture

The deployed mod gets `Mods/QudJP/QudTest/fixtures/*.json`. The headless CLI
links the same QudJP route helpers and writes inspectable artifacts without
opening the game UI. The game-compiled `Bootstrap.cs` still owns the visible
wish commands (`qudtest`, `qudtest:all`, and focused suite aliases) and
delegates by reflection into the loaded `QudJP.dll`. The shipped DLL owns
fixture loading, route execution, result writing, and summary formatting.

The first runtime routes are intentionally narrow:

- `start-replace`: runs `StartReplaceTranslationPatch.Prefix` against a template.
- `message-log`: runs `MessageLogPatch.Prefix` and strips direct markers.
- `message-queue`: runs `MessageQueueSemanticPipeline.TryTranslateQueuedMessage`
  and strips direct markers.
- `wish-queue`: enters `WishCommandQueueTranslationPatch` owner scope, runs the
  message queue pipeline, then exits the scope.
- `popup-text`: runs `PopupTranslationPatch.TranslatePopupTextForProducerRoute`.

This covers the path shapes most likely to expose stale dummy assumptions:
template replacement, final message-log sink behavior, owner-scoped queued
messages, and popup text translation.

## Artifacts

Headless output defaults to `.artifacts/qudtest`:

- `.artifacts/qudtest/results.json`
- `.artifacts/qudtest/summary.txt`
- `.artifacts/qudtest/runs/<utc-run-id>/results.json`
- `.artifacts/qudtest/runs/<utc-run-id>/summary.txt`

Runtime output goes under `DataManager.LocalPath("QudTest")` in game:

- `QudTest/results.json`
- `QudTest/summary.txt`
- `QudTest/runs/<utc-run-id>/results.json`
- `QudTest/runs/<utc-run-id>/summary.txt`

Repository tooling validates the latest result against fixture expectations,
checks freshness, reports failed cases, and optionally scans `Player.log` for
fatal mod-load/runtime markers.

## Acceptance Criteria

- `qudtest` wish commands compile through `Bootstrap.cs` and delegate into
  `QudJP.dll` without requiring the loaded DLL to be discoverable by
  `WishManager`.
- Fixture schema and result writer have L1 coverage and use the existing
  Newtonsoft-backed runtime JSON loader, not `DataContractJsonSerializer`.
- `scripts/qudtest_inspect.py` has pytest coverage for passing results, missing
  cases, stale artifacts, fixture mismatches, failed cases, Player.log fatal
  markers, and run history listing.
- `just qudtest-headless` runs the same fixture suite without launching the
  game and validates the artifact with `--skip-player-log`.
- `scripts/sync_mod.py` deploys `QudTest/fixtures/*.json` and no arbitrary C#
  source files.
- `just qudtest-results`, `just qudtest-inspect-game`, and
  `just qudtest-history` are available.
- Documentation states that QudTest validates runtime text output, not visual
  rendering.

## Risks

- Wish discovery only sees modules known to the game compiler. The bridge stays
  in `Bootstrap.cs` to avoid relying on `QudJP.dll` wish attributes.
- Headless execution is not game-loaded evidence. Use the in-game wish path
  when the question is mod discovery, deployed file layout, or Player.log
  health.
- Fixture expected values can still fossilize bad behavior. Initial fixtures
  should be representative route contracts, not bulk copies of old L1/L2 cases.
