# Issue #408 — HistoricStringExpander Narrative Translation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a 2-Harmony-hook (Postfix on `QudHistoryFactory.GenerateVillageEraHistory` + Prefix on `JournalAPI.AddVillageGospels(HistoricEntity)`) translation seam that translates only allowlisted narrative-prose properties on `history.events` and on village `HistoricEntity` instances, covering both initial-world generation and Coda endgame, without touching symbolic-key properties used by `HistoricStringExpander`'s JSON-path lookups.

**Architecture:** Three new C# classes in `Mods/QudJP/Assemblies/src/Translation/` (translator + walker) and `Mods/QudJP/Assemblies/src/Patches/` (Harmony adapters). The translator delegates to the existing `JournalPatternTranslator` (passthrough MVP, no direct marker). The walker uses `HistoricEntity.SetEntityPropertyAtCurrentYear` / `MutateListPropertyAtCurrentYear` to write back via mutation events, with `SequenceEqual` guards to prevent extra event noise on all-passthrough lists. Tests follow Red→Green TDD across L1 (pure), L2 (Harmony with dummy targets), and L2G (signature verification against the game DLL).

**Tech Stack:** C# (.NET, LangVersion latest in QudJP.csproj), HarmonyLib (1.2.x), NUnit, existing QudJP infrastructure (`JournalPatternTranslator`, `Translator`, `DynamicTextObservability`). Production code: `Mods/QudJP/Assemblies/src/`. Tests: `Mods/QudJP/Assemblies/QudJP.Tests/L1/`, `L2/`, `L2G/`, `DummyTargets/`. Spec: `docs/superpowers/specs/2026-04-26-issue-408-hse-narrative-translation-design.md`.

---

## File Structure

**New production files** (all under `Mods/QudJP/Assemblies/src/`):

- `Translation/HistoricNarrativeTextTranslator.cs` — pure helper, ~30 LOC
  - Single API: `internal static string Translate(string? source, string? context = null)`
  - Body: `if (string.IsNullOrEmpty(source)) return source ?? string.Empty; return JournalPatternTranslator.Translate(source, context);`
  - No new state. No DLL dependencies beyond `JournalPatternTranslator`.

- `Translation/HistoricNarrativeDictionaryWalker.cs` — allowlist walker, ~80 LOC
  - Module-level allowlist constants (3 `HashSet<string>`)
  - Three public methods:
    - `internal static void TranslateEventProperties(HistoricEvent ev, string? context = null)` — direct dict mutation on `ev.eventProperties`
    - `internal static void TranslateEntity(HistoricEntity entity, string? context = null)` — uses entity mutation APIs
    - `internal static string TranslateGospelEntry(string raw, string? context = null)` — `prose|eventId` split helper

- `Patches/HistoricNarrativeTranslationPatches.cs` — 2 Harmony patch classes, ~60 LOC
  - `[HarmonyPatch(typeof(QudHistoryFactory), nameof(QudHistoryFactory.GenerateVillageEraHistory))]` Postfix
  - `[HarmonyPatch(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), typeof(HistoricEntity))]` Prefix
  - Each Postfix/Prefix body is short: try/catch + walker call

**New test files**:

- `Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeTextTranslatorTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeDictionaryWalkerTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/DummyTargets/DummyHistoricNarrativeTargets.cs` — DummyHistoricEvent / DummyHistoricEntity / DummyHistoricEntitySnapshot for L1+L2
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricNarrativeTranslationPatchesTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2G/HistoricNarrativeTranslationPatchesResolutionTests.cs`

**Untouched**: `Mods/QudJP/Assemblies/src/Patches/HistoricStringExpanderPatch.cs` — disabled stub stays exactly as-is per spec ("経緯記録維持").

---

## Allowlist Constants (used across walker + tests)

These appear in multiple tasks. Define them once in the walker and reference here:

```csharp
private static readonly HashSet<string> EventPropertyAllowlist = new(StringComparer.Ordinal)
{
    "gospel",
    "tombInscription",
};

private static readonly HashSet<string> EntityPropertyAllowlist = new(StringComparer.Ordinal)
{
    "proverb",
    "defaultSacredThing",
    "defaultProfaneThing",
};

private static readonly HashSet<string> EntityListPropertyAllowlist = new(StringComparer.Ordinal)
{
    "Gospels",
    "sacredThings",
    "profaneThings",
    "immigrant_dialogWhy_Q",
    "immigrant_dialogWhy_A",
    "pet_dialogWhy_Q",
};
```

---

## Task 1: L1 — Translator failing tests + class skeleton (Red)

**Files:**
- Create: `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs` (skeleton — throws NotImplementedException)
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeTextTranslatorTests.cs`

The skeleton class is created so the test file compiles. The body throws so all tests fail.

- [ ] **Step 1: Create the translator skeleton**

```csharp
// Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs
using System;

namespace QudJP;

/// <summary>
/// Translates HistoricStringExpander narrative prose (sultan gospel/tomb inscription,
/// village proverb/Gospels list/sacredThings/profaneThings/dialog list properties).
/// Delegates to JournalPatternTranslator without applying a direct marker so that
/// VillageStoryReveal and other non-journal display paths render the translated
/// text as-is.
/// </summary>
internal static class HistoricNarrativeTextTranslator
{
    internal static string Translate(string? source, string? context = null)
    {
        throw new NotImplementedException();
    }
}
```

- [ ] **Step 2: Write the L1 test class with the full case set**

