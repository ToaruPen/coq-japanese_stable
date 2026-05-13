# Test Runtime Scalability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce local C# verification time, slow-test growth pressure, and make CI/Python/C# runtime regressions easier to diagnose without deleting test coverage.

**Architecture:** Keep test coverage intact. Remove repeated local build/VSTest startup costs in `justfile`, add CI-visible timing artifacts, cache immutable production message pattern loads by canonical path, batch same-route L2 repository-family examples under one Harmony setup, and document a rule that future owner-family additions should use data/static contracts plus small runtime smoke coverage instead of one Harmony setup per string.

**Tech Stack:** `just`, GitHub Actions, pytest, NUnit, .NET 10, C# static translator helpers.

---

## Task 1: Build-Once Local C# Test Entrypoint

**Files:**
- Modify: `justfile`
- Test: `scripts/tests/test_qudjp_dotnet_test_contracts.py`

- [x] **Step 1: Write the failing contract**

```python
def test_local_csharp_full_suite_builds_test_project_once() -> None:
    """The local all-C# test entrypoint should avoid per-category build and VSTest startup."""
    justfile = "\n" + JUSTFILE.read_text(encoding="utf-8")
    recipe = _recipe_block(justfile, "test-csharp", "python-check")
    check_recipe = _recipe_block(justfile, "check", "pr-check")

    assert "dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj" in recipe
    assert recipe.count("dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj") == 1
    assert recipe.count('dotnet test "$test_dll"') == 1
    assert "TestCategory=" not in recipe

    assert "test-csharp" in check_recipe
    assert "test-l1 test-l2 test-l2g" not in check_recipe
```

- [x] **Step 2: Verify RED**

Run: `uv run pytest scripts/tests/test_qudjp_dotnet_test_contracts.py -q`
Expected: FAIL because `test-csharp` does not exist, then because it still runs per-category filters.

- [x] **Step 3: Implement the recipe**

Add `test-csharp` to build `QudJP.Tests.csproj` once into an isolated artifacts root, then run `dotnet test "$test_dll"` once. Update `check` to depend on `test-csharp`.

- [x] **Step 4: Verify GREEN and measure**

Run: `uv run pytest scripts/tests/test_qudjp_dotnet_test_contracts.py -q`
Expected: PASS.

Run: `/usr/bin/time -p just test-csharp`
Expected: all C# tests pass. Current measured result after cache and batching work: 4297 passed in `real 32.46`.

## Task 2: CI Test Result Visibility

**Files:**
- Modify: `.github/workflows/ci.yml`
- Test: `scripts/tests/test_ci_workflow_contract.py`

- [x] **Step 1: Write the failing contract**

```python
def test_ci_qudjp_test_matrix_uploads_category_test_results() -> None:
    """C# matrix legs should publish per-category TRX results for timing and failure review."""
    workflow = _workflow_text()
    job = _job_block(workflow, "qudjp-dotnet-test", "roslyn-tools")

    assert '--logger "trx;LogFileName=qudjp-${{ matrix.category }}.trx"' in job
    assert "--results-directory TestResults/qudjp-${{ matrix.category }}" in job
    assert "Upload QudJP ${{ matrix.category }} test results" in job
    assert "if: always()" in job
    assert "uses: actions/upload-artifact@v4" in job
    assert "name: qudjp-test-results-${{ matrix.category }}" in job
    assert "path: TestResults/qudjp-${{ matrix.category }}/*.trx" in job
    assert "if-no-files-found: ignore" in job
```

- [x] **Step 2: Verify RED**

Run: `uv run pytest scripts/tests/test_ci_workflow_contract.py -q`
Expected: FAIL because TRX logging/artifact upload is absent.

- [x] **Step 3: Implement workflow visibility**

Add TRX logger and `actions/upload-artifact@v4` for the C# matrix job.

- [x] **Step 4: Verify GREEN**

Run: `uv run pytest scripts/tests/test_ci_workflow_contract.py -q`
Expected: PASS.

## Task 3: Python Slow-Test Visibility

**Files:**
- Modify: `pyproject.toml`

- [x] **Step 1: Expand duration reporting**

Change pytest `addopts` from `--durations=10` to `--durations=20`.

- [x] **Step 2: Verify Python suite**

Run: `just python-test`
Expected: PASS and slowest 20 durations printed. Current measured result: 1086 passed, 1 skipped in 37.16 seconds.

## Task 4: Production Message Pattern Load Reuse

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/Translation/MessagePatternTranslator.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs`

- [x] **Step 1: Write failing test for same-file reuse across resets**

Add this test to `MessagePatternTranslatorTests` near `Translate_LoadsPatternFileOnlyOnce_WhenCalledRepeatedly`:

```csharp
[Test]
public void Translate_ReusesLoadedPatternFile_WhenSamePathIsSelectedAgain()
{
    WritePatternDictionary(("^You hear (.+?)[.!]?$", "あなたは{0}を聞いた"));
    _ = MessagePatternTranslator.Translate("You hear thunder.");

    MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    var translated = string.Empty;
    var output = TestTraceHelper.CaptureTrace(() =>
        translated = MessagePatternTranslator.Translate("You hear rain."));

    Assert.Multiple(() =>
    {
        Assert.That(translated, Is.EqualTo("あなたはrainを聞いた"));
        Assert.That(MessagePatternTranslator.LoadInvocationCount, Is.EqualTo(0));
        Assert.That(output, Does.Not.Contain("loaded 1 pattern(s)"));
    });
}
```

- [x] **Step 2: Verify RED**

Run:

```bash
QUDJP_DOTNET_ARTIFACTS_ROOT=.artifacts/dotnet-pattern-cache-red2 just test-l1
```

Actual: FAIL on `Translate_ReusesLoadedPatternFile_WhenSamePathIsSelectedAgain` with `LoadInvocationCount` equal to `1` after the same file path was selected again.

- [x] **Step 3: Implement canonical-path loaded pattern cache**

In `MessagePatternTranslator.cs`, add:

```csharp
private static readonly ConcurrentDictionary<string, CachedPatternFile> PatternFileCache =
    new ConcurrentDictionary<string, CachedPatternFile>(StringComparer.Ordinal);

