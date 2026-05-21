# QudTest Runtime Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an in-game QudTest wish harness that records stable runtime text-route outputs as inspectable artifacts.

**Architecture:** Keep the visible wish command in `Bootstrap.cs`, delegate into `QudJP.dll`, execute a narrow set of existing production route functions, and validate artifacts from Python. The first slice is in-game only; headless reflection execution is deferred.

**Tech Stack:** C# `net48` mod DLL, Newtonsoft JSON via `JsonAssetLoader`, Python 3.12 `argparse`/`json`, `just`, NUnit L1 tests, pytest.

---

## Task 1: Python Artifact Inspector

**Files:**
- Create: `scripts/qudtest_inspect.py`
- Create: `scripts/tests/test_qudtest_inspect.py`

- [x] Write failing pytest cases for matching results, missing cases, stale results, failed cases, wrong language, fatal Player.log marker, and history listing.
- [x] Run `uv run pytest scripts/tests/test_qudtest_inspect.py -q` and verify it fails because the script is missing.
- [x] Implement `scripts/qudtest_inspect.py` with `InspectionInputs`, fixture expectation loading, result validation, optional Player.log check, and `--list-runs`.
- [x] Re-run `uv run pytest scripts/tests/test_qudtest_inspect.py -q` and verify it passes.

## Task 2: C# QudTest Core

**Files:**
- Create: `Mods/QudJP/Assemblies/src/QudTest/QudTestModels.cs`
- Create: `Mods/QudJP/Assemblies/src/QudTest/QudTestFixtureLoader.cs`
- Create: `Mods/QudJP/Assemblies/src/QudTest/QudTestRunner.cs`
- Create: `Mods/QudJP/Assemblies/src/QudTest/QudTestArtifactWriter.cs`
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/QudTestRuntimeHarnessTests.cs`

- [x] Write failing L1 tests for fixture loading, route execution, route suite filtering, and failure result recording.
- [x] Run focused C# tests and verify the new tests fail because QudTest types are missing.
- [x] Implement models, loader, runner, and writer using Newtonsoft-backed `JsonAssetLoader`.
- [x] Re-run `just test-l1` and verify the new tests pass.

## Task 3: Stable Runtime Route Executor

**Files:**
- Create: `Mods/QudJP/Assemblies/src/QudTest/QudTestRouteExecutor.cs`
- Covered by: `Mods/QudJP/Assemblies/QudJP.Tests/L1/QudTestRuntimeHarnessTests.cs`

- [x] Write failing L1 tests for `start-replace`, `message-log`, `message-queue`, `wish-queue`, and `popup-text` routes.
- [x] Run focused C# tests and verify failures are missing executor/types.
- [x] Implement route execution by calling existing production patch helpers and stripping direct markers before comparison.
- [x] Re-run `just test-l1` and verify route tests pass.

## Task 4: Wish Bridge and Deployment

**Files:**
- Modify: `Mods/QudJP/Bootstrap.cs`
- Create: `Mods/QudJP/QudTest/fixtures/runtime-smoke.json`
- Modify: `scripts/sync_mod.py`
- Modify: `scripts/tests/test_sync_mod.py`
- Create: `scripts/tests/test_qudtest_bootstrap_contract.py`

- [x] Write failing Python tests that `Bootstrap.cs` exposes `qudtest` wish commands and sync includes `QudTest/fixtures/*.json`.
- [x] Run focused pytest commands and verify they fail.
- [x] Add a Bootstrap wish bridge that invokes `QudJP.QudTest.QudTestRuntimeEntrypoint.Run(command)` from the already loaded DLL.
- [x] Add the initial fixture documents with representative runtime route cases.
- [x] Add sync include patterns for `QudTest/fixtures/*.json`.
- [x] Re-run focused pytest commands and verify they pass.

## Task 5: Recipes and Docs

**Files:**
- Modify: `justfile`
- Modify: `docs/workflows/runtime-evidence.md`
- Modify: `docs/test-architecture.md`
- Modify: `README.md`
- Modify: `scripts/tests/test_release_justfile_contract.py`

- [x] Write failing tests or dry-run checks for `qudtest-results`, `qudtest-inspect-game`, and `qudtest-history`.
- [x] Add recipes and documentation.
- [x] Run focused Python tests and `just --list | rg qudtest`.

## Task 6: Verification

- [x] Run `just test-l1`.
- [x] Run `uv run pytest scripts/tests/test_qudtest_inspect.py scripts/tests/test_sync_mod.py scripts/tests/test_qudtest_bootstrap_contract.py scripts/tests/test_release_justfile_contract.py -q`.
- [x] Run `just build`.
- [x] Run `git diff --check`.
- [x] Inspect `git diff --stat` and ensure no game binaries or decompiled files are included.