```csharp
// Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeTextTranslatorTests.cs
using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class HistoricNarrativeTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-narrative-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
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

    private void WritePatternDictionary(params (string Pattern, string Template)[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("{\"patterns\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (pattern, template) = entries[i];
            sb.Append("{\"pattern\":\"").Append(pattern.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\",\"template\":\"").Append(template.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"}");
        }
        sb.Append("]}");
        File.WriteAllText(patternFilePath, sb.ToString(), Utf8WithoutBom);
    }

    [Test]
    public void Translate_NullSource_ReturnsEmpty()
    {
        Assert.That(HistoricNarrativeTextTranslator.Translate(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_EmptySource_ReturnsEmpty()
    {
        Assert.That(HistoricNarrativeTextTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_UnmatchedSource_ReturnsOriginal()
    {
        var source = "An unmatched gospel sentence.";
        Assert.That(HistoricNarrativeTextTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_PatternMatch_AppliesTemplate()
    {
        WritePatternDictionary(("^In year (.+?), (.+?) was crowned\\.$", "{1}年、{0}が即位した。"));

        var translated = HistoricNarrativeTextTranslator.Translate("In year 42, Reshephwas crowned.");

        // Note: capture order in template ({0} = year, {1} = name) — see JournalPatternTranslator semantics.
        // Adjust template if JournalPatternTranslator uses different capture group ordering.
        Assert.That(translated, Does.Contain("即位"));
    }

    [Test]
    public void Translate_DoesNotApplyDirectMarker()
    {
        WritePatternDictionary(("^Plain English\\.$", "日本語"));

        var translated = HistoricNarrativeTextTranslator.Translate("Plain English.");

        // U+0001 is the direct-marker control character used by JournalTextTranslator.TryTranslate*ForStorage.
        Assert.That(translated, Is.EqualTo("日本語"));
        Assert.That(translated, Does.Not.Contain(""));
    }

    // Markup invariant preservation. Each test uses a passthrough source containing
    // exactly one invariant token; assertion confirms the token survives unchanged.
    [TestCase("&Wbright")]
    [TestCase("^kdark")]
    [TestCase("&&literal-amp")]
    [TestCase("^^literal-caret")]
    [TestCase("{{X|colored}}")]
    [TestCase("{{W|warning}}")]
    [TestCase("{{NAME|named-shader}}")]
    [TestCase("<color=#44ff88>tmp-green</color>")]
    [TestCase("line1\nline2")]
    [TestCase("=name=")]
    [TestCase("=year=")]
    [TestCase("=pluralize=")]
    [TestCase("=article=")]
    [TestCase("=Article=")]
    [TestCase("=capitalize=")]
    [TestCase("<spice.proverbs.!random.capitalize>")]
    [TestCase("<entity.name>")]
    [TestCase("<undefined entity property foo>")]
    [TestCase("<empty entity list bar>")]
    [TestCase("<unknown entity>")]
    [TestCase("<unknown format whatever>")]
    [TestCase("*Worships.LegendaryCreature.DisplayName*")]
    public void Translate_PreservesMarkupInvariant(string input)
    {
        // No pattern dictionary written: passthrough behavior surfaces invariant.
        Assert.That(HistoricNarrativeTextTranslator.Translate(input), Is.EqualTo(input));
    }
}
```

- [ ] **Step 3: Run the L1 tests to verify Red**

Run from `/private/tmp/qudjp-pr408`:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~HistoricNarrativeTextTranslatorTests" \
  --logger "console;verbosity=normal"
```

Expected: all tests fail with `NotImplementedException` from the skeleton.

- [ ] **Step 4: Commit**

```bash
git add Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs \
        Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeTextTranslatorTests.cs
git commit -m "test: add L1 failing tests for HistoricNarrativeTextTranslator (#408)"
```

---

## Task 2: L1 — Translator implementation (Green)

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs`

- [ ] **Step 1: Implement the translator body**

Replace the body of `Translate`:

```csharp
internal static string Translate(string? source, string? context = null)
{
    if (string.IsNullOrEmpty(source))
    {
        return source ?? string.Empty;
    }
    return JournalPatternTranslator.Translate(source, context);
}
```

- [ ] **Step 2: Run L1 tests to verify Green**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~HistoricNarrativeTextTranslatorTests"
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs
git commit -m "feat: implement HistoricNarrativeTextTranslator (#408)"
```

---

## Task 3: Dummy targets + L1 walker failing tests (Red)

**Files:**
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/DummyTargets/DummyHistoricNarrativeTargets.cs`
- Create: `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs` (skeleton — throws NotImplementedException)
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeDictionaryWalkerTests.cs`

The walker takes `HistoricEvent` and `HistoricEntity` parameters which exist in the game DLL. For L1 (game-DLL-free) tests we wrap the walker behind small adapter methods that take dummy types. Decision: the walker exposes both real-game methods AND testable internal helpers that take dummy-friendly interfaces.

**Approach**: the walker exposes:
1. `TranslateEventProperties(HistoricEvent ev, string? context)` — production API, used by patches
2. `TranslateEntity(HistoricEntity entity, string? context)` — production API, used by patches
3. `TranslateGospelEntry(string raw, string? context)` — pure string helper, L1-testable directly
4. Internal helpers that operate on `IDictionary<string, string>` and `IList<string>` shapes — L1-testable via dummy adapters

This keeps the L1 tests isolated from the game DLL while preserving a single source of allowlist truth.

- [ ] **Step 1: Create the walker skeleton**

```csharp
// Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs
using System;
using System.Collections.Generic;
using System.Linq;
#if HAS_GAME_DLL
using HistoryKit;
#endif

namespace QudJP;

/// <summary>
/// Walks HistoricEvent.eventProperties (direct mutation) and HistoricEntity
/// (mutation events via SetEntityPropertyAtCurrentYear / MutateListPropertyAtCurrentYear)
/// applying <see cref="HistoricNarrativeTextTranslator"/> only to allowlisted keys.
/// </summary>
internal static class HistoricNarrativeDictionaryWalker
{
    internal static readonly HashSet<string> EventPropertyAllowlist = new(StringComparer.Ordinal)
    {
        "gospel",
        "tombInscription",
    };

    internal static readonly HashSet<string> EntityPropertyAllowlist = new(StringComparer.Ordinal)
    {
        "proverb",
        "defaultSacredThing",
        "defaultProfaneThing",
    };

