using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class JournalEntryDisplayTextPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-journal-entry-l2", Guid.NewGuid().ToString("N"));
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

    [Test]
    public void Postfix_TranslatesAccomplishmentLeaf_WhenPatched()
    {
        WriteExactDictionary(("You contracted glotrot.", "舌腐病に罹患した。"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalAccomplishment
            {
                Category = "general",
                Text = "You contracted glotrot.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("舌腐病に罹患した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesAccomplishmentPattern_WhenPatched()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalAccomplishment
            {
                Category = "general",
                Text = "You journeyed to Kyakukya.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("キャクキャに旅した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_SkipsPlayerChronologyEntries_WhenPatched()
    {
        WriteExactDictionary(("You contracted glotrot.", "舌腐病に罹患した。"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalAccomplishment
            {
                Category = "player",
                Text = "You contracted glotrot.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("You contracted glotrot."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_SkipsGeneralNotes_WhenPatched()
    {
        WriteExactDictionary(("You contracted glotrot.", "舌腐病に罹患した。"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalGeneralNote
            {
                Text = "You contracted glotrot.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("You contracted glotrot."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesMapNoteLeaf_WhenPatched()
    {
        WriteExactDictionary(("A {{w|dromad}} caravan", "{{w|ドロマド}}の隊商"));
        WritePatternDictionary(("^Last visited on the (.+?) of (.+?)$", "{1}の{0}日に最後に訪れた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalMapNote), nameof(DummyJournalMapNote.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteDisplayTextPatch), nameof(JournalMapNoteDisplayTextPatch.Postfix))));

            var entry = new DummyJournalMapNote
            {
                Category = "Merchants",
                Text = "A {{w|dromad}} caravan\nLast visited on the 5th of Ut yara Ux",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("{{w|ドロマド}}の隊商\nUt yara Uxの5th日に最後に訪れた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_SkipsPlayerMapNotes_WhenPatched()
    {
        WriteExactDictionary(("A {{w|dromad}} caravan", "{{w|ドロマド}}の隊商"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalMapNote), nameof(DummyJournalMapNote.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteDisplayTextPatch), nameof(JournalMapNoteDisplayTextPatch.Postfix))));

            var entry = new DummyJournalMapNote
            {
                Category = "Miscellaneous",
                Text = "A {{w|dromad}} caravan",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("A {{w|dromad}} caravan"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("journal-entry-l2.ja.json", entries);
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[],\"patterns\":[");
        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteDictionaryFile(string fileName, (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(dictionaryDirectory, fileName),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
