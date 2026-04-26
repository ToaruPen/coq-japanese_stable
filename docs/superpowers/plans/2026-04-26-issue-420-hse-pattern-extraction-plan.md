# Issue #420 PR1: HSE Pattern Extraction Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Translate Caves of Qud's Sultan Resheph history into Japanese by adding a Roslyn-based regex/template extraction pipeline on top of the #408 translation runtime.

**Architecture:** Build-time Python pipeline (`extract`/`validate`/`translate`/`merge`) that drives a repo-local C# Roslyn console (`AnnalsPatternExtractor`) to extract regex/template pairs from decompiled `XRL.Annals/Resheph*.cs` and emit them into a sibling `annals-patterns.ja.json`. Runtime extends `JournalPatternTranslator` to ordered multi-file load. Build-time and runtime are completely decoupled — the shipped DLL contains zero Roslyn code.

**Tech Stack:** C# net10.0 (Roslyn `Microsoft.CodeAnalysis.CSharp`), Python 3.12+ (stdlib + `pytest`), JSON dictionaries, NUnit (existing test project).

**Spec:** `docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md`

---

## File Structure (Decomposition)

### New files

**Build-time C# console (one responsibility per file):**

- `scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj` — net10.0 console project
- `scripts/tools/AnnalsPatternExtractor/Program.cs` — CLI entry, argv, orchestration
- `scripts/tools/AnnalsPatternExtractor/Extractor.cs` — Roslyn visitor; finds `SetEventProperty` calls; classifies slots; resolves same-method locals
- `scripts/tools/AnnalsPatternExtractor/CandidateOutput.cs` — DTO + JSON serialization helpers + `en_template_hash` computation

**Build-time Python pipeline (one script per pipeline stage):**

- `scripts/extract_annals_patterns.py` — wraps `dotnet run`, writes candidate JSON, `--force` with `.bak-` backup
- `scripts/validate_candidate_schema.py` — schema validator, importable as a library
- `scripts/translate_annals_patterns.py` — Codex CLI batch with hash-resume
- `scripts/merge_annals_patterns.py` — collision detection, dictionary emission

**Tests — Python (`scripts/tests/`):**

- `scripts/tests/test_extract_annals_patterns.py` — fixture-driven golden tests
- `scripts/tests/test_validate_candidate_schema.py` — schema check coverage
- `scripts/tests/test_translate_annals_patterns.py` — mocked Codex CLI
- `scripts/tests/test_merge_annals_patterns.py` — collision/clean/empty/malformed paths
- `scripts/tests/test_roslyn_extractor_smoke.py` — `dotnet build` smoke
- `scripts/tests/test_pipeline_roundtrip.py` — extract→validate→merge round-trip
- `scripts/tests/test_artifact_gitignore.py` — `.gitignore` rule presence

**Test fixtures:**

- `scripts/tests/fixtures/annals/simple_concat.cs` — synthetic single-literal + concat shape
- `scripts/tests/fixtures/annals/string_format.cs` — synthetic `string.Format` shape (PR1 emits `needs_manual`)
- `scripts/tests/fixtures/annals/switch_cases.cs` — synthetic switch shape (PR1 emits `needs_manual`)
- `scripts/tests/fixtures/annals/unresolved_variable.cs` — synthetic unresolved-local shape (PR1 emits `needs_manual`)
- `scripts/tests/fixtures/annals/expected_simple_concat.json` — golden JSON for simple_concat
- `scripts/tests/fixtures/annals/expected_string_format.json` — golden JSON
- `scripts/tests/fixtures/annals/expected_switch_cases.json` — golden JSON
- `scripts/tests/fixtures/annals/expected_unresolved_variable.json` — golden JSON

**Tests — L1 (`Mods/QudJP/Assemblies/QudJP.Tests/L1/`):**

- `JournalPatternTranslatorMultiFileTests.cs`
- `AnnalsPatternsMarkupInvariantTests.cs`
- `AnnalsPatternsCollisionTests.cs`
- `AnnalsPatternsAssetReachabilityTests.cs`

**Tests — L2 (`Mods/QudJP/Assemblies/QudJP.Tests/L2/`):**

- `ReshephHistoryTranslationTests.cs`
- `Fixtures/annals-samples.json`

**Dictionary deliverable:**

- `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json`

### Modified files

- `Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs` — multi-file ordered load
- `.gitignore` — add `scripts/_artifacts/annals/`
- `scripts/translation_glossary.txt` — add Resheph proper nouns
- `.github/workflows/ci.yml` — add `AnnalsPatternExtractor` build step
- `scripts/AGENTS.md` — annals pipeline operator workflow
- `Mods/QudJP/Localization/Dictionaries/AGENTS.md` (or `.../README.md` if AGENTS does not exist) — `annals-patterns.ja.json` role
- `CHANGELOG.md` — Unreleased entry

---

## Task Sequence

The tasks are ordered by dependency. Tasks 1–7 produce the codebase; Task 8 runs the pipeline operator-style; Tasks 9–11 finalize tests, docs, and live evidence.

---

### Task 1: Foundation — gitignore, glossary, CI build placeholder

**Why first:** these are independent, low-risk changes that other tasks depend on (the artifact directory needs to be gitignored before scripts write to it; the glossary needs the new proper nouns before translation runs; the CI step pre-registers the Roslyn project so we catch build rot the moment Task 3 introduces it).

**Files:**

- Modify: `.gitignore`
- Modify: `scripts/translation_glossary.txt`
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Read current `.gitignore`**

```bash
cat .gitignore
```

Expected: existing rules but no `scripts/_artifacts/annals/` entry.

- [ ] **Step 2: Add the artifact directory rule**

Append to `.gitignore`:

```
# Issue #420 build-time pipeline artifacts (candidate JSON, conflict reports, .bak files)
scripts/_artifacts/annals/
```

- [ ] **Step 3: Verify `.gitignore` change**

```bash
grep 'scripts/_artifacts/annals/' .gitignore
```

Expected: matches the line just added.

- [ ] **Step 4: Read current glossary**

```bash
cat scripts/translation_glossary.txt
```

Expected: existing entries; check whether `Qud`, `Rebekah`, `Gyre`, `Tomb of the Eaters`, `Omonporch`, `Spindle` are already present.

- [ ] **Step 5: Add missing Resheph proper nouns**

Append entries that are missing (skip ones that already exist). Format matches existing `Name = 訳語` lines:

```
Rebekah = レベカ
Gyre = ジャイア
Tomb of the Eaters = 喰らう者の墓
Omonporch = オモンポーチ
Spindle = スピンドル
```

(`Qud = クッド` is already present per spec §3.8.)

- [ ] **Step 6: Read current CI workflow**

```bash
cat .github/workflows/ci.yml
```

Expected: existing build/test/ruff/pytest steps as described in spec §5.6.

- [ ] **Step 7: Add the AnnalsPatternExtractor build step**

Insert directly after the existing `Build QudJP.Tests` step in `.github/workflows/ci.yml`:

```yaml
      - name: Build AnnalsPatternExtractor
        if: hashFiles('scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj') != ''
        run: dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj --configuration Release
```

The `hashFiles` guard means this is a no-op until Task 3 lands the csproj. That intentional ordering — pre-register the gate now so we catch build rot the moment the project is added.

- [ ] **Step 8: Run existing tooling to confirm no regression**

```bash
ruff check scripts/
```

Expected: PASS (no new Python yet, but glossary edit doesn't affect ruff).

- [ ] **Step 9: Commit**

```bash
git add .gitignore scripts/translation_glossary.txt .github/workflows/ci.yml
git commit -m "$(cat <<'EOF'
chore(420): foundation — gitignore, glossary, CI build gate

- .gitignore: scripts/_artifacts/annals/ for build-time artifacts
- translation_glossary.txt: add Rebekah, Gyre, Tomb of the Eaters,
  Omonporch, Spindle (needed by translate_annals_patterns.py for
  Resheph backstory cohesion)
- ci.yml: pre-register AnnalsPatternExtractor build step so the
  csproj cannot silently rot once Task 3 introduces it

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §3.8, §3.9, §5.6
EOF
)"
```

---

### Task 2: Runtime — JournalPatternTranslator multi-file ordered load

**Why now:** independent of build-time pipeline; lets later tasks rely on multi-file behavior without further coupling.

**Files:**

- Modify: `Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs`
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorMultiFileTests.cs`

**Spec references:** §2 Multi-file load patch, §6.3 Runtime change

- [ ] **Step 1: Read existing JournalPatternTranslator.cs**

Read these lines specifically:
- 29–32 (private static fields including `patternFileOverride`)
- 73–88 (`SetPatternFileForTests`, `ResetForTests`)
- 146–200 (`LoadPatterns`, `ResolvePatternFilePath`)

This grounds you in the existing single-file load contract.

- [ ] **Step 2: Read existing single-file test for the API style**

```bash
cat Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorTests.cs | head -60
```

Note the existing testing convention: `Translate_ThrowsFileNotFoundException_WhenPatternFileMissing` (Subject_Behavior_Condition naming).

- [ ] **Step 3: Write the failing tests for multi-file load**

Create `Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorMultiFileTests.cs`:

```csharp
using System;
using System.IO;
using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class JournalPatternTranslatorMultiFileTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-multifile-l1",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string WritePatternFile(string fileName, string contents)
    {
        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, contents, Utf8WithoutBom);
        return path;
    }

    private static string PatternFileBody(params (string Pattern, string Template)[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("{\"entries\":[],\"patterns\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (pattern, template) = entries[i];
            sb.Append("{\"pattern\":\"").Append(pattern.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\",\"template\":\"").Append(template.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\"}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    [Test]
    public void Translate_FirstFilePatternWins_WhenSameInputMatchesBoth()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello world$", "First template")));
        var second = WritePatternFile("second.json",
            PatternFileBody(("^Hello world$", "Second template")));

        JournalPatternTranslator.SetPatternFilesForTests(first, second);

        Assert.That(JournalPatternTranslator.Translate("Hello world"), Is.EqualTo("First template"));
    }

    [Test]
    public void Translate_SecondFilePatternMatches_WhenFirstFileDoesNotMatch()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Apple$", "りんご")));
        var second = WritePatternFile("second.json",
            PatternFileBody(("^Banana$", "バナナ")));

        JournalPatternTranslator.SetPatternFilesForTests(first, second);

        Assert.That(JournalPatternTranslator.Translate("Banana"), Is.EqualTo("バナナ"));
    }

    [Test]
    public void SetPatternFilesForTests_NullArray_ResetsToDefaults()
    {
        // Set non-default first, then null should reset to default loader.
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Override$", "OverrideOK")));
        JournalPatternTranslator.SetPatternFilesForTests(first);
        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("OverrideOK"));

        JournalPatternTranslator.SetPatternFilesForTests((string[]?)null);

        // After null reset, default journal-patterns.ja.json is in effect again.
        // The default file path is real-asset-resolved; we can confirm reset by checking that
        // "Override" no longer translates (default dict has no such pattern).
        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("Override"));
    }

    [Test]
    public void SetPatternFilesForTests_EmptyArray_ResetsToDefaults()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Override$", "OverrideOK")));
        JournalPatternTranslator.SetPatternFilesForTests(first);
        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("OverrideOK"));

        JournalPatternTranslator.SetPatternFilesForTests(Array.Empty<string>());

        // Empty list is treated identically to null (per spec §2 SetPatternFilesForTests).
        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("Override"));
    }

    [Test]
    public void SetPatternFileForTests_LegacyApi_NullStillResetsToDefaults()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Legacy$", "LegacyOK")));
        JournalPatternTranslator.SetPatternFileForTests(first);
        Assert.That(JournalPatternTranslator.Translate("Legacy"), Is.EqualTo("LegacyOK"));

        JournalPatternTranslator.SetPatternFileForTests(null);
        Assert.That(JournalPatternTranslator.Translate("Legacy"), Is.EqualTo("Legacy"));
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenFirstFileMissing()
    {
        var second = WritePatternFile("second.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var ghost = Path.Combine(tempDirectory, "does-not-exist.json");

        JournalPatternTranslator.SetPatternFilesForTests(ghost, second);

        Assert.Throws<FileNotFoundException>(() => JournalPatternTranslator.Translate("Hello"));
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenSecondFileMissing()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var ghost = Path.Combine(tempDirectory, "does-not-exist.json");

        JournalPatternTranslator.SetPatternFilesForTests(first, ghost);

        Assert.Throws<FileNotFoundException>(() => JournalPatternTranslator.Translate("Hello"));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenSecondFileMalformedJson()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var malformed = Path.Combine(tempDirectory, "malformed.json");
        File.WriteAllText(malformed, "{ this is not valid json", Utf8WithoutBom);

        JournalPatternTranslator.SetPatternFilesForTests(first, malformed);

        Assert.Throws<InvalidDataException>(() => JournalPatternTranslator.Translate("Hello"));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenSecondFileHasNoPatternsArray()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var noPatterns = Path.Combine(tempDirectory, "no-patterns.json");
        File.WriteAllText(noPatterns, "{\"entries\":[]}", Utf8WithoutBom);

        JournalPatternTranslator.SetPatternFilesForTests(first, noPatterns);

        Assert.Throws<InvalidDataException>(() => JournalPatternTranslator.Translate("Hello"));
    }
}
```

- [ ] **Step 4: Confirm the test fails to compile or fails at runtime**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~JournalPatternTranslatorMultiFileTests
```

Expected: COMPILE FAIL — `JournalPatternTranslator.SetPatternFilesForTests` does not exist.

- [ ] **Step 5: Apply the runtime change to JournalPatternTranslator.cs**

Open `Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs`.

Replace the field declaration block at line ~29–32:

```csharp
    private static List<JournalPatternDefinition>? loadedPatterns;
    private static string? patternFileOverride;
    private static string patternLoadSummary = "JournalPatternTranslator: pattern load summary unavailable.";
```

…with the new multi-file shape:

```csharp
    private static List<JournalPatternDefinition>? loadedPatterns;
    private static string[]? patternFileOverrides;
    private static string patternLoadSummary = "JournalPatternTranslator: pattern load summary unavailable.";

    internal static readonly string[] DefaultPatternAssetPaths =
    {
        "Dictionaries/journal-patterns.ja.json",
        "Dictionaries/annals-patterns.ja.json",
    };
```

Replace `SetPatternFileForTests` and `ResetForTests` (the existing 73–90 block) with:

```csharp
    internal static void SetPatternFilesForTests(params string[]? filePaths)
    {
        lock (SyncRoot)
        {
            // Null OR empty array resets to defaults; non-empty array overrides.
            patternFileOverrides = (filePaths is null || filePaths.Length == 0) ? null : filePaths;
            loadedPatterns = null;
            RegexCache.Clear();
            MissingPatternCounts.Clear();
            MissingRouteCounts.Clear();
            patternLoadSummary = "JournalPatternTranslator: pattern load summary unavailable.";
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    internal static void SetPatternFileForTests(string? filePath)
    {
        SetPatternFilesForTests(filePath is null ? null : new[] { filePath });
    }

    internal static void ResetForTests()
    {
        SetPatternFilesForTests((string[]?)null);
    }
```

Replace `LoadPatterns` (the existing 146–200 block) with the multi-file version. Open the file and study the existing single-file body first; the new version applies the existing per-file parsing as a loop:

```csharp
    private static List<JournalPatternDefinition> LoadPatterns()
    {
        Interlocked.Increment(ref loadInvocationCount);

        var paths = ResolvePatternFilePaths();
        var allDefinitions = new List<JournalPatternDefinition>();
        var summaries = new List<string>(paths.Count);
        var totalDuplicates = 0;
        var distinctDuplicates = new Dictionary<string, int>(StringComparer.Ordinal);
        var seenPatternsAcrossFiles = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var fileIndex = 0; fileIndex < paths.Count; fileIndex++)
        {
            var patternFilePath = paths[fileIndex];
            if (!File.Exists(patternFilePath))
            {
                throw new FileNotFoundException(
                    $"QudJP: journal pattern dictionary file not found: {patternFilePath}",
                    patternFilePath);
            }

            JournalPatternDocument? document;
            try
            {
                using var stream = File.OpenRead(patternFilePath);
                var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocument));
                document = serializer.ReadObject(stream) as JournalPatternDocument;
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                throw new InvalidDataException(
                    $"QudJP: malformed JSON in pattern file '{patternFilePath}': {ex.Message}", ex);
            }

            if (document?.Patterns is null)
            {
                throw new InvalidDataException(
                    $"QudJP: journal pattern file has no patterns array: {patternFilePath}");
            }

            var fileDuplicateCount = 0;
            for (var index = 0; index < document.Patterns.Count; index++)
            {
                var patternEntry = document.Patterns[index];
                var pattern = patternEntry?.Pattern;
                var template = patternEntry?.Template;
                if (pattern is null || pattern.Length == 0 || template is null)
                {
                    throw new InvalidDataException(
                        $"QudJP: malformed journal pattern entry at index {index} in '{patternFilePath}'.");
                }

                _ = GetCompiledRegex(pattern);
                if (seenPatternsAcrossFiles.ContainsKey(pattern))
                {
                    fileDuplicateCount++;
                    totalDuplicates++;
                    distinctDuplicates[pattern] = distinctDuplicates.TryGetValue(pattern, out var dc) ? dc + 1 : 1;
                    // First-match-wins: skip adding the duplicate; the earlier definition prevails.
                    continue;
                }

                seenPatternsAcrossFiles[pattern] = allDefinitions.Count;
                allDefinitions.Add(new JournalPatternDefinition(pattern, template));
            }

            summaries.Add($"{document.Patterns.Count} pattern(s) from '{patternFilePath}' ({fileDuplicateCount} duplicate(s) shadowed)");
        }

        patternLoadSummary =
            $"JournalPatternTranslator: loaded {allDefinitions.Count} unique pattern(s) across {paths.Count} file(s); " +
            $"{totalDuplicates} duplicate(s) across {distinctDuplicates.Count} distinct pattern(s) shadowed by earlier files. " +
            string.Join("; ", summaries);
        LogObservability($"[QudJP] {patternLoadSummary}");
        LogDuplicatePatternSummary(distinctDuplicates);

        return allDefinitions;
    }
```

Replace `ResolvePatternFilePath` (the existing 203–211 block) with `ResolvePatternFilePaths` (note pluralized):

```csharp
    private static IReadOnlyList<string> ResolvePatternFilePaths()
    {
        var overrides = patternFileOverrides;
        if (overrides is { Length: > 0 })
        {
            var resolved = new string[overrides.Length];
            for (var i = 0; i < overrides.Length; i++)
            {
                resolved[i] = Path.GetFullPath(overrides[i]);
            }
            return resolved;
        }

        var defaults = new string[DefaultPatternAssetPaths.Length];
        for (var i = 0; i < DefaultPatternAssetPaths.Length; i++)
        {
            defaults[i] = LocalizationAssetResolver.GetLocalizationPath(DefaultPatternAssetPaths[i]);
        }
        return defaults;
    }
```

Search the rest of the file for any remaining `patternFileOverride` (singular) references and replace them with `patternFileOverrides` plural where logically equivalent, or remove if dead code.

- [ ] **Step 6: Build and verify the runtime compiles**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

Expected: BUILD SUCCEEDED. If there are compile errors about `patternFileOverride`, search the file and finish the rename.

- [ ] **Step 7: Build the test project**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 8: Run the new tests**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --no-build --filter FullyQualifiedName~JournalPatternTranslatorMultiFileTests
```

Expected: 9/9 PASS (1 each for the 9 `[Test]` methods written in Step 3). Note: PR1 currently has no `Dictionaries/annals-patterns.ja.json`, so the default-fallback tests rely only on the existing `journal-patterns.ja.json`.

If `SetPatternFilesForTests_NullArray_ResetsToDefaults` fails because the second default path `annals-patterns.ja.json` does not yet exist, that is expected runtime behavior under the contract (hard-fail-on-malformed). The test is structured to NOT translate "Override" through the default loader — it just asserts the original test override is not still in effect. **However** if the assertion accidentally reaches `Translate()` and triggers the `LoadPatterns` flow, it WILL fail because annals-patterns.ja.json is missing. To make Task 2 fully testable in isolation, the multi-file load must tolerate the second default file being missing for the lifetime of PR1 development. **Update the multi-file `LoadPatterns` to make absence of a DEFAULT (non-override) file a soft warning that skips that file, while OVERRIDE missing files remain hard fail.** Replace the `if (!File.Exists...)` block in `LoadPatterns` with:

```csharp
            if (!File.Exists(patternFilePath))
            {
                // Default-asset paths may be missing during incremental rollout (e.g. annals-patterns.ja.json
                // before Task 8 lands). For test overrides, missing is always a hard fail.
                if (patternFileOverrides is null)
                {
                    LogObservability($"[QudJP] JournalPatternTranslator: default pattern file not present, skipping: {patternFilePath}");
                    continue;
                }
                throw new FileNotFoundException(
                    $"QudJP: journal pattern dictionary file not found: {patternFilePath}",
                    patternFilePath);
            }
```

Re-run the test command from this step. Expected: 9/9 PASS.

- [ ] **Step 9: Run the full L1 suite to confirm no regression**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --no-build --filter TestCategory=L1
```

Expected: ALL PASS (the existing `JournalPatternTranslatorTests` continues to use the legacy single-file API which is now a wrapper).

- [ ] **Step 10: Commit**

```bash
git add Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs Mods/QudJP/Assemblies/QudJP.Tests/L1/JournalPatternTranslatorMultiFileTests.cs
git commit -m "$(cat <<'EOF'
feat(420): JournalPatternTranslator multi-file ordered load

Add SetPatternFilesForTests(params string[]) and a default ordered list
[journal-patterns.ja.json, annals-patterns.ja.json]. First-match-wins
semantics for cross-file duplicate patterns. Existing
SetPatternFileForTests(string?) becomes a wrapper preserving null-resets-
to-default semantics. Default-asset absence soft-skips with a log
message; test overrides remain hard-fail.

L1 covers ordered match, null/empty reset, missing-file paths (override
hard-fail), malformed JSON, missing patterns array, legacy null reset.

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §2, §6.3
EOF
)"
```

---

### Task 3: AnnalsPatternExtractor — C# Roslyn console

**Why now:** the foundation gitignore + CI build step from Task 1 are in place. The Python pipeline (Tasks 4–7) needs this C# tool to invoke. Multi-file load (Task 2) is unrelated and can land in either order.

**Files:**