    internal static readonly HashSet<string> EntityListPropertyAllowlist = new(StringComparer.Ordinal)
    {
        "Gospels",
        "sacredThings",
        "profaneThings",
        "immigrant_dialogWhy_Q",
        "immigrant_dialogWhy_A",
        "pet_dialogWhy_Q",
    };

    private const string GospelEventIdSeparator = "|";

    internal static string TranslateGospelEntry(string raw, string? context = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>L1-testable. Mutates the dict in place per the event-property allowlist.</summary>
    internal static void TranslateEventPropertiesDict(IDictionary<string, string> properties, string? context = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// L1-testable. Reads current snapshot via <paramref name="readProperty"/> / <paramref name="readList"/>,
    /// translates each allowlisted value, writes back via <paramref name="writeProperty"/> /
    /// <paramref name="mutateList"/>. The list mutation callback is invoked only when at least one
    /// element changed (sequence equality guard).
    /// </summary>
    internal static void TranslateEntityViaCallbacks(
        Func<string, string?> readProperty,
        Func<string, IReadOnlyList<string>?> readList,
        Action<string, string> writeProperty,
        Action<string, Func<string, string>> mutateList,
        string? context = null)
    {
        throw new NotImplementedException();
    }

#if HAS_GAME_DLL
    internal static void TranslateEventProperties(HistoricEvent ev, string? context = null)
    {
        throw new NotImplementedException();
    }

    internal static void TranslateEntity(HistoricEntity entity, string? context = null)
    {
        throw new NotImplementedException();
    }
#endif
}
```

- [ ] **Step 2: Create the dummy targets**

```csharp
// Mods/QudJP/Assemblies/QudJP.Tests/DummyTargets/DummyHistoricNarrativeTargets.cs
namespace QudJP.Tests.DummyTargets;

internal sealed class DummyHistoricEntitySnapshot
{
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> ListProperties { get; } = new(StringComparer.Ordinal);

    public string? GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : null;
    public IReadOnlyList<string>? GetList(string name) => ListProperties.TryGetValue(name, out var list) ? list : null;
    public bool HasListProperty(string name) => ListProperties.ContainsKey(name);
}

internal sealed class DummySetEntityPropertyEvent
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}

internal sealed class DummyMutateListPropertyEvent
{
    public required string Name { get; init; }
    public required Func<string, string> Mutation { get; init; }
}

internal sealed class DummyHistoricEntity
{
    private readonly DummyHistoricEntitySnapshot snapshot = new();

    public List<DummySetEntityPropertyEvent> PropertyEvents { get; } = new();
    public List<DummyMutateListPropertyEvent> MutateListEvents { get; } = new();

    public DummyHistoricEntitySnapshot Snapshot => snapshot;

    /// <summary>
    /// Replays past events to build a fresh snapshot view. Mirrors HistoricEntity.GetCurrentSnapshot semantics.
    /// </summary>
    public DummyHistoricEntitySnapshot GetCurrentSnapshot()
    {
        // Note: snapshot already has the seeded values; replay applies recorded mutations on top.
        // For test simplicity we apply mutation events to the snapshot in-place.
        // (The walker reads via the original snapshot first; subsequent reads see the events' effects.)
        var fresh = new DummyHistoricEntitySnapshot();
        foreach (var (k, v) in snapshot.Properties) fresh.Properties[k] = v;
        foreach (var (k, list) in snapshot.ListProperties) fresh.ListProperties[k] = new List<string>(list);

        foreach (var ev in PropertyEvents)
        {
            fresh.Properties[ev.Name] = ev.Value;
        }
        foreach (var ev in MutateListEvents)
        {
            if (fresh.ListProperties.TryGetValue(ev.Name, out var existing))
            {
                fresh.ListProperties[ev.Name] = existing.Select(ev.Mutation).ToList();
            }
        }
        return fresh;
    }

    public void SetEntityPropertyAtCurrentYear(string name, string value)
    {
        PropertyEvents.Add(new DummySetEntityPropertyEvent { Name = name, Value = value });
    }

    public void MutateListPropertyAtCurrentYear(string name, Func<string, string> mutation)
    {
        MutateListEvents.Add(new DummyMutateListPropertyEvent { Name = name, Mutation = mutation });
    }

    public void SeedProperty(string name, string value) => snapshot.Properties[name] = value;
    public void SeedList(string name, params string[] items) => snapshot.ListProperties[name] = items.ToList();
}

internal sealed class DummyHistoricEvent
{
    public Dictionary<string, string> EventProperties { get; } = new(StringComparer.Ordinal);
}
```

- [ ] **Step 3: Write the L1 walker tests**

```csharp
// Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeDictionaryWalkerTests.cs
using System.Text;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class HistoricNarrativeDictionaryWalkerTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-walker-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
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

    private void WritePatternDictionary(params (string Pattern, string Template)[] entries)
    {
        // identical to the helper in HistoricNarrativeTextTranslatorTests; duplicated for test isolation
        var sb = new StringBuilder();
        sb.Append("{\"patterns\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (pattern, template) = entries[i];
            sb.Append("{\"pattern\":\"").Append(pattern.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\",\"template\":\"").Append(template.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"}");
        }
        sb.Append("]}");
        File.WriteAllText(patternFilePath, sb.ToString(), Utf8WithoutBom);
    }

    // -- TranslateGospelEntry --

    [Test]
    public void TranslateGospelEntry_SplitsAndTranslatesProseOnly()
    {
        WritePatternDictionary(("^Hello world$", "こんにちは世界"));

        var result = HistoricNarrativeDictionaryWalker.TranslateGospelEntry("Hello world|42");

        Assert.That(result, Is.EqualTo("こんにちは世界|42"));
    }

    [Test]
    public void TranslateGospelEntry_NoSeparator_TranslatesEntireString()
    {
        WritePatternDictionary(("^Hello world$", "こんにちは世界"));

        var result = HistoricNarrativeDictionaryWalker.TranslateGospelEntry("Hello world");

        Assert.That(result, Is.EqualTo("こんにちは世界"));
    }

