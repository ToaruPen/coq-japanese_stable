using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class JournalApiAddTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-journal-api-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        DummyJournalApi.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyJournalApi.Reset();
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesStoredTexts_WhenPatched()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(
            ("^You journeyed to (.+?)\\.$", "{t0}に旅した。"),
            ("^Notes: (.+)$", "備考: {t0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "You journeyed to Kyakukya.",
                "Notes: Kyakukya",
                "Notes: Kyakukya",
                category: "general");

            var entry = DummyJournalApi.Accomplishments.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001キャクキャに旅した。"));
                Assert.That(entry.MuralText, Is.EqualTo("\u0001備考: キャクキャ"));
                Assert.That(entry.GospelText, Is.EqualTo("\u0001備考: キャクキャ"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddMapNote_TranslatesText_WhenPatched()
    {
        WritePatternDictionary(("^A \"SATED\" baetyl$", "「満足した」ベテル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddMapNote)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteAddTranslationPatch), nameof(JournalMapNoteAddTranslationPatch.Prefix))));

            DummyJournalApi.AddMapNote("Joppa.1.1.1.1.10", "A \"SATED\" baetyl", "Baetyls");

            Assert.That(
                DummyJournalApi.MapNotes.Single().Text,
                Is.EqualTo("\u0001「満足した」ベテル"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddMapNote_SkipsMiscellaneousCategory_WhenPatched()
    {
        WritePatternDictionary(("^A \"SATED\" baetyl$", "「満足した」ベテル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddMapNote)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteAddTranslationPatch), nameof(JournalMapNoteAddTranslationPatch.Prefix))));

            DummyJournalApi.AddMapNote("Joppa.1.1.1.1.10", "A \"SATED\" baetyl", "Miscellaneous");

            Assert.That(
                DummyJournalApi.MapNotes.Single().Text,
                Is.EqualTo("A \"SATED\" baetyl"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddObservation_TranslatesTextAndRevealText_WhenPatched()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddObservation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalObservationAddTranslationPatch), nameof(JournalObservationAddTranslationPatch.Prefix))));

            DummyJournalApi.AddObservation(
                "You journeyed to Kyakukya.",
                "obs-1",
                "general",
                additionalRevealText: "You journeyed to Kyakukya.");

            var entry = DummyJournalApi.Observations.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001キャクキャに旅した。"));
                Assert.That(entry.RevealText, Is.EqualTo("\u0001キャクキャに旅した。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddObservation_TranslatesHistoricGossip_WhenPatched()
    {
        WriteExactDictionary(("some organization", "ある組織"), ("some party", "ある一団"));
        WritePatternDictionary(("^(.+?) repeatedly beat (.+?) at dice\\.$", "{t0}は{t1}を何度も賽子で打ち負かした。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddObservation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalObservationAddTranslationPatch), nameof(JournalObservationAddTranslationPatch.Prefix))));

            DummyJournalApi.AddObservation(
                "some organization repeatedly beat some party at dice.",
                "gossip-1",
                "general",
                additionalRevealText: "some organization repeatedly beat some party at dice.");

            var entry = DummyJournalApi.Observations.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001ある組織はある一団を何度も賽子で打ち負かした。"));
                Assert.That(entry.RevealText, Is.EqualTo("\u0001ある組織はある一団を何度も賽子で打ち負かした。"));
            });
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
        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
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
            Path.Combine(dictionaryDirectory, "journal-api-l2.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
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