- Create: `scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj`
- Create: `scripts/tools/AnnalsPatternExtractor/Program.cs`
- Create: `scripts/tools/AnnalsPatternExtractor/Extractor.cs`
- Create: `scripts/tools/AnnalsPatternExtractor/CandidateOutput.cs`
- Create: `scripts/tests/fixtures/annals/simple_concat.cs`
- Create: `scripts/tests/fixtures/annals/string_format.cs`
- Create: `scripts/tests/fixtures/annals/switch_cases.cs`
- Create: `scripts/tests/fixtures/annals/unresolved_variable.cs`
- Create: `scripts/tests/fixtures/annals/expected_simple_concat.json`
- Create: `scripts/tests/fixtures/annals/expected_string_format.json`
- Create: `scripts/tests/fixtures/annals/expected_switch_cases.json`
- Create: `scripts/tests/fixtures/annals/expected_unresolved_variable.json`
- Create: `scripts/tests/test_roslyn_extractor_smoke.py`
- Create: `scripts/tests/test_extract_annals_patterns.py` (the C# tool's golden tests; Python wrapper tests are added in Task 4)

**Spec references:** §3.1, §3.6, §5.2

- [ ] **Step 1: Read the existing Roslyn precedent in the repo**

```bash
cat Mods/QudJP/Assemblies/QudJP.Analyzers/QudJP.Analyzers.csproj
```

Note the `Microsoft.CodeAnalysis.CSharp` package version used. Use the same version for consistency.

- [ ] **Step 2: Verify the directory does not exist yet**

```bash
ls scripts/tools 2>&1
```

Expected: `No such file or directory` (or empty if it exists).

- [ ] **Step 3: Create the csproj**

Create `scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>QudJP.Tools.AnnalsPatternExtractor</RootNamespace>
    <AssemblyName>AnnalsPatternExtractor</AssemblyName>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
  </ItemGroup>
</Project>
```

(Use the same version `Microsoft.CodeAnalysis.CSharp` as `QudJP.Analyzers.csproj` reads in Step 1; if that file pins a different version, mirror it here.)

- [ ] **Step 4: Verify the project restores and builds (no code yet, will fail)**

```bash
dotnet restore scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj
dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj
```

Expected: BUILD FAIL (`Program` not found / no entry point). This confirms the csproj is wired up correctly.

- [ ] **Step 5: Write the Roslyn smoke test (TDD)**

Create `scripts/tests/test_roslyn_extractor_smoke.py`:

```python
"""Smoke test: AnnalsPatternExtractor csproj builds in Release."""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

import pytest

PROJECT_PATH = Path("scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj")


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_extractor_csproj_builds_in_release() -> None:
    """The Roslyn extractor csproj must build cleanly so the CI step does not rot."""
    result = subprocess.run(
        ["dotnet", "build", str(PROJECT_PATH), "--configuration", "Release"],
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, (
        f"dotnet build failed (exit {result.returncode}).\n"
        f"stdout:\n{result.stdout}\n"
        f"stderr:\n{result.stderr}"
    )
```

- [ ] **Step 6: Run the smoke test (will fail until Steps 7-9 land)**

```bash
pytest scripts/tests/test_roslyn_extractor_smoke.py -v
```

Expected: FAIL (no `Program` entry point yet).

- [ ] **Step 7: Create CandidateOutput.cs**

Create `scripts/tools/AnnalsPatternExtractor/CandidateOutput.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QudJP.Tools.AnnalsPatternExtractor;

internal sealed class SlotEntry
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("raw")]
    public string Raw { get; set; } = "";

    [JsonPropertyName("default")]
    public string Default { get; set; } = "";
}

internal sealed class CandidateEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("source_file")]
    public string SourceFile { get; set; } = "";

    [JsonPropertyName("annal_class")]
    public string AnnalClass { get; set; } = "";

    [JsonPropertyName("switch_case")]
    public string? SwitchCase { get; set; }

    [JsonPropertyName("event_property")]
    public string EventProperty { get; set; } = "";

    [JsonPropertyName("sample_source")]
    public string SampleSource { get; set; } = "";

    [JsonPropertyName("extracted_pattern")]
    public string ExtractedPattern { get; set; } = "";

    [JsonPropertyName("slots")]
    public List<SlotEntry> Slots { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("ja_template")]
    public string JaTemplate { get; set; } = "";

    [JsonPropertyName("review_notes")]
    public string ReviewNotes { get; set; } = "";

    [JsonPropertyName("route")]
    public string Route { get; set; } = "annals";

    [JsonPropertyName("en_template_hash")]
    public string EnTemplateHash { get; set; } = "";
}

internal sealed class CandidateDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1";

    [JsonPropertyName("candidates")]
    public List<CandidateEntry> Candidates { get; set; } = new();
}

internal static class HashHelper
{
    public static string ComputeEnTemplateHash(CandidateEntry candidate)
    {
        // canonical_json over a fixed-shape payload: pattern, slots, sample_source, event_property, switch_case
        var payload = new SortedDictionary<string, object?>(System.StringComparer.Ordinal)
        {
            ["extracted_pattern"] = candidate.ExtractedPattern,
            ["slots"] = candidate.Slots,
            ["sample_source"] = candidate.SampleSource,
            ["event_property"] = candidate.EventProperty,
            ["switch_case"] = candidate.SwitchCase,
        };
        var canonical = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var sb = new StringBuilder("sha256:");
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

internal static class CandidateWriter
{
    public static void WriteToFile(string path, CandidateDocument document)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var json = JsonSerializer.Serialize(document, options);
        File.WriteAllText(path, json + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
```

- [ ] **Step 8: Create Extractor.cs**

Create `scripts/tools/AnnalsPatternExtractor/Extractor.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QudJP.Tools.AnnalsPatternExtractor;

internal sealed class Extractor
{
    private readonly List<CandidateEntry> candidates = new();
    private readonly List<string> diagnostics = new();

    public IReadOnlyList<CandidateEntry> Candidates => candidates;
    public IReadOnlyList<string> Diagnostics => diagnostics;

    public void ProcessFile(string sourcePath)
    {
        var sourceText = File.ReadAllText(sourcePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();
        var fileName = Path.GetFileName(sourcePath);
        var className = Path.GetFileNameWithoutExtension(sourcePath);

        var generateMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == "Generate");
        if (generateMethod is null)
        {
            diagnostics.Add($"{fileName}: no Generate() method found, skipping");
            return;
        }

        // Build local-variable initializer table (literal-only resolution within Generate())
        var localInitializers = CollectLiteralLocals(generateMethod);

        // Find SetEventProperty calls
        var setterCalls = generateMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsSetEventPropertyCall)
            .ToList();

        for (var i = 0; i < setterCalls.Count; i++)
        {
            var invocation = setterCalls[i];
            var (eventProperty, valueExpr) = ParseSetterArgs(invocation);
            if (eventProperty is null || valueExpr is null) continue;
            if (eventProperty != "gospel" && eventProperty != "tombInscription") continue;

            var candidateId = $"{className}#default";
            // PR1 does not handle switch/case; if the call is inside a SwitchSectionSyntax, mark needs_manual.
            var switchSection = invocation.Ancestors().OfType<SwitchSectionSyntax>().FirstOrDefault();
            if (switchSection is not null)
            {
                candidates.Add(NeedsManual(
                    id: $"{className}#switch{i}",
                    sourceFile: fileName,
                    annalClass: className,
                    switchCase: ExtractSwitchLabel(switchSection),
                    eventProperty: eventProperty,
                    reason: "switch/case decomposition is out of scope for PR1 (deferred to #422)"));
                continue;
            }

            // Other unsupported shapes also degrade.
            if (IsStringFormatCall(valueExpr))
            {
                candidates.Add(NeedsManual(
                    id: candidateId,
                    sourceFile: fileName,
                    annalClass: className,
                    switchCase: "default",
                    eventProperty: eventProperty,
                    reason: "string.Format(...) extraction is out of scope for PR1 (deferred to #422)"));
                continue;
            }

            var resolution = ResolveValueExpression(valueExpr, localInitializers);
            if (!resolution.Resolved)
            {
                candidates.Add(NeedsManual(
                    id: candidateId,
                    sourceFile: fileName,
                    annalClass: className,
                    switchCase: "default",
                    eventProperty: eventProperty,
                    reason: resolution.Reason));
                continue;
            }

            var candidate = BuildCandidate(
                id: candidateId,
                sourceFile: fileName,
                annalClass: className,
                switchCase: "default",
                eventProperty: eventProperty,
                resolved: resolution);

            candidates.Add(candidate);
        }
    }

    private static bool IsSetEventPropertyCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is IdentifierNameSyntax id) return id.Identifier.ValueText == "SetEventProperty";
        if (invocation.Expression is MemberAccessExpressionSyntax m) return m.Name.Identifier.ValueText == "SetEventProperty";
        return false;
    }

    private static (string? property, ExpressionSyntax? value) ParseSetterArgs(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count < 2) return (null, null);
        if (invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax keyLiteral) return (null, null);
        if (!keyLiteral.IsKind(SyntaxKind.StringLiteralExpression)) return (null, null);
        return (keyLiteral.Token.ValueText, invocation.ArgumentList.Arguments[1].Expression);
    }

    private static bool IsStringFormatCall(ExpressionSyntax expr)
    {
        if (expr is not InvocationExpressionSyntax invoc) return false;
        if (invoc.Expression is MemberAccessExpressionSyntax m
            && m.Expression is IdentifierNameSyntax type
            && type.Identifier.ValueText == "string"
            && m.Name.Identifier.ValueText == "Format")
        {
            return true;
        }
        return false;
    }

    private static string? ExtractSwitchLabel(SwitchSectionSyntax section)
    {
        var label = section.Labels.FirstOrDefault();
        return label switch
        {
            CaseSwitchLabelSyntax csl => csl.Value.ToString(),
            DefaultSwitchLabelSyntax => "default",
            _ => null,
        };
    }

    private static Dictionary<string, string> CollectLiteralLocals(MethodDeclarationSyntax method)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var declarator in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Initializer?.Value is LiteralExpressionSyntax lit
                && lit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                dict[declarator.Identifier.ValueText] = lit.Token.ValueText;
            }
        }
        return dict;
    }

    private sealed class ResolutionResult
    {
        public bool Resolved { get; init; }
        public string Reason { get; init; } = "";
        public string SampleSource { get; init; } = "";
        public List<SlotEntry> Slots { get; init; } = new();
    }

    private static ResolutionResult ResolveValueExpression(
        ExpressionSyntax valueExpr,
        IReadOnlyDictionary<string, string> localInitializers)
    {
        // Required PR1 shapes:
        //   a) single string literal
        //   b) BinaryExpression (+ concat) of string literals and identifier references whose initializer is a literal
        var pieces = new List<string>();
        var slots = new List<SlotEntry>();

        if (!FlattenConcat(valueExpr, localInitializers, pieces, slots, out var unsupportedReason))
        {
            return new ResolutionResult { Resolved = false, Reason = unsupportedReason };
        }

        var sample = string.Concat(pieces);
        return new ResolutionResult
        {
            Resolved = true,
            SampleSource = sample,
            Slots = slots,
        };
    }

    private static bool FlattenConcat(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, string> locals,
        List<string> pieces,
        List<SlotEntry> slots,
        out string unsupportedReason)
    {
        unsupportedReason = "";
        switch (expr)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                pieces.Add(lit.Token.ValueText);
                return true;

            case IdentifierNameSyntax id:
                if (locals.TryGetValue(id.Identifier.ValueText, out var literalValue))
                {
                    pieces.Add(literalValue);
                    return true;
                }
                // Treat as a dynamic slot (entity-property style)
                AddSlot(slots, id.Identifier.ValueText, type: "entity-property");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;

            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
                if (!FlattenConcat(bin.Left, locals, pieces, slots, out unsupportedReason)) return false;
                if (!FlattenConcat(bin.Right, locals, pieces, slots, out unsupportedReason)) return false;
                return true;

            case InvocationExpressionSyntax invoc when IsEntityGetProperty(invoc):
                AddSlot(slots, $"entity.GetProperty({GetFirstStringArg(invoc)})", type: "entity-property");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;

            case InvocationExpressionSyntax invoc when IsRandomCall(invoc):
                AddSlot(slots, "Random(...)", type: "string-format-arg");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;

            default:
                unsupportedReason =
                    $"unsupported expression for PR1 AST subset: {expr.Kind()} '{expr.ToString()}'";
                return false;
        }
    }

    private static bool IsEntityGetProperty(InvocationExpressionSyntax invoc)
    {
        if (invoc.Expression is MemberAccessExpressionSyntax m
            && m.Name.Identifier.ValueText == "GetProperty")
        {
            return true;
        }
        return false;
    }

    private static bool IsRandomCall(InvocationExpressionSyntax invoc)
    {
        if (invoc.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "Random") return true;
        return false;
    }

    private static string GetFirstStringArg(InvocationExpressionSyntax invoc)
    {
        if (invoc.ArgumentList.Arguments.Count == 0) return "?";
        if (invoc.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit) return $"\"{lit.Token.ValueText}\"";
        return invoc.ArgumentList.Arguments[0].Expression.ToString();
    }

    private static void AddSlot(List<SlotEntry> slots, string raw, string type)
    {
        slots.Add(new SlotEntry
        {
            Index = slots.Count,
            Type = type,
            Raw = raw,
            Default = $"{{t{slots.Count}}}",
        });
    }

    private static CandidateEntry BuildCandidate(
        string id,
        string sourceFile,
        string annalClass,
        string switchCase,
        string eventProperty,
        ResolutionResult resolved)
    {
        var sample = resolved.SampleSource;
        var pattern = BuildAnchoredRegex(sample, resolved.Slots);
        var c = new CandidateEntry
        {
            Id = id,
            SourceFile = sourceFile,
            AnnalClass = annalClass,
            SwitchCase = switchCase,
            EventProperty = eventProperty,
            SampleSource = sample,
            ExtractedPattern = pattern,
            Slots = resolved.Slots,
            Status = "pending",
            Reason = "",
            JaTemplate = "",
            ReviewNotes = "",
            Route = "annals",
        };
        c.EnTemplateHash = HashHelper.ComputeEnTemplateHash(c);
        return c;
    }

    private static string BuildAnchoredRegex(string sample, List<SlotEntry> slots)
    {
        // Replace each "{N}" placeholder in the sample with a non-greedy capture group, escape literals.
        var sb = new StringBuilder("^");
        var i = 0;
        var slotIndex = 0;
        while (i < sample.Length)
        {
            if (sample[i] == '{' && i + 2 < sample.Length && char.IsDigit(sample[i + 1]))
            {
                var close = sample.IndexOf('}', i);
                if (close > i)
                {
                    sb.Append("(.+?)");
                    i = close + 1;
                    slotIndex++;
                    continue;
                }
            }
            sb.Append(Regex.Escape(sample[i].ToString()));
            i++;
        }
        sb.Append('$');
        return sb.ToString();
    }

    private static CandidateEntry NeedsManual(
        string id,
        string sourceFile,
        string annalClass,
        string? switchCase,
        string eventProperty,
        string reason)
    {
        var c = new CandidateEntry
        {
            Id = id,
            SourceFile = sourceFile,
            AnnalClass = annalClass,
            SwitchCase = switchCase,
            EventProperty = eventProperty,
            SampleSource = "",
            ExtractedPattern = "",
            Slots = new List<SlotEntry>(),
            Status = "needs_manual",
            Reason = reason,
            JaTemplate = "",
            ReviewNotes = "",
            Route = "annals",
        };
        c.EnTemplateHash = HashHelper.ComputeEnTemplateHash(c);
        return c;
    }
}
```

- [ ] **Step 9: Create Program.cs**

Create `scripts/tools/AnnalsPatternExtractor/Program.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QudJP.Tools.AnnalsPatternExtractor;

string? sourceRoot = null;
string? include = null;
string? output = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--source-root": sourceRoot = args[++i]; break;
        case "--include": include = args[++i]; break;
        case "--output": output = args[++i]; break;
        case "--help":
            Console.Out.WriteLine("Usage: AnnalsPatternExtractor --source-root <dir> --include <glob> --output <json-path>");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

if (sourceRoot is null || include is null || output is null)
{
    Console.Error.WriteLine("Missing required argument. Use --help.");
    return 2;
}

if (!Directory.Exists(sourceRoot))
{
    Console.Error.WriteLine($"--source-root does not exist: {sourceRoot}");
    return 1;
}

var globPattern = include;
var files = Directory.GetFiles(sourceRoot, globPattern, SearchOption.TopDirectoryOnly)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine($"No files matched --include '{include}' under {sourceRoot}");
    return 1;
}

var extractor = new Extractor();
foreach (var file in files)
{
    Console.Out.WriteLine($"[extract] processing {Path.GetFileName(file)}");
    extractor.ProcessFile(file);
}

foreach (var diag in extractor.Diagnostics)
{
    Console.Error.WriteLine($"[warn] {diag}");
}

var doc = new CandidateDocument
{
    SchemaVersion = "1",
    Candidates = extractor.Candidates.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
};
CandidateWriter.WriteToFile(output, doc);

Console.Out.WriteLine($"[extract] wrote {doc.Candidates.Count} candidate(s) to {output}");
return 0;
```

- [ ] **Step 10: Build the extractor**

```bash
dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj --configuration Release
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 11: Re-run smoke test**

```bash
pytest scripts/tests/test_roslyn_extractor_smoke.py -v
```

Expected: PASS.

- [ ] **Step 12: Create the four fixture .cs files**

Create `scripts/tests/fixtures/annals/simple_concat.cs`:

```csharp
using System;

namespace XRL.Annals;

[Serializable]
public class ReshephIsBornFixture : HistoricEvent
{
    public override void Generate()
    {
        string text = "<spice.commonPhrases.oneStarryNight.!random.capitalize>";
        SetEventProperty("gospel", text + ", a sultan was born in the salt marsh.");
    }
}
```

Create `scripts/tests/fixtures/annals/string_format.cs`:

```csharp
using System;

namespace XRL.Annals;

[Serializable]
public class BloodyBattleFixture : HistoricEvent
{
    public override void Generate()
    {
        SetEventProperty(
            "tombInscription",
            string.Format("In the {0}, {1} vanquished {2}.", "year", "Resheph", "an enemy"));
    }
}
```

Create `scripts/tests/fixtures/annals/switch_cases.cs`:

```csharp
using System;

namespace XRL.Annals;

[Serializable]
public class FoundAsBabeFixture : HistoricEvent
{
    public override void Generate()
    {
        switch (Random(0, 2))
        {
            case 0:
                SetEventProperty("gospel", "case zero gospel.");
                break;
            case 1:
                SetEventProperty("gospel", "case one gospel.");
                break;
            default:
                SetEventProperty("gospel", "default gospel.");
                break;
        }
    }
}
```

Create `scripts/tests/fixtures/annals/unresolved_variable.cs`:

```csharp
using System;

namespace XRL.Annals;

[Serializable]
public class UnresolvedFixture : HistoricEvent
{
    public override void Generate()
    {
        string mystery = SomeHelper.Compute();
        SetEventProperty("gospel", "prefix " + mystery + " suffix.");
    }
}
```

- [ ] **Step 13: Generate golden JSON for each fixture**

Run the extractor against each fixture and capture the output as the golden JSON:

```bash
mkdir -p /tmp/annals-fixture-out
for fixture in simple_concat string_format switch_cases unresolved_variable; do
    dotnet run --project scripts/tools/AnnalsPatternExtractor -- \
        --source-root scripts/tests/fixtures/annals \
        --include "${fixture}.cs" \
        --output /tmp/annals-fixture-out/${fixture}.json
    cp /tmp/annals-fixture-out/${fixture}.json scripts/tests/fixtures/annals/expected_${fixture}.json
done
```

Expected: 4 JSON files written, each with `schema_version: "1"` and at least one candidate.

- [ ] **Step 14: Manually inspect each golden JSON**

```bash
for f in scripts/tests/fixtures/annals/expected_*.json; do echo "=== $f ==="; cat "$f"; done
```

Verify:
- `expected_simple_concat.json`: 1 candidate, `status="pending"`, `sample_source` contains the resolved literal `<spice...>` + `, a sultan was born in the salt marsh.`
- `expected_string_format.json`: 1 candidate, `status="needs_manual"`, `reason` mentions string.Format
- `expected_switch_cases.json`: 3 candidates with `status="needs_manual"`, `reason` mentions switch/case
- `expected_unresolved_variable.json`: 1 candidate, `status="needs_manual"`, `reason` mentions unsupported expression OR mentions the unresolved identifier (depending on how `mystery` is treated — if `IdentifierNameSyntax` falls through to "treat as dynamic slot", the candidate may still be `pending` with a slot). **If a fixture's actual behavior surprises you, update the fixture .cs to make the unsupported case clearer (e.g. use a method-call expression that obviously falls outside the AST subset), then regenerate the golden.** The point is that the golden JSON should reflect the deterministic output of the extractor, whatever that is — once locked in, the test asserts no regression.

- [ ] **Step 15: Write the extract golden test (TDD)**

Create `scripts/tests/test_extract_annals_patterns.py`:

```python
"""Golden tests for the AnnalsPatternExtractor C# tool."""

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path

import pytest

PROJECT_PATH = Path("scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj")
FIXTURES = Path("scripts/tests/fixtures/annals")


def _run_extractor(include: str, output: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(PROJECT_PATH),
            "--",
            "--source-root",
            str(FIXTURES),
            "--include",
            include,
            "--output",
            str(output),
        ],
        capture_output=True,
        text=True,
        check=False,
    )


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
@pytest.mark.parametrize(
    "fixture",
    [
        "simple_concat",
        "string_format",
        "switch_cases",
        "unresolved_variable",
    ],
)
def test_extractor_matches_golden(fixture: str, tmp_path: Path) -> None:
    output = tmp_path / f"{fixture}.json"
    result = _run_extractor(f"{fixture}.cs", output)
    assert result.returncode == 0, (
        f"extractor failed (exit {result.returncode}). "
        f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )

    actual = json.loads(output.read_text(encoding="utf-8"))
    expected = json.loads((FIXTURES / f"expected_{fixture}.json").read_text(encoding="utf-8"))

    # Schema sanity (will catch if golden was regenerated against a broken extractor)
    assert actual["schema_version"] == "1"
    assert "candidates" in actual

    # Direct equality. If the extractor changes output shape, the golden must be regenerated.
    assert actual == expected, f"extractor output diverged from golden for {fixture}"
```

- [ ] **Step 16: Run the golden tests**

```bash
pytest scripts/tests/test_extract_annals_patterns.py -v
```

Expected: 4/4 PASS.

- [ ] **Step 17: Run ruff to confirm Python style is clean**

```bash
ruff check scripts/tests/test_extract_annals_patterns.py scripts/tests/test_roslyn_extractor_smoke.py
```

Expected: no warnings.

- [ ] **Step 18: Commit**

```bash
git add scripts/tools/AnnalsPatternExtractor/ scripts/tests/fixtures/annals/ scripts/tests/test_extract_annals_patterns.py scripts/tests/test_roslyn_extractor_smoke.py
git commit -m "$(cat <<'EOF'
feat(420): AnnalsPatternExtractor (Roslyn AST → candidate JSON)