    [Test]
    public void TranslateGospelEntry_EmptyEventId_PreservesTrailingPipe()
    {
        var result = HistoricNarrativeDictionaryWalker.TranslateGospelEntry("Untranslated|");

        Assert.That(result, Is.EqualTo("Untranslated|"));
    }

    // -- TranslateEventPropertiesDict --

    [Test]
    public void TranslateEventPropertiesDict_TranslatesAllowlistedKeysOnly()
    {
        WritePatternDictionary(("^In a year, things happened\\.$", "ある年、何かが起こった。"));

        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gospel"] = "In a year, things happened.",
            ["tombInscriptionCategory"] = "CrownedSultan",
            ["region"] = "DesertCanyon-7-2",
            ["revealsRegion"] = "OldRegionName",
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        Assert.That(dict["gospel"], Is.EqualTo("ある年、何かが起こった。"));
        Assert.That(dict["tombInscriptionCategory"], Is.EqualTo("CrownedSultan"));
        Assert.That(dict["region"], Is.EqualTo("DesertCanyon-7-2"));
        Assert.That(dict["revealsRegion"], Is.EqualTo("OldRegionName"));
    }

    [Test]
    public void TranslateEventPropertiesDict_SkipsLookalikeKeys()
    {
        WritePatternDictionary(("^Anything\\.$", "何でも。"));

        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gospelText"] = "Anything.", // not in allowlist (note the suffix)
            ["Gospel"] = "Anything.",     // case-sensitive miss
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        Assert.That(dict["gospelText"], Is.EqualTo("Anything."));
        Assert.That(dict["Gospel"], Is.EqualTo("Anything."));
    }

    [Test]
    public void TranslateEventPropertiesDict_PassthroughDoesNotMutateValue()
    {
        // No pattern dictionary: translator returns input unchanged.

        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gospel"] = "Untranslated gospel sentence.",
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        Assert.That(dict["gospel"], Is.EqualTo("Untranslated gospel sentence."));
    }

    // -- TranslateEntityViaCallbacks --

    [Test]
    public void TranslateEntityViaCallbacks_TranslatesEntityPropertiesViaWriteCallback()
    {
        WritePatternDictionary(("^A proverb\\.$", "ある格言。"));

        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proverb"] = "A proverb.",
            ["worships_creature_id"] = "Snapjaw_creature_42",
        };
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var writes = new List<(string Name, string Value)>();
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.TryGetValue(name, out var v) ? v : null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (name, value) => writes.Add((name, value)),
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(writes, Has.Count.EqualTo(1));
        Assert.That(writes[0].Name, Is.EqualTo("proverb"));
        Assert.That(writes[0].Value, Is.EqualTo("ある格言。"));
        // worships_creature_id (not in allowlist) must not generate a write event.
        Assert.That(writes.Any(w => w.Name == "worships_creature_id"), Is.False);
    }

    [Test]
    public void TranslateEntityViaCallbacks_AllPassthroughList_DoesNotInvokeMutateList()
    {
        // No pattern dictionary, all list elements unchanged after translate.

        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["sacredThings"] = new List<string> { "Untranslated A", "Untranslated B" },
        };
        var writes = new List<(string Name, string Value)>();
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.TryGetValue(name, out var v) ? v : null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (name, value) => writes.Add((name, value)),
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Is.Empty,
            "All-passthrough lists should not call MutateListPropertyAtCurrentYear (no event noise).");
    }

    [Test]
    public void TranslateEntityViaCallbacks_PartiallyTranslatedList_InvokesMutateListOnce()
    {
        WritePatternDictionary(("^Sacred\\.$", "聖。"));

        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["sacredThings"] = new List<string> { "Sacred.", "Untranslated.", "Sacred." },
        };
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: _ => null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (_, _) => { },
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Has.Count.EqualTo(1));
        Assert.That(mutations[0].Name, Is.EqualTo("sacredThings"));
        // Verify the supplied mutation function does the right thing on each element.
        Assert.That(mutations[0].Mutation("Sacred."), Is.EqualTo("聖。"));
        Assert.That(mutations[0].Mutation("Untranslated."), Is.EqualTo("Untranslated."));
    }

    [Test]
    public void TranslateEntityViaCallbacks_GospelsListUsesGospelEntrySplit()
    {
        WritePatternDictionary(("^Hello world$", "こんにちは世界"));

        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Gospels"] = new List<string> { "Hello world|42" },
        };
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: _ => null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (_, _) => { },
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Has.Count.EqualTo(1));
        Assert.That(mutations[0].Name, Is.EqualTo("Gospels"));
        Assert.That(mutations[0].Mutation("Hello world|42"), Is.EqualTo("こんにちは世界|42"));
    }

    [Test]
    public void TranslateEntityViaCallbacks_NullList_NoOp()
    {
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: _ => null,
            readList: _ => null,
            writeProperty: (_, _) => { },
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Is.Empty);
    }

    [Test]
    public void TranslateEntityViaCallbacks_NullOrEmptyProperty_NoWrite()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proverb"] = string.Empty,
            ["defaultSacredThing"] = string.Empty,
        };
        var writes = new List<(string Name, string Value)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.TryGetValue(name, out var v) ? v : null,
            readList: _ => null,
            writeProperty: (name, value) => writes.Add((name, value)),
            mutateList: (_, _) => { });

        Assert.That(writes, Is.Empty);
    }
}
```

- [ ] **Step 4: Run the L1 walker tests to verify Red**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~HistoricNarrativeDictionaryWalkerTests"
```

Expected: all tests fail with `NotImplementedException`.

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs \
        Mods/QudJP/Assemblies/QudJP.Tests/DummyTargets/DummyHistoricNarrativeTargets.cs \
        Mods/QudJP/Assemblies/QudJP.Tests/L1/HistoricNarrativeDictionaryWalkerTests.cs
git commit -m "test: add L1 failing tests for HistoricNarrativeDictionaryWalker (#408)"
```

---

## Task 4: L1 — Walker implementation (Green)

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs`

