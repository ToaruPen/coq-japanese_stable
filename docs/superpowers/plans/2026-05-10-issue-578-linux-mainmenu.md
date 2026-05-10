# Issue 578 Linux Main Menu Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Linux-realistic automated coverage and dev-only runtime diagnostics for the issue #578 follow-up where Linux native title-menu Japanese labels render blank.

**Architecture:** Keep production behavior scoped to existing main-menu owner patches. Add a reflection-only observability helper so CI can verify the log payload without Unity runtime, then call it from `MainMenuRowTranslationPatch.Postfix` through `RuntimeDiagnostics.LogVerboseProbe`, which is compiled only in dev builds. Add L2G hook resolution coverage for `MainMenuRowTranslationPatch`.

**Tech Stack:** C# net48/net10 tests, NUnit L2/L2G, Harmony target resolution, QudJP `RuntimeDiagnostics`.

---

### Task 1: Main Menu Row Probe Contract

**Files:**
- Create: `Mods/QudJP/Assemblies/src/Observability/MainMenuRowObservability.cs`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2/MainMenuRowTranslationPatchTests.cs`

- [x] **Step 1: Write the failing test**

Add a test that calls `MainMenuRowObservability.TryBuildStateForTests(row, "postfix", out var logLine)` against a dummy row with `data.Text`, `text.text`, and `text.font.name`, then asserts the log includes `MainMenuRowProbe/postfix`, the translated row text, and font name.

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj -c Release --filter "FullyQualifiedName~MainMenuRowTranslationPatchTests"`

Expected: build failure because `MainMenuRowObservability` does not exist.

- [x] **Step 3: Write minimal implementation**

Create a reflection-only helper that extracts `row.data.Text`, `row.text.text`, and `row.text.font.name` without referencing Unity types, truncates long text, and returns a single log line.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj -c Release --filter "FullyQualifiedName~MainMenuRowTranslationPatchTests"`

Expected: PASS.

### Task 2: Wire Dev-Only Runtime Probe

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/Patches/MainMenuRowTranslationPatch.cs`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/RuntimeDiagnosticsPolicyTests.cs` if a policy test already exists; otherwise add coverage in the existing most relevant policy test.

- [x] **Step 1: Write the failing policy test**

Assert the source for `MainMenuRowTranslationPatch` contains `RuntimeDiagnostics.LogVerboseProbe` and `MainMenuRowObservability.TryBuildState`.

- [x] **Step 2: Run test to verify it fails**

Run: `just test-l1`

Expected: FAIL until the patch emits the dev-only probe.

- [x] **Step 3: Wire the probe**

In `MainMenuRowTranslationPatch.Postfix`, after legacy font application, call `RuntimeDiagnostics.LogVerboseProbe(() => MainMenuRowObservability.TryBuildState(__instance, "postfix", out var logLine) ? logLine : null);`.

- [x] **Step 4: Run tests**

Run: `just test-l1` and the focused MainMenuRow L2 command.

Expected: PASS.

### Task 3: L2G Hook Coverage

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs`

- [x] **Step 1: Add target-resolution expectation**

Add `MainMenuRowTranslationPatch` to the single-target resolution cases with target `MainMenuRow.setData(XRL.UI.Framework.FrameworkDataElement)`.

- [x] **Step 2: Run L2G**

Run: `just test-l2g`

Expected: PASS and future game-signature drift is caught.

### Task 4: Final Verification

**Files:** no new files.

- [x] **Step 1: Run focused and relevant gates**

Run: `just test-l1`, `just test-l2`, `just test-l2g`.

Expected: all pass.

- [x] **Step 2: Inspect diff**

Run: `git diff --stat` and `git diff --check`.

Expected: no whitespace errors and only issue #578 Linux main-menu diagnostics files changed.