private sealed class CachedPatternFile
{
    public CachedPatternFile(List<MessagePatternDefinition> patterns, string summary)
    {
        Patterns = patterns;
        Summary = summary;
    }

    public List<MessagePatternDefinition> Patterns { get; }
    public string Summary { get; }
}
```

Change `LoadPatterns()` so it resolves the full pattern path, then returns the cached `CachedPatternFile` when present. Only increment `loadInvocationCount`, parse JSON, and build the summary inside the cache factory. Keep `SetPatternFileForTests(...)` clearing active state and observability counters so file override switching still resets active state; do not clear `PatternFileCache` there.

- [x] **Step 4: Add explicit cache invalidation for mutable temp files**

Add an internal test-only method:

```csharp
internal static void InvalidatePatternFileCacheForTests(string? filePath)
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        return;
    }

    PatternFileCache.TryRemove(Path.GetFullPath(filePath), out _);
}
```

Call it from `WritePatternDictionary(...)` in `MessagePatternTranslatorTests` and `CombatAndLogMessageQueuePatchTests` immediately after writing the mutable test dictionary file. This keeps temp-file rewrite tests correct while allowing stable repository dictionaries to be reused.

- [x] **Step 5: Verify GREEN and focused runtime**

Run:

```bash
uv run pytest scripts/tests/test_qudjp_dotnet_test_contracts.py -q
just test-l1
just test-l2
```

Actual: PASS. Current measured results: `just test-l1` 2209 passed in `real 36.03`, `just test-l2` 1768 passed in `real 56.80`.

## Task 5: Batch High-Cost L2 Repository-Family Tests

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs`

- [x] **Step 1: Identify highest-impact same-route parameterized tests**

Use the measured hot spot: `CombatAndLogMessageQueuePatchTests` accounts for most L2 runtime. Start with same Harmony owner/sink tests that currently repeat setup through many `[TestCase]` rows, especially:

```text
MessagingEmitMessage_TranslatesStableRepositoryFamilies_WhenPatched
CombatMeleeAttack_TranslatesInventoriedShapes_WithRepositoryPatterns
```

- [x] **Step 2: Convert source/expected rows into explicit case arrays**

Preserve every source/expected pair. Replace repeated `[TestCase]` execution with a single `[Test]` that creates one Harmony instance, patches once, loops through the case list, and asserts each case with a case-specific message.

- [x] **Step 3: Verify focused L2 behavior**

Run a focused L2 command against the changed method names if possible, then run `just test-l2`.

Actual: PASS with fewer NUnit result rows and lower L2 runtime because repeated patch/unpatch setup has been removed.

## Task 6: Scalable Test-Addition Rules

**Files:**
- Modify: `docs/test-architecture.md`
- Modify: `docs/RULES.md`
- Test: `scripts/tests/test_qudjp_dotnet_test_contracts.py`

- [x] **Step 1: Write failing docs contract**

Add `test_route_family_test_guidance_limits_l2_case_growth` to require:

```python
assert "batch them under one Harmony patch setup" in test_architecture
assert "static inventory or data-contract coverage" in test_architecture
assert "one L2 smoke case" in rules
assert "Do not add one new Harmony patch/unpatch test case for every newly claimed string" in rules
```

- [x] **Step 2: Verify RED**

Run: `uv run pytest scripts/tests/test_qudjp_dotnet_test_contracts.py::test_route_family_test_guidance_limits_l2_case_growth -q`
Expected: FAIL before docs are updated.

- [x] **Step 3: Update docs**

Add L2 scalability guidance to `docs/test-architecture.md` and route-family addition guidance to `docs/RULES.md`.

- [x] **Step 4: Verify GREEN**

Run: `uv run pytest scripts/tests/test_qudjp_dotnet_test_contracts.py::test_route_family_test_guidance_limits_l2_case_growth -q`
Expected: PASS.

## Task 7: Polishment, Draft PR, and PR Convergence

**Files:**
- Current diff only.

- [x] **Step 1: Run final deterministic checks**

Run:

```bash
just test-csharp
just python-test
just python-check
git diff --check
```

- [x] **Step 2: Independent review**

Dispatch a fresh code reviewer with the full diff and verification evidence. Fix Critical/Important findings, then re-run relevant checks.

- [x] **Step 3: Cleanup pass**

Use the normal `simplify` route unless the reviewer identifies concrete AI-slop signals. Keep cleanup behavior-preserving and scoped to changed files.

- [ ] **Step 4: Create draft PR**

Commit, push `codex/test-runtime-audit`, and open a draft PR summarizing scope, measurements, checks, review verdict, and remaining follow-up work.

- [ ] **Step 5: Converge PR**

Use `post-pr-convergence` after draft PR creation. Watch GitHub Actions, inspect failures or review comments, fix actionable issues, re-run focused checks locally, push updates, and continue until CI is green and no blocking requested changes remain.