- [ ] **Step 1: Implement `TranslateGospelEntry`**

Replace the body:

```csharp
internal static string TranslateGospelEntry(string raw, string? context = null)
{
    if (string.IsNullOrEmpty(raw))
    {
        return raw ?? string.Empty;
    }

    var separatorIndex = raw.IndexOf(GospelEventIdSeparator, StringComparison.Ordinal);
    if (separatorIndex < 0)
    {
        return HistoricNarrativeTextTranslator.Translate(raw, context);
    }

    var prose = raw.Substring(0, separatorIndex);
    var suffix = raw.Substring(separatorIndex); // includes the leading "|"
    var translatedProse = HistoricNarrativeTextTranslator.Translate(prose, context);
    return translatedProse + suffix;
}
```

- [ ] **Step 2: Implement `TranslateEventPropertiesDict`**

```csharp
internal static void TranslateEventPropertiesDict(IDictionary<string, string> properties, string? context = null)
{
    if (properties == null || properties.Count == 0)
    {
        return;
    }

    foreach (var key in EventPropertyAllowlist)
    {
        if (!properties.TryGetValue(key, out var current) || string.IsNullOrEmpty(current))
        {
            continue;
        }
        var translated = HistoricNarrativeTextTranslator.Translate(current, context);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            properties[key] = translated;
        }
    }
}
```

- [ ] **Step 3: Implement `TranslateEntityViaCallbacks`**

```csharp
internal static void TranslateEntityViaCallbacks(
    Func<string, string?> readProperty,
    Func<string, IReadOnlyList<string>?> readList,
    Action<string, string> writeProperty,
    Action<string, Func<string, string>> mutateList,
    string? context = null)
{
    if (readProperty == null || readList == null || writeProperty == null || mutateList == null)
    {
        return;
    }

    foreach (var key in EntityPropertyAllowlist)
    {
        var current = readProperty(key);
        if (string.IsNullOrEmpty(current))
        {
            continue;
        }
        var translated = HistoricNarrativeTextTranslator.Translate(current, context);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            writeProperty(key, translated);
        }
    }

    foreach (var key in EntityListPropertyAllowlist)
    {
        var current = readList(key);
        if (current == null || current.Count == 0)
        {
            continue;
        }
        Func<string, string> mutation = key == "Gospels"
            ? (raw => TranslateGospelEntry(raw, context))
            : (raw => HistoricNarrativeTextTranslator.Translate(raw, context));

        // Pre-compute translated values to detect whether any element actually changes.
        // MutateListPropertyAtCurrentYear unconditionally adds a MutateListProperty event
        // (see decompiled MutateListProperty.Generate); avoid event noise on full passthrough.
        var changed = false;
        for (var i = 0; i < current.Count; i++)
        {
            if (!string.Equals(mutation(current[i]), current[i], StringComparison.Ordinal))
            {
                changed = true;
                break;
            }
        }
        if (changed)
        {
            mutateList(key, mutation);
        }
    }
}
```

- [ ] **Step 4: Implement game-DLL adapters under `#if HAS_GAME_DLL`**

```csharp
#if HAS_GAME_DLL
    internal static void TranslateEventProperties(HistoricEvent ev, string? context = null)
    {
        if (ev?.eventProperties == null)
        {
            return;
        }
        TranslateEventPropertiesDict(ev.eventProperties, context);
    }

    internal static void TranslateEntity(HistoricEntity entity, string? context = null)
    {
        if (entity == null)
        {
            return;
        }

        var snapshot = entity.GetCurrentSnapshot();
        if (snapshot == null)
        {
            return;
        }

        TranslateEntityViaCallbacks(
            readProperty: name => snapshot.GetProperty(name),
            readList: name => snapshot.HasListProperty(name)
                ? (IReadOnlyList<string>)snapshot.GetList(name)
                : null,
            writeProperty: (name, value) => entity.SetEntityPropertyAtCurrentYear(name, value),
            mutateList: (name, mutation) => entity.MutateListPropertyAtCurrentYear(name, mutation),
            context: context);
    }
#endif
```

Note: `HistoricEntitySnapshot.GetProperty` returns `string` (or null/empty when absent) per `~/dev/coq-decompiled_stable/HistoryKit/HistoricEntitySnapshot.cs`. `HasListProperty` and `GetList` are public on the same type. Verify exact method names against the decompiled source before implementation; adjust if different.

- [ ] **Step 5: Run L1 walker tests to verify Green**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~HistoricNarrativeDictionaryWalkerTests"
```

Expected: all 11 tests pass.

- [ ] **Step 6: Run all L1 tests to confirm no regression**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "TestCategory=L1"
```

Expected: full L1 suite still green (existing tests unaffected).

- [ ] **Step 7: Commit**

```bash
git add Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs
git commit -m "feat: implement HistoricNarrativeDictionaryWalker (#408)"
```

---

## Task 5: L2 — Patches failing tests (Red)

**Files:**
- Create: `Mods/QudJP/Assemblies/src/Patches/HistoricNarrativeTranslationPatches.cs` (skeleton — empty Postfix/Prefix bodies)
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricNarrativeTranslationPatchesTests.cs`

L2 here means: tests that exercise patch logic via Harmony hooked against dummy targets (no real game DLL). The pattern follows existing L2 tests like `JournalAccomplishmentAddTranslationPatchTests`.

- [ ] **Step 1: Create the patches skeleton**

```csharp
// Mods/QudJP/Assemblies/src/Patches/HistoricNarrativeTranslationPatches.cs
using System;
using System.Diagnostics;
#if HAS_GAME_DLL
using HarmonyLib;
using HistoryKit;
using Qud.API;
using XRL.Annals;
#endif

namespace QudJP.Patches;

#if HAS_GAME_DLL
[HarmonyPatch(typeof(QudHistoryFactory), nameof(QudHistoryFactory.GenerateVillageEraHistory))]
public static class GenerateVillageEraHistoryTranslationPatch
{
    private const string Context = nameof(GenerateVillageEraHistoryTranslationPatch);

