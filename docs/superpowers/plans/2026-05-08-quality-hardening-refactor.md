# Quality Hardening Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden known ad-hoc fallback and validation gaps without changing localization behavior.

**Architecture:** Keep changes local to existing patch/bootstrap/validation surfaces. Prefer explicit failure signals over silent partial success, and add focused regression tests before implementation.

**Tech Stack:** C# net10 NUnit tests for assembly behavior, Python pytest for script validation, GitHub Actions YAML for CI gate alignment.

---

## Task 1: Character Status Fallback Safety

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenBindingOwnerPatchTests.cs`
- Modify: `Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenBindingPatch.cs`

- [x] **Step 1: Write the failing test**

Add a test that sets `DummyCharacterStatusScreenBindingTarget.stats = null!`, calls `CharacterStatusScreenBindingPatch.Prefix(target)`, and asserts the prefix returns `true`.

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~StatusScreenBindingOwnerPatchTests"`

Expected: the new test fails because `Prefix` currently returns `false` after `TryPopulateControllers` swallows the exception.

- [x] **Step 3: Write minimal implementation**

Change critical `TryPopulateControllers` to return `bool`; when it fails, log and return `true` from `Prefix` so upstream `UpdateViewFromData` can run.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~StatusScreenBindingOwnerPatchTests"`

Expected: all focused status binding tests pass.

## Task 2: QudJP Initialization Retry Safety

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2/QudJPModTests.cs`
- Modify: `Mods/QudJP/Assemblies/src/QudJPMod.cs`

- [x] **Step 1: Write the failing test**

Add test helpers to reset and inspect `isInitialized`, then add tests that call `QudJPMod.InitializeForTests(...)` and verify failures reset the guard so a later attempt is not locked out. Use `ResetInitializationForTests()` to isolate each test and `IsInitializedForTests()` to assert the guard state.

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~QudJPModTests"`

Expected: compile fails until the test hook exists, or test fails because initialization failure does not reset the guard.

- [x] **Step 3: Write minimal implementation**

Wrap `FontManager.Initialize()` and `ApplyHarmonyPatches()` in `try/catch`; on exception, reset `isInitialized` to `0` and rethrow. Add internal test-only helpers under `#if DEBUG` if needed by tests.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~QudJPModTests"`

Expected: all focused QudJPMod tests pass.

## Task 3: Route Validation Gate

**Files:**
- Modify: `scripts/tests/test_validate_pattern_routes.py`
- Modify: `scripts/validate_pattern_routes.py`
- Modify: `justfile`
- Modify: `.github/workflows/ci.yml`

- [x] **Step 1: Write the failing test**

Change `test_main_reports_invalid_route_and_returns_nonzero` so `message-log` is accepted, and add an integration test that validates `Mods/QudJP/Localization/Dictionaries/messages.ja.json` successfully.

- [x] **Step 2: Run test to verify it fails**

Run: `uv run pytest scripts/tests/test_validate_pattern_routes.py -q`

Expected: tests fail because `message-log`, `description`, and `effect-cripple` are not allowed yet.

- [x] **Step 3: Write minimal implementation**

Add the current repository routes to `ALLOWED_ROUTES`, wire `scripts/validate_pattern_routes.py Mods/QudJP/Localization/Dictionaries/messages.ja.json` into `just localization-check`, and add the same validation to CI localization changes.

- [x] **Step 4: Run test to verify it passes**

Run: `uv run pytest scripts/tests/test_validate_pattern_routes.py -q`

Expected: route tests pass.

## Task 4: Test Hygiene Fixes

**Files:**
- Modify: `scripts/tests/test_artifact_gitignore.py`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsCollisionTests.cs`

- [x] **Step 1: Write the failing tests**

Update artifact gitignore assertions to match the intended policy: `.bak` and `merge_conflicts.json` ignored, `candidates_pending.json` tracked. Update annals collision tests to assert regex compile failures are test failures.

- [x] **Step 2: Run tests to verify they fail**

Run: `uv run pytest scripts/tests/test_artifact_gitignore.py -q`

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~AnnalsPatternsCollisionTests"`

Expected: Python test passes after assertion text update if policy already matches; C# compile/collision test should still pass with valid current patterns.

- [x] **Step 3: Write minimal implementation**

Replace broad `catch { continue; }` with `Assert.Fail` including pattern identifiers. Keep `.gitignore` unchanged unless the updated test exposes a real policy mismatch.

- [x] **Step 4: Run focused tests**

Run both commands from Step 2 again.

Expected: both focused tests pass.

## Final Verification

- [x] Run `just test-l1`
- [x] Run `just python-test-filter 'validate_pattern_routes or artifact_gitignore or translate_corpus_batch'`
- [x] Run `just localization-check`
- [x] Run `just translation-token-check`
- [x] Run `git diff --check`