Repo-local C# console at scripts/tools/AnnalsPatternExtractor/ that
parses XRL.Annals/*.cs via CSharpSyntaxTree, finds
SetEventProperty("gospel"|"tombInscription", value) calls in Generate(),
flattens + concatenations of literals + same-method local references,
classifies dynamic slots, and emits the candidate JSON schema (§3.6).

Unsupported AST shapes (switch/case, string.Format, unresolved locals)
degrade to status=needs_manual with a reason, per spec §3.1 PR1 AST
subset.

Tests: dotnet build smoke + 4 fixture-driven golden tests. Fixtures use
synthetic .cs (no decompiled source committed per CLAUDE.md).

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §3.1, §3.6, §5.2
EOF
)"
```

---

### Task 4: extract_annals_patterns.py (Python wrapper)

**Files:**

- Create: `scripts/extract_annals_patterns.py`

**Spec references:** §3.2, §4.2

- [ ] **Step 1: Read the existing Python script style**

```bash
head -30 scripts/translate_corpus_batch.py
head -30 scripts/check_encoding.py
```

Note the imports, ruff suppressions style, and CLI argv conventions.

- [ ] **Step 2: Write the wrapper**

Create `scripts/extract_annals_patterns.py`:

```python
"""Run AnnalsPatternExtractor against decompiled XRL.Annals/*.cs sources."""
# ruff: noqa: T201

from __future__ import annotations

import argparse
import datetime as dt
import json
import shutil
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PROJECT_PATH = REPO_ROOT / "scripts" / "tools" / "AnnalsPatternExtractor" / "AnnalsPatternExtractor.csproj"


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract Annals candidate patterns via Roslyn AST.",
    )
    parser.add_argument("--source-root", required=True, type=Path,
                        help="Decompiled XRL.Annals directory")
    parser.add_argument("--include", required=True,
                        help="Glob filter, e.g. 'Resheph*.cs'")
    parser.add_argument("--output", required=True, type=Path,
                        help="Path where candidate JSON will be written")
    parser.add_argument("--force", action="store_true",
                        help="Overwrite existing output (creates a .bak-YYYYMMDDHHMMSS first)")
    return parser.parse_args(argv)


def backup_existing(path: Path) -> Path:
    timestamp = dt.datetime.now().strftime("%Y%m%d%H%M%S")
    backup = path.with_suffix(path.suffix + f".bak-{timestamp}")
    shutil.copy2(path, backup)
    return backup


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)

    if not args.source_root.is_dir():
        print(f"error: --source-root does not exist: {args.source_root}", file=sys.stderr)
        return 1

    if args.output.exists():
        if not args.force:
            print(
                f"error: {args.output} already exists. "
                f"Re-run with --force to overwrite (a .bak- copy will be made first).",
                file=sys.stderr,
            )
            return 1
        backup = backup_existing(args.output)
        print(f"[extract] backed up existing output to {backup}")

    args.output.parent.mkdir(parents=True, exist_ok=True)

    if not shutil.which("dotnet"):
        print(
            "error: dotnet 10.0.x SDK required; install via standard means.",
            file=sys.stderr,
        )
        return 1

    cmd = [
        "dotnet", "run",
        "--project", str(PROJECT_PATH),
        "--",
        "--source-root", str(args.source_root),
        "--include", args.include,
        "--output", str(args.output),
    ]
    print(f"[extract] running: {' '.join(cmd)}")
    result = subprocess.run(cmd, check=False)
    if result.returncode != 0:
        print(f"error: extractor exited with {result.returncode}", file=sys.stderr)
        return 1

    # Validate basic JSON shape before declaring success
    try:
        doc = json.loads(args.output.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: produced output is not valid JSON: {exc}", file=sys.stderr)
        return 1
    if doc.get("schema_version") != "1" or "candidates" not in doc:
        print(f"error: produced output has unexpected schema: {doc.keys()}", file=sys.stderr)
        return 1

    n = len(doc["candidates"])
    accepted = sum(1 for c in doc["candidates"] if c["status"] == "pending")
    needs = sum(1 for c in doc["candidates"] if c["status"] == "needs_manual")
    print(f"[extract] OK — {n} candidate(s): {accepted} pending, {needs} needs_manual")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 3: Smoke-run against a fixture**

```bash
rm -f /tmp/extract-output.json
python3.12 scripts/extract_annals_patterns.py \
    --source-root scripts/tests/fixtures/annals \
    --include "simple_concat.cs" \
    --output /tmp/extract-output.json
```

Expected: exit 0, prints `[extract] OK — 1 candidate(s): 1 pending, 0 needs_manual`. Output file exists.

- [ ] **Step 4: Test the --force backup**

```bash
python3.12 scripts/extract_annals_patterns.py \
    --source-root scripts/tests/fixtures/annals \
    --include "simple_concat.cs" \
    --output /tmp/extract-output.json
```

Expected: exit 1, error message about existing output.

```bash
python3.12 scripts/extract_annals_patterns.py \
    --source-root scripts/tests/fixtures/annals \
    --include "simple_concat.cs" \
    --output /tmp/extract-output.json \
    --force
ls /tmp/extract-output.json* | head
```

Expected: exit 0, `.bak-YYYYMMDDHHMMSS` file exists alongside the new output.

- [ ] **Step 5: Lint**

```bash
ruff check scripts/extract_annals_patterns.py
```

Expected: clean.

- [ ] **Step 6: Commit**

```bash
git add scripts/extract_annals_patterns.py
git commit -m "$(cat <<'EOF'
feat(420): scripts/extract_annals_patterns.py

Python wrapper that invokes the Roslyn AnnalsPatternExtractor via
dotnet run, validates the produced candidate JSON shape, and reports a
counts summary (pending / needs_manual). Refuses to overwrite an
existing artifact unless --force is given (which first creates a
.bak-YYYYMMDDHHMMSS backup, since candidates_pending.json contains
unrecoverable human review state).

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §3.2
EOF
)"
```

---

### Task 5: validate_candidate_schema.py + tests

**Files:**

- Create: `scripts/validate_candidate_schema.py`
- Create: `scripts/tests/test_validate_candidate_schema.py`

**Spec references:** §3.3, §4.3 validate stage, §5.2

- [ ] **Step 1: Write the failing tests first (TDD)**

Create `scripts/tests/test_validate_candidate_schema.py`:

```python
"""Schema validation for annals candidate JSON."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path
from typing import Any

import pytest

SCRIPT = Path("scripts/validate_candidate_schema.py")


def _candidate(**overrides: Any) -> dict[str, Any]:
    base = {
        "id": "ReshephIsBorn#default",
        "source_file": "ReshephIsBorn.cs",
        "annal_class": "ReshephIsBorn",
        "switch_case": "default",
        "event_property": "gospel",
        "sample_source": "Resheph was born in the salt marsh.",
        "extracted_pattern": r"^Resheph was born in (.+?)\.$",
        "slots": [{"index": 0, "type": "spice", "raw": "<spice...>", "default": "{t0}"}],
        "status": "pending",
        "reason": "",
        "ja_template": "",
        "review_notes": "",
        "route": "annals",
        "en_template_hash": "sha256:abc",
    }
    base.update(overrides)
    return base


def _doc(*candidates: dict[str, Any], schema_version: str = "1") -> dict[str, Any]:
    return {"schema_version": schema_version, "candidates": list(candidates)}


def _run(tmp_path: Path, doc: dict[str, Any]) -> subprocess.CompletedProcess[str]:
    p = tmp_path / "candidates.json"
    p.write_text(json.dumps(doc), encoding="utf-8")
    return subprocess.run(
        [sys.executable, str(SCRIPT), str(p)],
        capture_output=True,
        text=True,
        check=False,
    )


def test_validate_passes_for_minimal_doc(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate()))
    assert result.returncode == 0, result.stderr


def test_validate_passes_for_accepted_with_template(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(status="accepted", ja_template="{t0}でレシェフが生まれた。")))
    assert result.returncode == 0, result.stderr


def test_validate_fails_when_schema_version_missing(tmp_path: Path) -> None:
    p = tmp_path / "candidates.json"
    p.write_text(json.dumps({"candidates": [_candidate()]}), encoding="utf-8")
    result = subprocess.run([sys.executable, str(SCRIPT), str(p)], capture_output=True, text=True, check=False)
    assert result.returncode == 1
    assert "schema_version" in result.stderr.lower()


def test_validate_fails_when_schema_version_wrong(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(), schema_version="2"))
    assert result.returncode == 1
    assert "schema_version" in result.stderr.lower()


def test_validate_fails_when_unknown_top_level_field(tmp_path: Path) -> None:
    doc = _doc(_candidate())
    doc["unknown_top"] = "boom"
    result = _run(tmp_path, doc)
    assert result.returncode == 1
    assert "unknown" in result.stderr.lower()


def test_validate_fails_for_invalid_status(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(status="bogus")))
    assert result.returncode == 1
    assert "status" in result.stderr.lower()


def test_validate_fails_when_accepted_has_empty_ja_template(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(status="accepted", ja_template="")))
    assert result.returncode == 1
    assert "ja_template" in result.stderr.lower()


def test_validate_fails_when_extracted_pattern_invalid_regex(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(extracted_pattern="(unclosed")))
    assert result.returncode == 1
    assert "regex" in result.stderr.lower() or "compile" in result.stderr.lower()


def test_validate_fails_when_placeholder_index_out_of_range(tmp_path: Path) -> None:
    # extracted_pattern has 1 capture; ja_template references {t1}, which is index 1 (i.e. 2nd capture).
    doc = _doc(_candidate(
        status="accepted",
        ja_template="{t1}foo",
        extracted_pattern=r"^Resheph was born in (.+?)\.$",
    ))
    result = _run(tmp_path, doc)
    assert result.returncode == 1
    assert "placeholder" in result.stderr.lower() or "index" in result.stderr.lower()


def test_validate_fails_when_id_duplicate(tmp_path: Path) -> None:
    result = _run(tmp_path, _doc(_candidate(id="A"), _candidate(id="A")))
    assert result.returncode == 1
    assert "duplicate" in result.stderr.lower() or "unique" in result.stderr.lower()


def test_validate_fails_when_required_field_missing(tmp_path: Path) -> None:
    bad = _candidate()
    del bad["sample_source"]
    result = _run(tmp_path, _doc(bad))
    assert result.returncode == 1
    assert "sample_source" in result.stderr.lower() or "missing" in result.stderr.lower()


def test_validate_passes_with_review_notes_allowlisted(tmp_path: Path) -> None:
    # review_notes is allowlisted per spec §3.3 even though it's not strictly required
    c = _candidate(review_notes="reviewed by foo on 2026-04-26")
    result = _run(tmp_path, _doc(c))
    assert result.returncode == 0
```

- [ ] **Step 2: Run the tests to confirm they fail**

```bash
pytest scripts/tests/test_validate_candidate_schema.py -v
```

Expected: most tests fail with "ENOENT" or "module not found" — `validate_candidate_schema.py` does not exist yet.

- [ ] **Step 3: Implement the validator**

Create `scripts/validate_candidate_schema.py`:

```python
"""Validate the candidate JSON schema produced by extract_annals_patterns.py."""
# ruff: noqa: T201

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

EXPECTED_SCHEMA_VERSION = "1"
ALLOWED_TOP_LEVEL_KEYS = {"schema_version", "candidates"}
REQUIRED_CANDIDATE_KEYS = {
    "id", "source_file", "annal_class", "switch_case", "event_property",
    "sample_source", "extracted_pattern", "slots", "status", "reason",
    "ja_template", "route", "en_template_hash",
}
ALLOWED_CANDIDATE_KEYS = REQUIRED_CANDIDATE_KEYS | {"review_notes"}
VALID_STATUSES = {"pending", "accepted", "needs_manual", "skip"}
VALID_SLOT_TYPES = {"spice", "entity-property", "grammar-helper", "string-format-arg"}
PLACEHOLDER_RE = re.compile(r"\{(t?)(\d+)\}")


class ValidationError(Exception):
    """Schema validation failed."""


def validate_doc(doc: dict[str, Any]) -> None:
    """Raise ValidationError on any failure. Returns None on success."""
    if not isinstance(doc, dict):
        raise ValidationError("top-level value must be a JSON object")

    extra = set(doc.keys()) - ALLOWED_TOP_LEVEL_KEYS
    if extra:
        raise ValidationError(f"unknown top-level field(s): {sorted(extra)}")

    schema_version = doc.get("schema_version")
    if schema_version is None:
        raise ValidationError("missing top-level field: schema_version")
    if schema_version != EXPECTED_SCHEMA_VERSION:
        raise ValidationError(
            f"unsupported schema_version: {schema_version!r} (expected {EXPECTED_SCHEMA_VERSION!r})"
        )

    candidates = doc.get("candidates")
    if not isinstance(candidates, list):
        raise ValidationError("candidates must be a list")

    seen_ids: set[str] = set()
    for index, candidate in enumerate(candidates):
        validate_candidate(candidate, index)
        cid = candidate["id"]
        if cid in seen_ids:
            raise ValidationError(f"duplicate candidate id: {cid!r} (must be unique)")
        seen_ids.add(cid)


def validate_candidate(candidate: Any, index: int) -> None:
    if not isinstance(candidate, dict):
        raise ValidationError(f"candidate[{index}] must be a JSON object")

    missing = REQUIRED_CANDIDATE_KEYS - set(candidate.keys())
    if missing:
        raise ValidationError(f"candidate[{index}] missing required field(s): {sorted(missing)}")

    extra = set(candidate.keys()) - ALLOWED_CANDIDATE_KEYS
    if extra:
        raise ValidationError(f"candidate[{index}] unknown field(s): {sorted(extra)}")

    status = candidate["status"]
    if status not in VALID_STATUSES:
        raise ValidationError(
            f"candidate[{index}] invalid status: {status!r} (allowed: {sorted(VALID_STATUSES)})"
        )

    if candidate["event_property"] not in {"gospel", "tombInscription"}:
        raise ValidationError(
            f"candidate[{index}] invalid event_property: {candidate['event_property']!r}"
        )

    pattern = candidate["extracted_pattern"]
    capture_count: int
    if pattern == "" and status in {"needs_manual", "skip"}:
        capture_count = 0
    else:
        try:
            compiled = re.compile(pattern)
        except re.error as exc:
            raise ValidationError(f"candidate[{index}] regex compile failed: {exc}") from exc
        capture_count = compiled.groups

    ja_template = candidate["ja_template"]
    if status == "accepted" and not ja_template:
        raise ValidationError(
            f"candidate[{index}] status=accepted requires non-empty ja_template"
        )
    if ja_template:
        for match in PLACEHOLDER_RE.finditer(ja_template):
            slot_index = int(match.group(2))
            if slot_index >= capture_count:
                raise ValidationError(
                    f"candidate[{index}] ja_template placeholder index {slot_index} "
                    f"exceeds capture count {capture_count} of pattern {pattern!r}"
                )

    slots = candidate["slots"]
    if not isinstance(slots, list):
        raise ValidationError(f"candidate[{index}] slots must be a list")
    for slot_index, slot in enumerate(slots):
        if not isinstance(slot, dict):
            raise ValidationError(f"candidate[{index}].slots[{slot_index}] must be a JSON object")
        for required in ("index", "type", "raw", "default"):
            if required not in slot:
                raise ValidationError(
                    f"candidate[{index}].slots[{slot_index}] missing field: {required}"
                )
        if slot["type"] not in VALID_SLOT_TYPES:
            raise ValidationError(
                f"candidate[{index}].slots[{slot_index}] invalid type: {slot['type']!r}"
            )

    route = candidate["route"]
    if route != "annals":
        raise ValidationError(f"candidate[{index}] route must be 'annals' (got {route!r})")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate annals candidate JSON.")
    parser.add_argument("path", type=Path, help="Path to candidates JSON file")
    args = parser.parse_args(argv)

    try:
        doc = json.loads(args.path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: could not read JSON from {args.path}: {exc}", file=sys.stderr)
        return 1

    try:
        validate_doc(doc)
    except ValidationError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1

    print(f"[validate] OK — {len(doc['candidates'])} candidate(s) pass schema check")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run the tests**

```bash
pytest scripts/tests/test_validate_candidate_schema.py -v
```

Expected: 12/12 PASS.

- [ ] **Step 5: Lint**

```bash
ruff check scripts/validate_candidate_schema.py scripts/tests/test_validate_candidate_schema.py
```

Expected: clean.

- [ ] **Step 6: Commit**

```bash
git add scripts/validate_candidate_schema.py scripts/tests/test_validate_candidate_schema.py
git commit -m "$(cat <<'EOF'
feat(420): scripts/validate_candidate_schema.py

Strict schema check for the annals candidate JSON: schema_version match,
allowlisted fields (incl. review_notes), required candidate keys, status
enum, event_property enum, regex compile, ja_template placeholder index
≤ capture count, slot type enum, route='annals', id uniqueness.

Importable as a library: validate_doc(dict) raises ValidationError on
failure (used by merge_annals_patterns.py per spec §3.5 idempotency
defense).

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §3.3
EOF
)"
```

---

### Task 6: translate_annals_patterns.py + tests

**Files:**

- Create: `scripts/translate_annals_patterns.py`
- Create: `scripts/tests/test_translate_annals_patterns.py`

**Spec references:** §3.4, §3.6 hash semantics, §3.8 glossary, §4.3 translate stage

- [ ] **Step 1: Read the existing translate_corpus_batch.py to understand glossary loading**

```bash
sed -n '40,75p' scripts/translate_corpus_batch.py
```

Note `load_glossary()` and the prompt template structure.

- [ ] **Step 2: Write the failing tests (TDD)**

Create `scripts/tests/test_translate_annals_patterns.py`:

```python
"""Tests for translate_annals_patterns.py."""
# ruff: noqa: S603, S607

from __future__ import annotations

import hashlib
import json
import sys
import textwrap
from pathlib import Path
from typing import Any
from unittest.mock import MagicMock, patch

import pytest

# Importable as a module
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import translate_annals_patterns as tap  # noqa: E402


def _candidate(**overrides: Any) -> dict[str, Any]:
    base = {
        "id": "ReshephIsBorn#default",
        "source_file": "ReshephIsBorn.cs",
        "annal_class": "ReshephIsBorn",
        "switch_case": "default",
        "event_property": "gospel",
        "sample_source": "Resheph was born in the salt marsh.",
        "extracted_pattern": r"^Resheph was born in (.+?)\.$",
        "slots": [{"index": 0, "type": "spice", "raw": "<spice...>", "default": "{t0}"}],
        "status": "accepted",
        "reason": "",
        "ja_template": "",
        "review_notes": "",
        "route": "annals",
        "en_template_hash": "",  # filled in by test setup
    }
    base.update(overrides)
    base["en_template_hash"] = tap.compute_en_template_hash(base)
    return base


def test_compute_en_template_hash_is_deterministic() -> None:
    c = _candidate()
    h1 = tap.compute_en_template_hash(c)
    h2 = tap.compute_en_template_hash(c)
    assert h1 == h2
    assert h1.startswith("sha256:")


def test_compute_en_template_hash_changes_on_pattern_change() -> None:
    c1 = _candidate()
    c2 = _candidate(extracted_pattern=r"^Resheph born$")
    h1 = tap.compute_en_template_hash(c1)
    h2 = tap.compute_en_template_hash(c2)
    assert h1 != h2


def test_compute_en_template_hash_ignores_status_and_ja_template() -> None:
    c1 = _candidate(status="pending", ja_template="X")
    c2 = _candidate(status="accepted", ja_template="Y")
    # status/ja_template are excluded from the hash per spec §3.6
    assert tap.compute_en_template_hash(c1) == tap.compute_en_template_hash(c2)


def test_select_pending_skips_already_translated_with_matching_hash(tmp_path: Path) -> None:
    c = _candidate(ja_template="既に翻訳済み")
    pending = tap.select_pending_candidates([c])
    assert pending == []


def test_select_pending_picks_up_stale_translation(tmp_path: Path) -> None:
    c = _candidate(ja_template="既に翻訳済み")
    # Simulate human edit that changes the pattern but not the cached hash
    c["extracted_pattern"] = r"^Different pattern$"
    pending = tap.select_pending_candidates([c])
    assert len(pending) == 1


def test_select_pending_picks_up_empty_template() -> None:
    c = _candidate(ja_template="")
    pending = tap.select_pending_candidates([c])
    assert len(pending) == 1


def test_select_pending_skips_non_accepted_status() -> None:
    c1 = _candidate(status="pending", ja_template="")
    c2 = _candidate(id="B", status="needs_manual", ja_template="")
    c3 = _candidate(id="C", status="skip", ja_template="")
    assert tap.select_pending_candidates([c1, c2, c3]) == []


def test_chunk_candidates_groups_5_to_8() -> None:
    cands = [_candidate(id=f"R#{i}") for i in range(13)]
    chunks = tap.chunk_candidates(cands)
    # 13 items at default chunk_size=8 -> [8, 5]
    assert [len(c) for c in chunks] == [8, 5]


def test_validate_chunk_response_rejects_missing_id() -> None:
    cands = [_candidate(id="A"), _candidate(id="B")]
    response = [{"id": "A", "ja_template": "X"}]  # missing B
    valid, errors = tap.validate_chunk_response(cands, response)
    assert not valid
    assert any("missing" in e.lower() or "B" in e for e in errors)


def test_validate_chunk_response_rejects_unknown_id() -> None:
    cands = [_candidate(id="A")]
    response = [{"id": "A", "ja_template": "X"}, {"id": "PHANTOM", "ja_template": "Y"}]
    valid, errors = tap.validate_chunk_response(cands, response)
    assert not valid


def test_validate_chunk_response_rejects_empty_ja_template() -> None:
    cands = [_candidate(id="A")]
    response = [{"id": "A", "ja_template": ""}]
    valid, errors = tap.validate_chunk_response(cands, response)
    assert not valid


def test_validate_chunk_response_rejects_placeholder_out_of_range() -> None:
    # pattern has 1 capture; response uses {t1} which is index 1, exceeds 1 capture
    cands = [_candidate(id="A", extracted_pattern=r"^Resheph (.+?)\.$")]
    response = [{"id": "A", "ja_template": "{t1}OK"}]
    valid, errors = tap.validate_chunk_response(cands, response)
    assert not valid


def test_validate_chunk_response_accepts_valid() -> None:
    cands = [_candidate(id="A")]
    response = [{"id": "A", "ja_template": "{t0}でレシェフが生まれた。"}]
    valid, errors = tap.validate_chunk_response(cands, response)
    assert valid, errors


@patch("subprocess.run")
def test_invoke_codex_returns_json_array_on_success(mock_run: MagicMock, tmp_path: Path) -> None:
    payload = json.dumps([{"id": "A", "ja_template": "{t0}OK"}])
    mock_run.return_value.returncode = 0
    mock_run.return_value.stdout = payload
    mock_run.return_value.stderr = ""

    result = tap.invoke_codex_translation([_candidate(id="A")], glossary="dummy")
    assert result == [{"id": "A", "ja_template": "{t0}OK"}]


@patch("subprocess.run")
def test_invoke_codex_returns_none_on_unparseable(mock_run: MagicMock) -> None:
    mock_run.return_value.returncode = 0
    mock_run.return_value.stdout = "not json"
    mock_run.return_value.stderr = ""
    result = tap.invoke_codex_translation([_candidate(id="A")], glossary="dummy")
    assert result is None


@patch("subprocess.run")
def test_invoke_codex_returns_none_on_nonzero_exit(mock_run: MagicMock) -> None:
    mock_run.return_value.returncode = 1
    mock_run.return_value.stdout = ""
    mock_run.return_value.stderr = "auth error"
    result = tap.invoke_codex_translation([_candidate(id="A")], glossary="dummy")
    assert result is None


def test_save_partial_progress_updates_in_place(tmp_path: Path) -> None:
    candidates = [_candidate(id="A"), _candidate(id="B")]
    doc = {"schema_version": "1", "candidates": candidates}
    p = tmp_path / "candidates.json"
    p.write_text(json.dumps(doc), encoding="utf-8")

    # Apply translation only for A
    candidates[0]["ja_template"] = "{t0}translated A"
    candidates[0]["en_template_hash"] = tap.compute_en_template_hash(candidates[0])
    tap.save_progress(p, doc)

    on_disk = json.loads(p.read_text(encoding="utf-8"))
    assert on_disk["candidates"][0]["ja_template"] == "{t0}translated A"
    assert on_disk["candidates"][1]["ja_template"] == ""
```

- [ ] **Step 3: Run tests; expect import error**

```bash
pytest scripts/tests/test_translate_annals_patterns.py -v
```

Expected: collection error — `translate_annals_patterns` module not found.

- [ ] **Step 4: Implement the translator**

Create `scripts/translate_annals_patterns.py`:

```python
"""Translate accepted annals candidates via Codex CLI batch invocation."""
# ruff: noqa: T201, S603, S607

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any

CHUNK_SIZE_DEFAULT = 8
MAX_RETRIES = 3
PLACEHOLDER_RE = re.compile(r"\{(t?)(\d+)\}")


def _canonical_json(payload: Any) -> str:
    return json.dumps(payload, sort_keys=True, ensure_ascii=False, separators=(",", ":"))


def compute_en_template_hash(candidate: dict[str, Any]) -> str:
    """Hash the structural identity of a candidate (excludes status/ja_template/reason)."""
    payload = {
        "extracted_pattern": candidate["extracted_pattern"],
        "slots": candidate["slots"],
        "sample_source": candidate["sample_source"],
        "event_property": candidate["event_property"],
        "switch_case": candidate.get("switch_case"),
    }
    digest = hashlib.sha256(_canonical_json(payload).encode("utf-8")).hexdigest()
    return f"sha256:{digest}"


def select_pending_candidates(candidates: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Pick candidates that need a fresh translation."""
    pending: list[dict[str, Any]] = []
    for c in candidates:
        if c["status"] != "accepted":
            continue
        current_hash = compute_en_template_hash(c)
        if c.get("ja_template", "") == "" or c.get("en_template_hash") != current_hash:
            pending.append(c)
    return pending


def chunk_candidates(
    candidates: list[dict[str, Any]],
    chunk_size: int = CHUNK_SIZE_DEFAULT,
) -> list[list[dict[str, Any]]]:
    return [candidates[i:i + chunk_size] for i in range(0, len(candidates), chunk_size)]


def validate_chunk_response(
    chunk: list[dict[str, Any]],
    response: list[dict[str, Any]],
) -> tuple[bool, list[str]]:
    errors: list[str] = []
    expected_ids = {c["id"] for c in chunk}
    received_ids = {item.get("id") for item in response if isinstance(item, dict)}
    missing = expected_ids - received_ids
    extra = received_ids - expected_ids
    if missing:
        errors.append(f"missing translations for ids: {sorted(missing)}")
    if extra:
        errors.append(f"unexpected ids in response: {sorted(extra)}")

    by_id = {c["id"]: c for c in chunk}
    for item in response:
        if not isinstance(item, dict):
            errors.append(f"non-object response entry: {item!r}")
            continue
        cid = item.get("id")
        if cid not in by_id:
            continue
        ja = item.get("ja_template", "")
        if not ja:
            errors.append(f"id={cid}: empty ja_template")
            continue
        candidate = by_id[cid]
        try:
            capture_count = re.compile(candidate["extracted_pattern"]).groups
        except re.error as exc:
            errors.append(f"id={cid}: candidate regex compile fail: {exc}")
            continue
        for match in PLACEHOLDER_RE.finditer(ja):
            slot_index = int(match.group(2))
            if slot_index >= capture_count:
                errors.append(f"id={cid}: placeholder {{...{slot_index}}} exceeds capture count {capture_count}")
                break
    return (not errors, errors)


def build_prompt(chunk: list[dict[str, Any]], glossary: str, all_candidates: list[dict[str, Any]]) -> str:
    """Build the per-chunk Codex prompt with backstory context."""
    context_summary = "\n".join(
        f"- {c['id']}: {c['sample_source'][:100]}{'…' if len(c['sample_source']) > 100 else ''}"
        for c in all_candidates
    )
    chunk_payload = json.dumps(
        [
            {
                "id": c["id"],
                "event_property": c["event_property"],
                "sample_source": c["sample_source"],
                "extracted_pattern": c["extracted_pattern"],
                "slots": c["slots"],
            }
            for c in chunk
        ],
        ensure_ascii=False,
        indent=2,
    )
    return (
        "あなたはCaves of Qudの「Sultan Histories」（スルタン史）ジャーナル文を翻訳します。\n"
        "出力は厳密なJSON配列のみ。各要素は {\"id\": ..., \"ja_template\": ...} のみ。\n"
        "## 必須用語\n"
        f"{glossary}\n\n"
        "## 翻訳ルール\n"
        "1. ja_template には extracted_pattern の capture group に対応する {t0} {t1} ... を使う\n"
        "2. capture が翻訳対象の固有名詞・場所名なら {tN}（per-capture lookup あり）\n"
        "3. capture が year のような構造値なら {N}（lookup なし）\n"
        "4. 文末は半角ピリオド「.」ではなく日本語句点「。」\n"
        "5. 古英語・伝承調の英文は擬古文調の日本語に\n"
        "6. JSON配列のみ出力、コメント・説明は禁止\n\n"
        "## Resheph背景一覧（文脈共有用、訳す対象は下のチャンクのみ）\n"
        f"{context_summary}\n\n"
        "## 翻訳対象チャンク\n"
        f"{chunk_payload}\n"
    )


def invoke_codex_translation(
    chunk: list[dict[str, Any]],
    glossary: str,
    all_candidates: list[dict[str, Any]] | None = None,
) -> list[dict[str, Any]] | None:
    if all_candidates is None:
        all_candidates = chunk
    prompt = build_prompt(chunk, glossary, all_candidates)
    if not shutil.which("codex"):
        print("error: codex CLI not on PATH", file=sys.stderr)
        return None
    try:
        result = subprocess.run(
            ["codex", "exec", "-s", "read-only", "-c", "approval_policy=\"never\"", "-"],
            input=prompt,
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError as exc:
        print(f"error: failed to invoke codex CLI: {exc}", file=sys.stderr)
        return None
    if result.returncode != 0:
        print(f"error: codex CLI exit {result.returncode}: {result.stderr}", file=sys.stderr)
        return None
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError:
        # Try to extract a JSON array from a response that might have surrounding text
        match = re.search(r"\[[\s\S]*\]", result.stdout)
        if match:
            try:
                return json.loads(match.group(0))
            except json.JSONDecodeError:
                pass
        return None


def save_progress(path: Path, doc: dict[str, Any]) -> None:
    """Write the doc back to disk in canonical formatting."""
    text = json.dumps(doc, ensure_ascii=False, indent=2) + "\n"
    path.write_text(text, encoding="utf-8")


def load_glossary_from_existing_pipeline() -> str:
    """Reuse the existing translation_glossary.txt loader from translate_corpus_batch.py."""
    glossary_path = Path("scripts/translation_glossary.txt")
    if not glossary_path.is_file():
        return ""
    return glossary_path.read_text(encoding="utf-8").strip()


def translate_chunk_with_retries(
    chunk: list[dict[str, Any]],
    glossary: str,
    all_candidates: list[dict[str, Any]],
) -> dict[str, str]:
    """Returns id -> ja_template for successfully translated entries.

    Retry strategy per spec §3.4:
    - Whole JSON unparseable → re-send entire chunk (max 3)
    - Parsed but missing IDs → next retry sends only failed IDs
    - 100% match required
    """
    successes: dict[str, str] = {}
    remaining = chunk
    for attempt in range(1, MAX_RETRIES + 1):
        response = invoke_codex_translation(remaining, glossary, all_candidates)
        if response is None:
            print(f"[translate] attempt {attempt}/{MAX_RETRIES}: unparseable; retrying full chunk", file=sys.stderr)
            continue
        valid, errors = validate_chunk_response(remaining, response)
        # Even on partial success, harvest valid items
        for item in response:
            if not isinstance(item, dict): continue
            cid = item.get("id")
            ja = item.get("ja_template", "")
            if cid in {c["id"] for c in remaining} and ja and cid not in successes:
                # Per-item placeholder check
                candidate = next((c for c in remaining if c["id"] == cid), None)
                if candidate is None: continue
                try:
                    capture_count = re.compile(candidate["extracted_pattern"]).groups
                except re.error:
                    continue
                ok = True
                for m in PLACEHOLDER_RE.finditer(ja):
                    if int(m.group(2)) >= capture_count:
                        ok = False
                        break
                if ok:
                    successes[cid] = ja

        if valid and len(successes) == len(chunk):
            return successes

        # Recompute remaining: ids not yet in successes
        remaining = [c for c in chunk if c["id"] not in successes]
        if not remaining:
            return successes
        print(f"[translate] attempt {attempt}/{MAX_RETRIES}: {errors}; will retry {len(remaining)} ids", file=sys.stderr)

    return successes


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Translate accepted annals candidates.")
    parser.add_argument("path", type=Path, help="Path to candidates JSON")
    parser.add_argument("--chunk-size", type=int, default=CHUNK_SIZE_DEFAULT)
    args = parser.parse_args(argv)

    try:
        doc = json.loads(args.path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: cannot read {args.path}: {exc}", file=sys.stderr)
        return 1

    candidates = doc.get("candidates", [])
    pending = select_pending_candidates(candidates)
    if not pending:
        print("[translate] nothing to translate (all accepted candidates have current translations)")
        return 0

    glossary = load_glossary_from_existing_pipeline()
    if not glossary:
        print("error: glossary file scripts/translation_glossary.txt missing or empty", file=sys.stderr)
        return 1

    print(f"[translate] {len(pending)} candidate(s) pending across {len(chunk_candidates(pending, args.chunk_size))} chunk(s)")

    by_id = {c["id"]: c for c in candidates}
    any_failure = False
    for chunk in chunk_candidates(pending, args.chunk_size):
        successes = translate_chunk_with_retries(chunk, glossary, candidates)
        for cid, ja in successes.items():
            target = by_id[cid]
            target["ja_template"] = ja
            target["en_template_hash"] = compute_en_template_hash(target)
        save_progress(args.path, doc)
        for c in chunk:
            if c["id"] not in successes:
                # Downgrade to needs_manual
                target = by_id[c["id"]]
                target["status"] = "needs_manual"
                target["reason"] = "translation retries exhausted; review manually"
                any_failure = True
        save_progress(args.path, doc)

    return 1 if any_failure else 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 5: Run the tests**

```bash
pytest scripts/tests/test_translate_annals_patterns.py -v
```

Expected: 14/14 PASS.

- [ ] **Step 6: Lint**

```bash
ruff check scripts/translate_annals_patterns.py scripts/tests/test_translate_annals_patterns.py
```

Expected: clean.

- [ ] **Step 7: Commit**

```bash
git add scripts/translate_annals_patterns.py scripts/tests/test_translate_annals_patterns.py
git commit -m "$(cat <<'EOF'
feat(420): scripts/translate_annals_patterns.py

Codex CLI batch translator for accepted annals candidates with
en_template_hash-based stale detection (re-translate after human edits)
and per-chunk retry-failed-IDs-only semantics. 100% match requirement;
exhausted retries downgrade the candidate to status=needs_manual rather
than failing the whole pipeline. Reuses translation_glossary.txt as
canonical glossary.

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §3.4, §3.6, §3.8
EOF
)"
```

---

### Task 7: merge_annals_patterns.py + tests

**Files:**

- Create: `scripts/merge_annals_patterns.py`
- Create: `scripts/tests/test_merge_annals_patterns.py`

**Spec references:** §3.5, §3.7, §4.3 merge stage

- [ ] **Step 1: Write the failing tests**

Create `scripts/tests/test_merge_annals_patterns.py`:

```python
"""Tests for merge_annals_patterns.py."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any
from unittest.mock import patch

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import merge_annals_patterns as mer  # noqa: E402


def _candidate(**overrides: Any) -> dict[str, Any]:
    base = {
        "id": "ReshephIsBorn#default",
        "source_file": "ReshephIsBorn.cs",
        "annal_class": "ReshephIsBorn",
        "switch_case": "default",
        "event_property": "gospel",
        "sample_source": "Resheph was born in the salt marsh.",
        "extracted_pattern": r"^Resheph was born in (.+?)\.$",
        "slots": [{"index": 0, "type": "spice", "raw": "<spice...>", "default": "{t0}"}],
        "status": "accepted",
        "reason": "",
        "ja_template": "{t0}でレシェフが生まれた。",
        "review_notes": "",
        "route": "annals",
        "en_template_hash": "sha256:abc",
    }
    base.update(overrides)
    return base


def _doc(*candidates: dict[str, Any]) -> dict[str, Any]:
    return {"schema_version": "1", "candidates": list(candidates)}


def _journal_patterns(*entries: dict[str, str]) -> dict[str, Any]:
    return {"entries": [], "patterns": list(entries)}


def test_merge_filters_only_accepted_with_template(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(
        _candidate(id="A", status="accepted", ja_template="{t0}A"),
        _candidate(id="B", status="needs_manual", ja_template="ignored"),
        _candidate(id="C", status="skip", ja_template="ignored"),
        _candidate(id="D", status="accepted", ja_template=""),
        _candidate(id="E", status="pending", ja_template="ignored"),
    )), encoding="utf-8")

    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 0

    on_disk = json.loads(annals_path.read_text(encoding="utf-8"))
    assert on_disk["entries"] == []
    ids = [p.get("_provenance_id") for p in on_disk["patterns"] if "_provenance_id" in p] or []  # not in shipped schema
    # The shipped schema is {pattern, template, route} only; just count
    assert len(on_disk["patterns"]) == 1


def test_merge_emits_empty_patterns_when_no_accepted(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(
        _candidate(id="A", status="needs_manual", ja_template=""),
    )), encoding="utf-8")

    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 0
    on_disk = json.loads(annals_path.read_text(encoding="utf-8"))
    assert on_disk == {"entries": [], "patterns": []}


def test_merge_detects_raw_collision(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(
        _candidate(id="A", sample_source="Resheph was born in the salt marsh."),
    )), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    # Existing pattern that would already swallow the candidate's sample
    journal_path.write_text(json.dumps(_journal_patterns(
        {"pattern": r"^Resheph was born in (.+?)\.$", "template": "old", "route": "journal"}
    )), encoding="utf-8")

    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 1
    assert conflicts_path.exists()
    conflict_doc = json.loads(conflicts_path.read_text(encoding="utf-8"))
    assert conflict_doc["schema_version"] == "1"
    assert len(conflict_doc["conflicts"]) >= 1
    assert conflict_doc["conflicts"][0]["conflict_type"] == "raw"
    assert "skip_candidate" in conflict_doc["conflicts"][0]["resolution_options"]


def test_merge_detects_normalized_collision(tmp_path: Path) -> None:
    """An existing broad pattern that swallows the slot-normalized form."""
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(
        _candidate(
            id="A",
            sample_source="Resheph was born in the salt marsh.",
            slots=[{"index": 0, "type": "spice", "raw": "the salt marsh", "default": "{t0}"}],
            extracted_pattern=r"^Resheph was born in (.+?)\.$",
        ),
    )), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    # Existing pattern matches the normalized form (where 'the salt marsh' became 'SLOT0')
    journal_path.write_text(json.dumps(_journal_patterns(
        {"pattern": r"^Resheph was born in SLOT0\.$", "template": "old", "route": "journal"}
    )), encoding="utf-8")

    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 1
    conflict_doc = json.loads(conflicts_path.read_text(encoding="utf-8"))
    assert any(c["conflict_type"] == "normalized" for c in conflict_doc["conflicts"])
    assert any("narrow_candidate_pattern" in c["resolution_options"] for c in conflict_doc["conflicts"])


def test_merge_calls_validate_and_rejects_invalid_candidate(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(
        _candidate(id="A", status="accepted", ja_template="", extracted_pattern=r"^.+$"),
        # accepted with empty ja_template is invalid per spec §3.3
    )), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")

    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"
    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 1


def test_merge_clean_run_deletes_stale_conflicts_artifact(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(_candidate(id="A"))), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    # Pre-create a stale conflicts artifact
    conflicts_path.write_text(json.dumps({"schema_version": "1", "conflicts": [{"old": "stale"}]}), encoding="utf-8")
    assert conflicts_path.exists()

    code = mer.run_merge(
        candidates_path=candidates_path,
        existing_journal=journal_path,
        annals_output=annals_path,
        conflicts_output=conflicts_path,
    )
    assert code == 0
    assert not conflicts_path.exists()


def test_merge_output_is_deterministic(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(
        _candidate(id="ZZZ", extracted_pattern=r"^Z (.+?)$", ja_template="{t0}Z"),
        _candidate(id="AAA", extracted_pattern=r"^A (.+?)$", ja_template="{t0}A"),
    )), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path1 = tmp_path / "annals1.json"
    annals_path2 = tmp_path / "annals2.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    mer.run_merge(candidates_path, journal_path, annals_path1, conflicts_path)
    mer.run_merge(candidates_path, journal_path, annals_path2, conflicts_path)

    assert annals_path1.read_text(encoding="utf-8") == annals_path2.read_text(encoding="utf-8")


def test_merge_rejects_malformed_existing_annals(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    candidates_path.write_text(json.dumps(_doc(_candidate(id="A"))), encoding="utf-8")
    journal_path = tmp_path / "journal-patterns.ja.json"
    journal_path.write_text(json.dumps(_journal_patterns()), encoding="utf-8")
    annals_path = tmp_path / "annals-patterns.ja.json"
    conflicts_path = tmp_path / "merge_conflicts.json"

    # Pre-existing malformed annals (missing patterns array)
    annals_path.write_text(json.dumps({"entries": []}), encoding="utf-8")
    code = mer.run_merge(candidates_path, journal_path, annals_path, conflicts_path)
    assert code == 1
```

- [ ] **Step 2: Run tests; expect import error**

```bash
pytest scripts/tests/test_merge_annals_patterns.py -v
```

Expected: collection error.

- [ ] **Step 3: Implement merge**

Create `scripts/merge_annals_patterns.py`:

```python
"""Merge translated annals candidates into Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json."""
# ruff: noqa: T201

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

# Importable as a library by tests
sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_candidate_schema as schema  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_CANDIDATES = REPO_ROOT / "scripts/_artifacts/annals/candidates_pending.json"
DEFAULT_JOURNAL = REPO_ROOT / "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json"
DEFAULT_ANNALS = REPO_ROOT / "Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json"
DEFAULT_CONFLICTS = REPO_ROOT / "scripts/_artifacts/annals/merge_conflicts.json"


def normalize_sample(sample: str, slots: list[dict[str, Any]]) -> str:
    """Replace each slot's raw form in the sample with `SLOT<index>`."""
    out = sample
    for slot in slots:
        raw = slot.get("raw", "")
        if raw and raw in out:
            out = out.replace(raw, f"SLOT{slot.get('index', 0)}")
    return out


def detect_collisions(
    candidate: dict[str, Any],
    journal_patterns: list[dict[str, str]],
    journal_path: Path,
) -> list[dict[str, Any]]:
    """Return zero or more conflict entries for this candidate against the journal-patterns dict."""
    sample = candidate["sample_source"]
    normalized = normalize_sample(sample, candidate["slots"])
    conflicts: list[dict[str, Any]] = []

    for index, entry in enumerate(journal_patterns):
        existing_pattern = entry.get("pattern")
        if not existing_pattern:
            continue
        try:
            compiled = re.compile(existing_pattern)
        except re.error:
            continue
        match_raw = bool(compiled.search(sample))
        match_normalized = bool(compiled.search(normalized))
        if match_raw or match_normalized:
            conflict_type = "raw" if match_raw else "normalized"
            resolution_first = "skip_candidate" if conflict_type == "raw" else "narrow_candidate_pattern"
            other_resolutions = [
                opt for opt in ("skip_candidate", "narrow_candidate_pattern", "replace_existing_after_review")
                if opt != resolution_first
            ]
            conflicts.append({
                "candidate_id": candidate["id"],
                "candidate_pattern": candidate["extracted_pattern"],
                "candidate_pattern_normalized": candidate["extracted_pattern"],  # PR1: same as raw; v2 may differ
                "candidate_template": candidate["ja_template"],
                "sample_source": sample,
                "conflict_type": conflict_type,
                "conflicts": [{
                    "file": str(journal_path),
                    "pattern_index": index,
                    "pattern": existing_pattern,
                    "pattern_normalized": existing_pattern,
                    "template": entry.get("template", ""),
                }],
                "resolution_options": [resolution_first] + other_resolutions,
            })
    return conflicts


def run_merge(
    candidates_path: Path,
    existing_journal: Path,
    annals_output: Path,
    conflicts_output: Path,
) -> int:
    try:
        doc = json.loads(candidates_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: cannot read candidates from {candidates_path}: {exc}", file=sys.stderr)
        return 1

    try:
        schema.validate_doc(doc)
    except schema.ValidationError as exc:
        print(f"error: candidate schema invalid: {exc}", file=sys.stderr)
        return 1

    try:
        journal_doc = json.loads(existing_journal.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"error: cannot read existing journal-patterns from {existing_journal}: {exc}", file=sys.stderr)
        return 1

    journal_patterns = journal_doc.get("patterns", [])
    if not isinstance(journal_patterns, list):
        print(f"error: journal-patterns.ja.json has no 'patterns' array", file=sys.stderr)
        return 1

    # Verify any pre-existing annals-patterns.ja.json is well-formed
    if annals_output.exists():
        try:
            existing_annals = json.loads(annals_output.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            print(f"error: existing annals-patterns is malformed JSON: {exc}", file=sys.stderr)
            return 1
        if "patterns" not in existing_annals:
            print(f"error: existing annals-patterns.ja.json missing 'patterns' field", file=sys.stderr)
            return 1

    accepted = [
        c for c in doc["candidates"]
        if c["status"] == "accepted" and c["ja_template"]
    ]

    all_conflicts: list[dict[str, Any]] = []
    for candidate in accepted:
        all_conflicts.extend(detect_collisions(candidate, journal_patterns, existing_journal))

    if all_conflicts:
        conflicts_doc = {"schema_version": "1", "conflicts": all_conflicts}
        conflicts_output.parent.mkdir(parents=True, exist_ok=True)
        conflicts_output.write_text(
            json.dumps(conflicts_doc, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(f"error: {len(all_conflicts)} conflict(s); see {conflicts_output}", file=sys.stderr)
        return 1

    # Clean run: remove stale conflicts artifact
    if conflicts_output.exists():
        conflicts_output.unlink()

    accepted_sorted = sorted(accepted, key=lambda c: c["id"])
    annals_doc = {
        "entries": [],
        "patterns": [
            {
                "pattern": c["extracted_pattern"],
                "template": c["ja_template"],
                "route": "annals",
            }
            for c in accepted_sorted
        ],
    }
    annals_output.parent.mkdir(parents=True, exist_ok=True)
    annals_output.write_text(
        json.dumps(annals_doc, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"[merge] OK — wrote {len(accepted_sorted)} pattern(s) to {annals_output}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Merge accepted annals candidates into the dictionary.")
    parser.add_argument("path", type=Path, default=DEFAULT_CANDIDATES, nargs="?",
                        help="Path to candidates JSON")
    parser.add_argument("--journal", type=Path, default=DEFAULT_JOURNAL,
                        help="Existing journal-patterns.ja.json")
    parser.add_argument("--annals-output", type=Path, default=DEFAULT_ANNALS,
                        help="Output annals-patterns.ja.json")
    parser.add_argument("--conflicts-output", type=Path, default=DEFAULT_CONFLICTS,
                        help="Conflict report output")
    args = parser.parse_args(argv)

    return run_merge(
        candidates_path=args.path,
        existing_journal=args.journal,
        annals_output=args.annals_output,
        conflicts_output=args.conflicts_output,
    )


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run tests**

```bash
pytest scripts/tests/test_merge_annals_patterns.py -v
```

Expected: 8/8 PASS.

- [ ] **Step 5: Run all Python tests**

```bash
pytest scripts/tests/ -v
```

Expected: all pass.

- [ ] **Step 6: Lint**

```bash
ruff check scripts/merge_annals_patterns.py scripts/tests/test_merge_annals_patterns.py
```

Expected: clean.

- [ ] **Step 7: Commit**

```bash
git add scripts/merge_annals_patterns.py scripts/tests/test_merge_annals_patterns.py
git commit -m "$(cat <<'EOF'
feat(420): scripts/merge_annals_patterns.py

Merge accepted annals candidates into Localization/Dictionaries/annals-
patterns.ja.json. Calls validate_candidate_schema.validate_doc() up
front (idempotency hole defense per spec §3.5). Detects raw and
normalized (slot raws → SLOT0/SLOT1/...) collisions against existing
journal-patterns.ja.json regexes; on conflict writes
scripts/_artifacts/annals/merge_conflicts.json (schema §3.7) and exits
1. Output is deterministic (sorted by id). Empty accepted list emits
{"entries":[], "patterns":[]} and exits 0.

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §3.5, §3.7
EOF
)"
```

---

### Task 8: Generate the Resheph annals-patterns.ja.json (operator-driven)

**Why now:** all four pipeline scripts are in place. This task runs them against the real Resheph 16 files, requires human review of the candidates, and produces the dictionary deliverable.

**Files:**

- Create: `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` (output)

**Spec references:** §3.0, §6.2

> **Note on operator participation:** This task involves manual review steps that an automated subagent cannot fully complete. The subagent should drive the pipeline as far as it can; a human (the spec author) approves the candidate JSON edits and reviews the Codex-translated output. Mark sub-steps that require human judgment with the marker `[HUMAN]`.

- [ ] **Step 1: Verify decompiled source is present**

```bash
ls ~/dev/coq-decompiled_stable/XRL.Annals/Resheph*.cs | head -5
ls ~/dev/coq-decompiled_stable/XRL.Annals/Resheph*.cs | wc -l
```

Expected: 16 files.

If absent, run `scripts/decompile_game_dll.sh` (outside scope of this task; instruct the operator).

- [ ] **Step 2: Run extract**

```bash
python3.12 scripts/extract_annals_patterns.py \
    --source-root ~/dev/coq-decompiled_stable/XRL.Annals \
    --include "Resheph*.cs" \
    --output scripts/_artifacts/annals/candidates_pending.json
```

Expected: exit 0, summary line shows the candidate count.

- [ ] **Step 3: Run validate (sanity-check the freshly extracted JSON)**

```bash
python3.12 scripts/validate_candidate_schema.py \
    scripts/_artifacts/annals/candidates_pending.json
```

Expected: exit 0.

- [ ] **Step 4: [HUMAN] Review each candidate and set status + ja_template**

For each candidate:
- If `status="needs_manual"`, decide: hand-craft `extracted_pattern` + `slots` + `ja_template`, or set `status="skip"` if the C# composition can't reasonably be regex-extracted.
- If `status="pending"`, decide: set `status="accepted"` if the extracted pattern is reasonable, set `status="skip"` if the candidate is too generic / too risky / not worth translating.
- For `status="accepted"`, leave `ja_template=""` (Codex CLI fills it in Step 5) OR fill it manually if you already know the desired form.

- [ ] **Step 5: Re-validate post-review**

```bash
python3.12 scripts/validate_candidate_schema.py \
    scripts/_artifacts/annals/candidates_pending.json
```

Fix any issues until exit 0.

- [ ] **Step 6: Run translate**

```bash
python3.12 scripts/translate_annals_patterns.py \
    scripts/_artifacts/annals/candidates_pending.json
```

Expected: exit 0 (or exit 1 with some `needs_manual` downgrades — review the downgrades).

- [ ] **Step 7: [HUMAN] Review translations**

For each `accepted` candidate, read `ja_template` and adjust if the Japanese is awkward. Re-run validate after edits.

- [ ] **Step 8: Run merge**

```bash
python3.12 scripts/merge_annals_patterns.py \
    scripts/_artifacts/annals/candidates_pending.json
```

Expected: exit 0; `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` exists.

If exit 1 with conflicts: read `scripts/_artifacts/annals/merge_conflicts.json`, choose a resolution per the suggested options, and re-run.

- [ ] **Step 9: Verify the dictionary loads at runtime**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
```

Expected: ALL PASS. The new pattern file is now picked up by the multi-file ordered loader from Task 2.

- [ ] **Step 10: Commit the dictionary**

```bash
git add Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json
git commit -m "$(cat <<'EOF'
feat(420): annals-patterns.ja.json — Sultan Resheph translations

Output of running the issue 420 PR1 pipeline against the 16 Resheph*.cs
source files in XRL.Annals. N accepted candidates merged.

Generated: extract_annals_patterns.py → human review → translate_annals_
patterns.py → merge_annals_patterns.py.

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §6.2
EOF
)"
```

---

### Task 9: L1 tests for the dictionary

**Why now:** the dictionary deliverable from Task 8 exists, so we can exercise it in tests that read the real file.

**Files:**

- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsMarkupInvariantTests.cs`
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsCollisionTests.cs`
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsAssetReachabilityTests.cs`

**Spec references:** §5.3

- [ ] **Step 1: Write AnnalsPatternsAssetReachabilityTests.cs (TDD; smallest)**

Create `Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsAssetReachabilityTests.cs`:

```csharp
using System.IO;
using System.Runtime.Serialization.Json;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class AnnalsPatternsAssetReachabilityTests
{
    private const string AssetRelativePath = "Dictionaries/annals-patterns.ja.json";

    [Test]
    public void ResolveAssetPath_ReturnsExistingFile()
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(AssetRelativePath);
        Assert.That(File.Exists(path), Is.True,
            $"annals-patterns.ja.json must exist at the resolved path: {path}");
    }

    [Test]
    public void AnnalsPatternsFile_HasEntriesArrayAndPatternsArray()
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(AssetRelativePath);
        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocument));
        var document = serializer.ReadObject(stream) as JournalPatternDocument;
        Assert.That(document, Is.Not.Null);
        Assert.That(document!.Patterns, Is.Not.Null,
            "annals-patterns.ja.json must declare a 'patterns' array (may be empty if zero accepted candidates)");
    }
}
```

- [ ] **Step 2: Run it**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~AnnalsPatternsAssetReachabilityTests
```

Expected: PASS (the dictionary from Task 8 should be in place).

- [ ] **Step 3: Write AnnalsPatternsMarkupInvariantTests.cs**

Create `Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsMarkupInvariantTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class AnnalsPatternsMarkupInvariantTests
{
    private static readonly string AssetPath =
        LocalizationAssetResolver.GetLocalizationPath("Dictionaries/annals-patterns.ja.json");

    private static readonly string[] MarkupTokens =
    {
        "&W", "&w", "&G", "&g", "&R", "&r", "&Y", "&y", "&K", "&k",
        "^W", "^w", "^k", "^K",
        "&&", "^^",
    };

    private static readonly Regex CurlyMarkupRe = new(@"\{\{[^|}]+\|[^}]*\}\}", RegexOptions.Compiled);
    private static readonly Regex ColorOpenRe = new(@"<color=#[0-9A-Fa-f]{6,8}>", RegexOptions.Compiled);
    private static readonly Regex ColorCloseRe = new(@"</color>", RegexOptions.Compiled);
    private static readonly Regex EqualsTokenRe = new(@"=[a-zA-Z]+=", RegexOptions.Compiled);
    private static readonly Regex CaptureRefRe = new(@"\{t?\d+\}", RegexOptions.Compiled);

    public static IEnumerable<TestCaseData> AllPatterns()
    {
        if (!File.Exists(AssetPath)) yield break;

        using var stream = File.OpenRead(AssetPath);
        var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocument));
        var document = serializer.ReadObject(stream) as JournalPatternDocument;
        if (document?.Patterns is null) yield break;

        for (var i = 0; i < document.Patterns.Count; i++)
        {
            var p = document.Patterns[i];
            yield return new TestCaseData(p?.Pattern ?? "", p?.Template ?? "")
                .SetName($"AnnalsPattern_{i:D3}");
        }
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void Pattern_CompilesWithoutError(string pattern, string template)
    {
        Assert.DoesNotThrow(() => new Regex(pattern));
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void Template_CaptureReferences_DoNotExceedPatternCaptureCount(string pattern, string template)
    {
        var captureCount = new Regex(pattern).GetGroupNumbers().Length - 1;
        foreach (Match m in CaptureRefRe.Matches(template))
        {
            // Strip {t prefix if present, then } suffix
            var raw = m.Value.TrimStart('{').TrimEnd('}');
            if (raw.StartsWith('t')) raw = raw[1..];
            Assert.That(int.TryParse(raw, out var idx), Is.True, $"unparsable index in {m.Value}");
            Assert.That(idx, Is.LessThan(captureCount),
                $"template references index {idx} which exceeds capture count {captureCount} of pattern {pattern}");
        }
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void MarkupTokens_PresentInPattern_ArePresentInTemplateMultiset(string pattern, string template)
    {
        AssertTokenMultisetParity(pattern, template, MarkupTokens);
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void CurlyMarkupTokens_ArePresentInTemplateMultiset(string pattern, string template)
    {
        var patternHits = CurlyMarkupRe.Matches(pattern).Cast<Match>().Select(m => m.Value).ToList();
        var templateHits = CurlyMarkupRe.Matches(template).Cast<Match>().Select(m => m.Value).ToList();
        AssertMultisetEqual(patternHits, templateHits, "curly markup");
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void ColorTags_BalancedAcrossPatternAndTemplate(string pattern, string template)
    {
        var patternOpens = ColorOpenRe.Matches(pattern).Count;
        var templateOpens = ColorOpenRe.Matches(template).Count;
        var patternCloses = ColorCloseRe.Matches(pattern).Count;
        var templateCloses = ColorCloseRe.Matches(template).Count;
        Assert.That(patternOpens, Is.EqualTo(templateOpens),
            $"<color=...> open-count differs: pattern={patternOpens} template={templateOpens}");
        Assert.That(patternCloses, Is.EqualTo(templateCloses),
            $"</color> close-count differs: pattern={patternCloses} template={templateCloses}");
    }

    [TestCaseSource(nameof(AllPatterns))]
    public void EqualsTokens_ArePresentInTemplateMultiset(string pattern, string template)
    {
        var patternHits = EqualsTokenRe.Matches(pattern).Cast<Match>().Select(m => m.Value).ToList();
        var templateHits = EqualsTokenRe.Matches(template).Cast<Match>().Select(m => m.Value).ToList();
        AssertMultisetEqual(patternHits, templateHits, "=token=");
    }

    private static void AssertTokenMultisetParity(string pattern, string template, IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            var patternCount = CountSubstring(pattern, token);
            var templateCount = CountSubstring(template, token);
            Assert.That(patternCount, Is.EqualTo(templateCount),
                $"token '{token}' multiset parity violated: pattern={patternCount} template={templateCount}");
        }
    }

    private static int CountSubstring(string haystack, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    private static void AssertMultisetEqual(List<string> a, List<string> b, string label)
    {
        var sortedA = a.OrderBy(s => s).ToList();
        var sortedB = b.OrderBy(s => s).ToList();
        Assert.That(sortedA, Is.EqualTo(sortedB), $"{label} multiset differs: a={string.Join(",", sortedA)} b={string.Join(",", sortedB)}");
    }
}
```

- [ ] **Step 4: Run it**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~AnnalsPatternsMarkupInvariantTests
```

Expected: ALL PASS. If any fail, the issue is either in the dictionary content (re-run Task 8 to fix the offending pattern) or in the test logic itself.

- [ ] **Step 5: Write AnnalsPatternsCollisionTests.cs**

Create `Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatternsCollisionTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class AnnalsPatternsCollisionTests
{
    private static readonly string JournalPath =
        LocalizationAssetResolver.GetLocalizationPath("Dictionaries/journal-patterns.ja.json");
    private static readonly string AnnalsPath =
        LocalizationAssetResolver.GetLocalizationPath("Dictionaries/annals-patterns.ja.json");

    private static List<JournalPatternEntry> LoadPatterns(string path)
    {
        if (!File.Exists(path)) return new List<JournalPatternEntry>();
        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocument));
        var document = serializer.ReadObject(stream) as JournalPatternDocument;
        return document?.Patterns ?? new List<JournalPatternEntry>();
    }

    [Test]
    public void NoAnnalsPattern_ExactlyDuplicatesAJournalPattern()
    {
        var journal = LoadPatterns(JournalPath);
        var annals = LoadPatterns(AnnalsPath);
        var journalSet = new HashSet<string>();
        foreach (var p in journal) if (p?.Pattern is not null) journalSet.Add(p.Pattern);

        foreach (var a in annals)
        {
            if (a?.Pattern is null) continue;
            Assert.That(journalSet.Contains(a.Pattern), Is.False,
                $"annals pattern '{a.Pattern}' exact-duplicates a journal pattern; first-match-wins would never reach it.");
        }
    }

    [Test]
    public void NoAnnalsPattern_SwallowedByJournalPattern_ForItsOwnSampleHeuristic()
    {
        // For each annals pattern, build a "literal sample" by replacing each capture group
        // (.+?) / (.+) etc. with a placeholder string and check the journal patterns.
        // PR1 pragmatic approximation: if the annals pattern's literal anchors
        // (the parts between captures) are matched by any journal pattern, flag.
        var journal = LoadPatterns(JournalPath);
        var annals = LoadPatterns(AnnalsPath);
        foreach (var a in annals)
        {
            if (a?.Pattern is null) continue;
            var literalSample = StripCapturesToPlaceholder(a.Pattern, "X");
            foreach (var j in journal)
            {
                if (j?.Pattern is null) continue;
                Regex re;
                try { re = new Regex(j.Pattern); } catch { continue; }
                Assert.That(re.IsMatch(literalSample), Is.False,
                    $"annals pattern '{a.Pattern}' (sample={literalSample}) is swallowed by journal pattern '{j.Pattern}'");
            }
        }
    }

    private static string StripCapturesToPlaceholder(string pattern, string placeholder)
    {
        // Remove anchors and replace any (...) with the placeholder. Crude but adequate for L1.
        var noAnchors = pattern.TrimStart('^').TrimEnd('$');
        return Regex.Replace(noAnchors, @"\([^)]*\)", placeholder);
    }
}
```

- [ ] **Step 6: Run it**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~AnnalsPatternsCollisionTests
```

Expected: PASS. If a collision fires, re-run Task 8 to narrow the offending pattern, or accept that the merge conflict-detector should also have caught it (and re-run merge).

- [ ] **Step 7: Run all L1 tests as a regression check**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
```

Expected: ALL PASS.

- [ ] **Step 8: Commit**

```bash
git add Mods/QudJP/Assemblies/QudJP.Tests/L1/AnnalsPatterns*.cs
git commit -m "$(cat <<'EOF'
test(420): L1 — annals-patterns.ja.json invariants

Three new L1 fixtures covering the dictionary deliverable from Task 8:

- AnnalsPatternsAssetReachabilityTests: file resolves through
  LocalizationAssetResolver and contains valid {entries, patterns}
- AnnalsPatternsMarkupInvariantTests: per-pattern regex compile,
  placeholder index ≤ capture count, multiset parity of &W/^k/{{X|y}}/
  <color=...>/=name= markup tokens between pattern and template
- AnnalsPatternsCollisionTests: no annals pattern is swallowed by an
  existing journal pattern (exact dup or literal-sample swallowed)

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §5.3
EOF
)"
```

---

### Task 10: L2 test for the Resheph translation flow

**Files:**

- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L2/Fixtures/annals-samples.json`
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L2/ReshephHistoryTranslationTests.cs`

**Spec references:** §5.4

- [ ] **Step 1: [HUMAN] Author the L2 sample fixture**

Create `Mods/QudJP/Assemblies/QudJP.Tests/L2/Fixtures/annals-samples.json` with at least one sample per accepted candidate. The exact content depends on the dictionary produced in Task 8, but the schema is fixed:

```json
{
  "schema_version": "1",
  "samples": [
    {
      "candidate_id": "ReshephIsBorn#default",
      "event_property": "gospel",
      "input_source": "Resheph was born in the salt marsh.",
      "expected_japanese_contains": ["レシェフ"]
    }
  ]
}
```

For each accepted candidate:
- `candidate_id`: matches the candidate's id from candidates_pending.json
- `input_source`: a plausible post-HSE-expanded English sentence the candidate's regex should match
- `expected_japanese_contains`: substrings that must appear in the translated output (use proper-noun katakana from glossary; loose substring match)

- [ ] **Step 2: Look at how existing L2 tests construct the dummy walker invocation**

```bash
cat Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricNarrativeTranslationPatchesTests.cs
```

Note the dummy seam pattern.

- [ ] **Step 3: Write the test**

Create `Mods/QudJP/Assemblies/QudJP.Tests/L2/ReshephHistoryTranslationTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ReshephHistoryTranslationTests
{
    private static readonly string FixturePath =
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "annals-samples.json");

    private const string SchemaVersion = "1";

    [DataContract]
    private sealed class SampleEntry
    {
        [DataMember(Name = "candidate_id")]
        public string CandidateId { get; set; } = "";
        [DataMember(Name = "event_property")]
        public string EventProperty { get; set; } = "";
        [DataMember(Name = "input_source")]
        public string InputSource { get; set; } = "";
        [DataMember(Name = "expected_japanese_contains")]
        public List<string> ExpectedJapaneseContains { get; set; } = new();
    }

    [DataContract]
    private sealed class SampleDocument
    {
        [DataMember(Name = "schema_version")]
        public string SchemaVersion { get; set; } = "";
        [DataMember(Name = "samples")]
        public List<SampleEntry> Samples { get; set; } = new();
    }

    private static IEnumerable<TestCaseData> Samples()
    {
        if (!File.Exists(FixturePath)) yield break;
        using var stream = File.OpenRead(FixturePath);
        var serializer = new DataContractJsonSerializer(typeof(SampleDocument));
        var doc = serializer.ReadObject(stream) as SampleDocument;
        if (doc is null || doc.SchemaVersion != SchemaVersion) yield break;
        foreach (var sample in doc.Samples)
        {
            yield return new TestCaseData(sample).SetName($"Reshep_{sample.CandidateId}");
        }
    }

    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
    }

    [TestCaseSource(nameof(Samples))]
    public void TranslateEventPropertiesDict_ProducesExpectedJapanese(SampleEntry sample)
    {
        var dict = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            [sample.EventProperty] = sample.InputSource,
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        var actual = dict[sample.EventProperty];
        foreach (var needle in sample.ExpectedJapaneseContains)
        {
            Assert.That(actual, Does.Contain(needle),
                $"sample {sample.CandidateId}: translated output '{actual}' missing expected substring '{needle}'");
        }
    }
}
```

- [ ] **Step 4: Run the L2 test**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~ReshephHistoryTranslationTests
```

Expected: ALL PASS.

- [ ] **Step 5: Run all L2 tests for regression**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add Mods/QudJP/Assemblies/QudJP.Tests/L2/Fixtures/ Mods/QudJP/Assemblies/QudJP.Tests/L2/ReshephHistoryTranslationTests.cs
git commit -m "$(cat <<'EOF'
test(420): L2 — Resheph history translation per-candidate-id coverage

Fixture-driven L2 test that runs the existing
HistoricNarrativeDictionaryWalker against synthetic post-HSE-expansion
samples and asserts substring presence in the translated Japanese.
Covers the per-candidate-id assertion from spec §5.4 / §6.5.

Substring-set assertion is intentionally loose; exact-string equality is
brittle against future template adjustments.

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §5.4
EOF
)"
```

---

### Task 11: Documentation, .gitignore assertion test, round-trip test, live verification

**Files:**

- Create: `scripts/tests/test_artifact_gitignore.py`
- Create: `scripts/tests/test_pipeline_roundtrip.py`
- Modify: `scripts/AGENTS.md` (or `scripts/README.md` if AGENTS.md does not exist)
- Modify: `Mods/QudJP/Localization/Dictionaries/AGENTS.md` (if it exists; otherwise create `Mods/QudJP/Localization/Dictionaries/README.md`)
- Modify: `CHANGELOG.md`
- Live evidence: screenshot artifact path (out-of-tree)

**Spec references:** §6.5, §6.7

- [ ] **Step 1: Write the gitignore assertion test**

Create `scripts/tests/test_artifact_gitignore.py`:

```python
"""Assert that scripts/_artifacts/annals/ is gitignored."""

from __future__ import annotations

from pathlib import Path


def test_gitignore_lists_annals_artifact_directory() -> None:
    text = Path(".gitignore").read_text(encoding="utf-8")
    assert "scripts/_artifacts/annals/" in text, (
        "scripts/_artifacts/annals/ must be gitignored to prevent accidental "
        "commit of candidate JSON, conflict reports, and .bak backups."
    )
```

- [ ] **Step 2: Run it**

```bash
pytest scripts/tests/test_artifact_gitignore.py -v
```

Expected: PASS (Task 1 already added the entry).

- [ ] **Step 3: Write the pipeline round-trip test**

Create `scripts/tests/test_pipeline_roundtrip.py`:

```python
"""End-to-end round-trip: extract → manually inject ja_template → merge → JSON loadable."""
# ruff: noqa: T201, S603, S607

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

FIXTURES = Path("scripts/tests/fixtures/annals")
ROOT = Path.cwd()


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_pipeline_roundtrip_simple_concat(tmp_path: Path) -> None:
    candidates_path = tmp_path / "candidates.json"
    journal_path = tmp_path / "journal-patterns.ja.json"
    annals_path = tmp_path / "annals.json"
    conflicts_path = tmp_path / "conflicts.json"

    # Seed the journal so collision check has a baseline
    journal_path.write_text(json.dumps({
        "entries": [],
        "patterns": [],
    }), encoding="utf-8")

    # Extract
    result = subprocess.run(
        [
            sys.executable, "scripts/extract_annals_patterns.py",
            "--source-root", str(FIXTURES),
            "--include", "simple_concat.cs",
            "--output", str(candidates_path),
        ],
        capture_output=True, text=True, check=False,
    )
    assert result.returncode == 0, result.stderr

    # Validate
    result = subprocess.run(
        [sys.executable, "scripts/validate_candidate_schema.py", str(candidates_path)],
        capture_output=True, text=True, check=False,
    )
    assert result.returncode == 0, result.stderr

    # Manually inject ja_template (mocking the Codex translate stage)
    doc = json.loads(candidates_path.read_text(encoding="utf-8"))
    for c in doc["candidates"]:
        if c["status"] == "pending":
            c["status"] = "accepted"
            # Use a template referencing only valid capture indices
            captures = c["extracted_pattern"].count("(")
            placeholders = "".join(f"{{t{i}}}" for i in range(captures))
            c["ja_template"] = placeholders + "テスト用日本語"
    candidates_path.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    # Re-validate after edits
    result = subprocess.run(
        [sys.executable, "scripts/validate_candidate_schema.py", str(candidates_path)],
        capture_output=True, text=True, check=False,
    )
    assert result.returncode == 0, result.stderr

    # Merge
    result = subprocess.run(
        [
            sys.executable, "scripts/merge_annals_patterns.py", str(candidates_path),
            "--journal", str(journal_path),
            "--annals-output", str(annals_path),
            "--conflicts-output", str(conflicts_path),
        ],
        capture_output=True, text=True, check=False,
    )
    assert result.returncode == 0, result.stderr

    # Final assertion: the round-tripped output is a valid pattern dictionary
    final = json.loads(annals_path.read_text(encoding="utf-8"))
    assert "entries" in final and final["entries"] == []
    assert "patterns" in final
    assert all("pattern" in p and "template" in p and p.get("route") == "annals" for p in final["patterns"])
```

- [ ] **Step 4: Run it**

```bash
pytest scripts/tests/test_pipeline_roundtrip.py -v
```

Expected: PASS.

- [ ] **Step 5: Update scripts/AGENTS.md (or create scripts/README.md)**

Look for existing operator docs:

```bash
ls scripts/AGENTS.md scripts/README.md 2>&1
```

Append (or create) a section describing the annals pipeline. Use this template:

```markdown
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
```

- [ ] **Step 6: Update Mods/QudJP/Localization/Dictionaries/AGENTS.md (or create README.md)**

```bash
ls Mods/QudJP/Localization/Dictionaries/AGENTS.md Mods/QudJP/Localization/Dictionaries/README.md 2>&1
```

Add a section:

```markdown
## annals-patterns.ja.json

Sibling pattern dictionary to `journal-patterns.ja.json`. Generated by the
issue #420 pipeline (`scripts/extract_annals_patterns.py` →
`validate_candidate_schema.py` → `translate_annals_patterns.py` →
`merge_annals_patterns.py`). Loaded by `JournalPatternTranslator` in the
ordered list `[journal-patterns.ja.json, annals-patterns.ja.json]` —
first-match-wins, so journal patterns take precedence on overlap.

Schema: `{"entries": [], "patterns": [{"pattern", "template", "route":"annals"}]}`.
The `entries` array MUST be present (even if empty) because `Translator`
loads every `Dictionaries/*.ja.json` file and expects that field.

Do NOT hand-edit; re-run the pipeline. Hand-edits will be overwritten
the next time `merge_annals_patterns.py` runs against an updated
candidates JSON.
```

- [ ] **Step 7: Update CHANGELOG.md**

Append to the `## [Unreleased]` section (or add the section if missing):

```markdown
### Added
- Sultan Resheph history translation in `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` (#420 PR1).
- Build-time pipeline at `scripts/{extract,validate,translate,merge}_annals_patterns.py` and `scripts/tools/AnnalsPatternExtractor/` for extracting and translating regex/template pairs from `XRL.Annals/*.cs` (#420 PR1).
- `JournalPatternTranslator` now supports ordered multi-file pattern dictionary load (`SetPatternFilesForTests(params string[])`).
```

- [ ] **Step 8: Run the full test suite**

```bash
ruff check scripts/
pytest scripts/tests/ -v
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj
dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj --configuration Release
```

Expected: all green.

- [ ] **Step 9: [HUMAN] Run the live verification flow**

1. Sync mod to game: `python3.12 scripts/sync_mod.py`
2. Launch Caves of Qud (Apple Silicon: under Rosetta)
3. New character → Joppa world
4. Open the in-game console (typically with `~`) and run wish: `wish reshephgospel`
5. Open Journal → Sultan Histories → Resheph
6. Capture a screenshot showing at least one Resheph gospel line in Japanese
7. Save the screenshot at `~/Library/Logs/Freehold Games/CavesOfQud/issue-420-resheph-evidence.png` (or a path of your choosing)
8. Confirm `~/Library/Logs/Freehold Games/CavesOfQud/Player.log` has no new `spice reference <日本語> wasn't a node` errors

- [ ] **Step 10: Commit docs and tests**

```bash
git add scripts/tests/test_artifact_gitignore.py scripts/tests/test_pipeline_roundtrip.py scripts/AGENTS.md Mods/QudJP/Localization/Dictionaries/AGENTS.md CHANGELOG.md scripts/README.md Mods/QudJP/Localization/Dictionaries/README.md
git commit -m "$(cat <<'EOF'
docs(420): pipeline operator workflow + round-trip test

- scripts/AGENTS.md (or README.md): annals pipeline operator workflow
- Localization/Dictionaries/AGENTS.md (or README.md): annals-patterns
  role, schema constraint (entries required), do-not-hand-edit notice
- CHANGELOG.md Unreleased: Resheph translation entry
- scripts/tests/test_artifact_gitignore.py: regression for .gitignore
  rule (Task 1 added it; this asserts it stays)
- scripts/tests/test_pipeline_roundtrip.py: extract→validate→inject ja_
  template→merge end-to-end against the simple_concat fixture

Live verification: see Task 11 Step 9 of plan; screenshot evidence is
attached in PR description.

Refs: docs/superpowers/specs/2026-04-26-issue-420-hse-pattern-extraction-design.md §6.5, §6.7
EOF
)"
```

---

## Acceptance Criteria Map

Cross-reference of spec §6 to plan tasks:

- §6.1 Pipeline implementation — Tasks 3 (extractor C#) + 4 (extract.py) + 5 (validate.py) + 6 (translate.py) + 7 (merge.py)
- §6.2 Dictionary deliverable — Task 8
- §6.3 Runtime change — Task 2
- §6.4 Tests (Python 7, L1 4, L2 1) — Tasks 3 (test_extract, test_roslyn_smoke), 5 (test_validate), 6 (test_translate), 7 (test_merge), 9 (L1 ×3 — multifile is in Task 2), 10 (L2), 11 (test_artifact_gitignore, test_pipeline_roundtrip). Multi-file L1 test is in Task 2.
- §6.5 Live-runtime verification — Task 11 Step 9
- §6.6 Quality gates — Task 11 Step 8 + CI step from Task 1
- §6.7 Documentation — Task 11 Steps 5–7
- §6.8 Glossary additions — Task 1 Step 5

## Self-Review

**Spec coverage check:** every section of §6 maps to a task above. ✅

**Placeholder scan:** searched for "TBD", "TODO", "implement later", "fill in" — none present. The `[HUMAN]` markers in Task 8 / 11 are deliberate and explicit. ✅

**Type consistency:** the candidate JSON shape, the merge_conflicts shape, and the field names are reproduced verbatim across tasks (no drift between Task 3's C# DTO, Task 5's Python validator, Task 6's translator, and Task 7's merge logic). The `SetPatternFilesForTests(params string[])` and `SetPatternFileForTests(string?)` wrapper is consistent between Task 2's tests and runtime change. The `en_template_hash` payload (`extracted_pattern`/`slots`/`sample_source`/`event_property`/`switch_case`) matches in C# (Task 3) and Python (Task 6). ✅