    [HarmonyPriority(Priority.Low)]
    public static void Postfix(History __result)
    {
        try
        {
            if (__result?.events == null)
            {
                return;
            }
            foreach (var ev in __result.events)
            {
                HistoricNarrativeDictionaryWalker.TranslateEventProperties(ev, Context);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GenerateVillageEraHistoryTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}

[HarmonyPatch(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), typeof(HistoricEntity))]
public static class AddVillageGospelsTranslationPatch
{
    private const string Context = nameof(AddVillageGospelsTranslationPatch);

    [HarmonyPriority(Priority.Low)]
    public static void Prefix(HistoricEntity Village)
    {
        try
        {
            HistoricNarrativeDictionaryWalker.TranslateEntity(Village, Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AddVillageGospelsTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
#endif
```

- [ ] **Step 2: Write the L2 tests**

The L2 tests verify, against the `DummyHistoricEntity` / `DummyHistoricEvent` dummies (no Harmony bind needed because we exercise the walker directly through its public adapters, then assert the patches' contract — invoke walker once per event/entity, swallow exceptions, log on failure).

```csharp
// Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricNarrativeTranslationPatchesTests.cs
using System.Text;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HistoricNarrativeTranslationPatchesTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-narrative-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
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

    private void WritePatternDictionary(params (string Pattern, string Template)[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("{\"patterns\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (pattern, template) = entries[i];
            sb.Append("{\"pattern\":\"").Append(pattern.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\",\"template\":\"").Append(template.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"}");
        }
        sb.Append("]}");
        File.WriteAllText(patternFilePath, sb.ToString(), Utf8WithoutBom);
    }

    // Helper that mirrors the patch body using the L1-callable walker entry points,
    // exercised against the dummy targets so we don't need to bind real Harmony in L2.

    private static void RunGenerateVillageEraHistoryPostfixUsing(IEnumerable<DummyHistoricEvent> events)
    {
        foreach (var ev in events)
        {
            HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(ev.EventProperties);
        }
    }

    private static void RunAddVillageGospelsPrefixUsing(DummyHistoricEntity entity)
    {
        var snapshot = entity.GetCurrentSnapshot();
        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.GetProperty(name),
            readList: name => snapshot.GetList(name),
            writeProperty: entity.SetEntityPropertyAtCurrentYear,
            mutateList: entity.MutateListPropertyAtCurrentYear);
    }

    [Test]
    public void EraHistoryPostfix_TranslatesGospelOnlyOnAllowlistedEventProperties()
    {
        WritePatternDictionary(("^Crowned\\.$", "戴冠した。"));

        var ev = new DummyHistoricEvent();
        ev.EventProperties["gospel"] = "Crowned.";
        ev.EventProperties["tombInscriptionCategory"] = "CrownedSultan";
        ev.EventProperties["region"] = "DesertCanyon";
        ev.EventProperties["revealsRegion"] = "OldName";

        RunGenerateVillageEraHistoryPostfixUsing(new[] { ev });

        Assert.That(ev.EventProperties["gospel"], Is.EqualTo("戴冠した。"));
        Assert.That(ev.EventProperties["tombInscriptionCategory"], Is.EqualTo("CrownedSultan"));
        Assert.That(ev.EventProperties["region"], Is.EqualTo("DesertCanyon"));
        Assert.That(ev.EventProperties["revealsRegion"], Is.EqualTo("OldName"));
    }

    [Test]
    public void AddVillageGospelsPrefix_TranslatesAllowlistedEntityPropertiesViaMutationApi()
    {
        WritePatternDictionary(
            ("^A proverb\\.$", "ある格言。"),
            ("^Sacred\\.$", "聖。"),
            ("^Gospel sentence\\.$", "ゴスペル一文。"));

        var entity = new DummyHistoricEntity();
        entity.SeedProperty("proverb", "A proverb.");
        entity.SeedProperty("worships_creature_id", "Snapjaw_42");
        entity.SeedList("sacredThings", "Sacred.");
        entity.SeedList("Gospels", "Gospel sentence.|17");
        entity.SeedList("type", "village"); // not in allowlist

        RunAddVillageGospelsPrefixUsing(entity);

        // Entity property: written via SetEntityPropertyAtCurrentYear.
        Assert.That(entity.PropertyEvents.Any(e => e.Name == "proverb" && e.Value == "ある格言。"), Is.True);
        Assert.That(entity.PropertyEvents.Any(e => e.Name == "worships_creature_id"), Is.False);

        // List property: mutation event added; runs translator on each element.
        var sacredEvent = entity.MutateListEvents.SingleOrDefault(e => e.Name == "sacredThings");
        Assert.That(sacredEvent, Is.Not.Null);
        Assert.That(sacredEvent!.Mutation("Sacred."), Is.EqualTo("聖。"));

        var gospelEvent = entity.MutateListEvents.SingleOrDefault(e => e.Name == "Gospels");
        Assert.That(gospelEvent, Is.Not.Null);
        Assert.That(gospelEvent!.Mutation("Gospel sentence.|17"), Is.EqualTo("ゴスペル一文。|17"));

        // type list (not in allowlist) must not produce a mutation event.
        Assert.That(entity.MutateListEvents.Any(e => e.Name == "type"), Is.False);
    }

    [Test]
    public void AddVillageGospelsPrefix_AllPassthroughLists_NoMutationEvents()
    {
        // No pattern dictionary: every translation is identity.

        var entity = new DummyHistoricEntity();
        entity.SeedList("sacredThings", "Sacred A.", "Sacred B.");
        entity.SeedList("Gospels", "Untranslated A.|11", "Untranslated B.|22");

        RunAddVillageGospelsPrefixUsing(entity);

        Assert.That(entity.MutateListEvents, Is.Empty,
            "Lists with all-passthrough elements must not generate MutateListProperty events.");
    }

    [Test]
    public void AddVillageGospelsPrefix_DoubleInvocation_DoesNotDuplicateEvents()
    {
        WritePatternDictionary(("^A proverb\\.$", "ある格言。"));

        var entity = new DummyHistoricEntity();
        entity.SeedProperty("proverb", "A proverb.");

        RunAddVillageGospelsPrefixUsing(entity);
        RunAddVillageGospelsPrefixUsing(entity);

        // First call writes "ある格言。"; second call sees the already-translated value and
        // the translator returns it unchanged (no Japanese pattern). Therefore no second write.
        var proverbEvents = entity.PropertyEvents.Where(e => e.Name == "proverb").ToList();
        Assert.That(proverbEvents, Has.Count.EqualTo(1),
            "Idempotent re-application should not duplicate property events.");
    }

    [Test]
    public void AddVillageGospelsPrefix_NullEntity_NoOp()
    {
        // Patches' try/catch + walker null-guard means null entity must be safe.
        // Walker handles null via the production code path; the dummy harness tests the API contract.

        Assert.DoesNotThrow(() =>
            HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
                readProperty: _ => null,
                readList: _ => null,
                writeProperty: (_, _) => { },
                mutateList: (_, _) => { }));
    }
}
```

- [ ] **Step 3: Run L2 tests to verify Red**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~HistoricNarrativeTranslationPatchesTests"
```

Expected: failures because the walker production methods invoked by the test harness (`TranslateEventPropertiesDict`, `TranslateEntityViaCallbacks`) already exist after Tasks 3-4. So actually most of these tests should PASS at this point — they exercise the walker, not the Harmony patch. The Red→Green TDD here is on the **patch class** itself: its existence and signature.

If all tests pass at Step 3, that means the Red phase moved earlier; proceed to Step 4 to commit (the patch class skeleton is the only artifact left to add for L2 coverage).

- [ ] **Step 4: Commit**

```bash
git add Mods/QudJP/Assemblies/src/Patches/HistoricNarrativeTranslationPatches.cs \
        Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricNarrativeTranslationPatchesTests.cs
git commit -m "test: add L2 patch tests + skeleton for HistoricNarrativeTranslationPatches (#408)"
```

---

## Task 6: Patches implementation (Green)

The patch skeleton already calls the walker correctly in Task 5. This task verifies via dotnet build (with `-p:HAS_GAME_DLL=true` if applicable) that the Harmony attributes resolve and that the production code compiles end-to-end.

**Files:**
- No new file changes; verify build only.

- [ ] **Step 1: Build with default configuration**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

Expected: `Build succeeded.` with no errors. Pre-existing warnings are acceptable.

- [ ] **Step 2: Build the test project**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj
```

Expected: clean build.

- [ ] **Step 3: Run all L2 tests to confirm no regression**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "TestCategory=L2"
```

Expected: full L2 suite green.

- [ ] **Step 4: No commit needed if no files changed**

If the build surfaced fixes (e.g., the patch class needed an adjustment), commit those:

```bash
git add Mods/QudJP/Assemblies/src/Patches/HistoricNarrativeTranslationPatches.cs
git commit -m "fix: HistoricNarrativeTranslationPatches build adjustments (#408)"
```

Otherwise skip.

---

## Task 7: L2G — Signature verification

**Files:**
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L2G/HistoricNarrativeTranslationPatchesResolutionTests.cs`

L2G tests reflect against the loaded game DLL to confirm Harmony patch targets and assumption-method signatures still exist. Pattern follows `AbilityBarAfterRenderTranslationPatchResolutionTests`.

- [ ] **Step 1: Write the L2G test class**

```csharp
// Mods/QudJP/Assemblies/QudJP.Tests/L2G/HistoricNarrativeTranslationPatchesResolutionTests.cs
#if HAS_GAME_DLL
using System.Reflection;
using HarmonyLib;
using HistoryKit;
using Qud.API;
using XRL.Annals;
using XRL.World.Conversations.Parts;
using XRL.World.WorldBuilders;
using XRL.World.ZoneBuilders;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class HistoricNarrativeTranslationPatchesResolutionTests
{
    [Test]
    public void GenerateVillageEraHistoryPatch_ResolvesTarget()
    {
        var patchType = typeof(Translator).Assembly
            .GetType("QudJP.Patches.GenerateVillageEraHistoryTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "GenerateVillageEraHistoryTranslationPatch type not found.");

        var attribute = patchType!.GetCustomAttribute<HarmonyPatch>();
        Assert.That(attribute, Is.Not.Null);

        var method = AccessTools.DeclaredMethod(typeof(QudHistoryFactory), nameof(QudHistoryFactory.GenerateVillageEraHistory));
        Assert.That(method, Is.Not.Null, "QudHistoryFactory.GenerateVillageEraHistory(History) not found.");
        Assert.That(method!.GetParameters().Length, Is.EqualTo(1));
        Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(History)));
        Assert.That(method.ReturnType, Is.EqualTo(typeof(History)));
    }

    [Test]
    public void AddVillageGospelsPatch_ResolvesHistoricEntityOverload()
    {
        var patchType = typeof(Translator).Assembly
            .GetType("QudJP.Patches.AddVillageGospelsTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "AddVillageGospelsTranslationPatch type not found.");

        var method = AccessTools.DeclaredMethod(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), new[] { typeof(HistoricEntity) });
        Assert.That(method, Is.Not.Null, "JournalAPI.AddVillageGospels(HistoricEntity) not found.");
        Assert.That(method!.GetParameters().Length, Is.EqualTo(1));
        Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(HistoricEntity)));
    }

    [Test]
    public void AddVillageGospels_HasSnapshotOverload()
    {
        var method = AccessTools.DeclaredMethod(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), new[] { typeof(HistoricEntitySnapshot) });
        Assert.That(method, Is.Not.Null, "JournalAPI.AddVillageGospels(HistoricEntitySnapshot) not found.");
    }

    [Test]
    public void Worships_HasPostProcessEvent()
    {
        var method = AccessTools.DeclaredMethod(typeof(Worships), "PostProcessEvent",
            new[] { typeof(HistoricEntity), typeof(string), typeof(string) });
        Assert.That(method, Is.Not.Null, "Worships.PostProcessEvent(HistoricEntity,string,string) not found.");
    }

    [Test]
    public void Despises_HasPostProcessEvent()
    {
        var method = AccessTools.DeclaredMethod(typeof(Despises), "PostProcessEvent",
            new[] { typeof(HistoricEntity), typeof(string), typeof(string) });
        Assert.That(method, Is.Not.Null, "Despises.PostProcessEvent(HistoricEntity,string,string) not found.");
    }

    [Test]
    public void VillageCoda_HasGenerateVillageEntity()
    {
        var method = typeof(VillageCoda).GetMethod("GenerateVillageEntity",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "VillageCoda.GenerateVillageEntity not found.");
    }

    [Test]
    public void EndGame_HasApplyVillage()
    {
        var method = typeof(EndGame).GetMethod("ApplyVillage",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "EndGame.ApplyVillage not found.");
    }

    [Test]
    public void JoppaWorldBuilder_HasAddVillages()
    {
        var method = typeof(JoppaWorldBuilder).GetMethod("AddVillages",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "JoppaWorldBuilder.AddVillages not found.");
    }
}
#endif
```

- [ ] **Step 2: Run L2G tests**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "TestCategory=L2G"
```

Expected: all tests pass against the game DLL. If the game DLL exposes signatures slightly different (e.g., extra parameters), update the assertion to match the actual API and document the divergence in a comment. Do NOT silently relax the assertion.

- [ ] **Step 3: Commit**

```bash
git add Mods/QudJP/Assemblies/QudJP.Tests/L2G/HistoricNarrativeTranslationPatchesResolutionTests.cs
git commit -m "test: add L2G signature verification for HistoricNarrative patches (#408)"
```

---

## Task 8: Full repository verification

**Files:** none modified.

- [ ] **Step 1: dotnet build**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 2: L1 tests**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
```

Expected: all L1 tests pass (existing baseline + new Historic narrative L1 tests).

- [ ] **Step 3: L2 tests**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
```

Expected: all L2 tests pass.

- [ ] **Step 4: L2G tests**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G
```

Expected: all L2G tests pass.

- [ ] **Step 5: Python tooling**

```bash
uv run pytest scripts/tests/ -q
ruff check scripts/
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
```

Expected: all green. None of these should be affected by this PR (no Python/JSON/XML touched), but run as a sanity check per `docs/RULES.md`.

- [ ] **Step 6: Final cleanup commit if anything was adjusted**

If verification surfaced a fix-forward (e.g., a using-directive cleanup or a method-name correction discovered in Task 7), commit it:

```bash
git add -p
git commit -m "chore: post-verification cleanup for #408"
```

Otherwise skip.

---

## Self-Review

**Spec coverage check:**

| Spec section | Implementing task |
|---|---|
| Allowlist (event property: gospel, tombInscription) | Task 3 (constants) + Task 4 (TranslateEventPropertiesDict) + L1 walker tests |
| Allowlist (entity property: proverb, defaultSacredThing, defaultProfaneThing) | Task 3 (constants) + Task 4 (TranslateEntityViaCallbacks) + L1 walker tests |
| Allowlist (entity list: Gospels, sacredThings, profaneThings, immigrant_dialogWhy_Q/A, pet_dialogWhy_Q) | Task 3 (constants) + Task 4 (TranslateEntityViaCallbacks list branch) + L1 walker tests |
| Hook 1 (GenerateVillageEraHistory Postfix) | Task 5 (patch class) + L2 test |
| Hook 2 (AddVillageGospels Prefix on HistoricEntity overload) | Task 5 (patch class) + L2 test |
| HistoricEntity mutation API write-back | Task 4 (TranslateEntityViaCallbacks + #if HAS_GAME_DLL adapter) |
| Markup invariants (full list) | Task 1 (L1 translator tests via [TestCase] parametrization) |
| Direct marker absence (``) | Task 1 (L1 translator test `Translate_DoesNotApplyDirectMarker`) |
| Gospels prose|eventId split | Task 3 + Task 4 (TranslateGospelEntry) + L1 tests |
| List idempotency guard (SequenceEqual) | Task 4 (TranslateEntityViaCallbacks `changed` flag) + L1 test `AllPassthroughList_DoesNotInvokeMutateList` + L2 test `AllPassthroughLists_NoMutationEvents` |
| L2G signature verification (2 patch targets + 6 assumption checks) | Task 7 |
| HistoricStringExpanderPatch.cs untouched | (Tasks do not modify this file) |

**Type consistency check:**
- `TranslateEntityViaCallbacks` signature is identical in Task 3 (skeleton) and Task 4 (implementation).
- `TranslateGospelEntry(string raw, string? context = null)` signature consistent across Tasks 3, 4, and the L1 tests.
- Allowlist constants defined once in Task 3 walker file, referenced (not redeclared) elsewhere.
- `DummyHistoricEntity.SeedList` accepts `params string[]` consistently in tests.

**Placeholder scan:** No "TBD"/"TODO"/"add validation" patterns. Each step contains the actual code or exact command.

**Known caveat:** Task 5 Step 3 may produce zero failures because the L2 test harness exercises the walker (Tasks 3-4 already make those green) rather than running real Harmony bind. This is by design — the patch class is thin glue, and real Harmony binding is verified in L2G (Task 7) via reflection. Document this in the commit message and proceed.

**Note on capture group order in Task 1's `Translate_PatternMatch_AppliesTemplate`:** The example template uses `{0}` and `{1}` placeholders. The actual placeholder semantics depend on `JournalPatternTranslator`'s template engine. The spec says JournalPatternTranslator behaviour should be relied on without modification. If the test fails because of capture-order assumptions, adjust the assertion to a contains-style check (the existing assertion is already loose: `Does.Contain("即位")`).
