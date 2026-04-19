using System.Text;

using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

using QudJP.Patches;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DoesVerbRouteTranslatorPropertyTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private const string ReplaySeed = "741852963,97531";

    private string tempDirectory = null!;
    private string dictionaryPath = null!;
    private string dictionariesPath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-does-route-pbt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryPath = Path.Combine(tempDirectory, "verbs.ja.json");
        dictionariesPath = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionariesPath);
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(dictionaryPath);

        WriteDictionary(
            tier2: new[]
            {
                ("are", "stuck", "動けなくなった"),
                ("are", "exhausted", "疲弊した"),
                ("ask", "about its location and is no longer lost", "{subject}は自分の居場所について尋ね、もう迷っていない"),
                ("ask", "about its location and is no longer stunned", "{subject}は自分の居場所について尋ね、もう気絶していない"),
                ("are", "stunned", "気絶した"),
                ("have", "no room for more water", "これ以上の水を入れる余地がない"),
                ("fall", "to the ground", "地面に倒れた")
            });
    }

    [TearDown]
    public void TearDown()
    {
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(DoesVerbRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateMarkedMessage_TranslatesTailAndNormalizesSubject(DoesVerbMarkedTailCase sample)
    {
        var subjectLength = sample.Fragment.LastIndexOf(' ');
        var marked = DoesVerbRouteTranslator.MarkDoesFragment(sample.Fragment, sample.Verb, subjectLength, null) + sample.Tail;

        var ok = DoesVerbRouteTranslator.TryTranslateMarkedMessage(marked, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(sample.Expected));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(DoesVerbRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePlainSentenceForTests_PrefersEarliestVerbCandidate(DoesVerbEarliestVerbCase sample)
    {
        var ok = DoesVerbRouteTranslator.TryTranslatePlainSentenceForTests(sample.Source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(sample.Expected));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(DoesVerbRouteTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateMarkedMessage_UsesCanonicalMarkerVerb(DoesVerbCanonicalMarkerCase sample)
    {
        var subjectLength = sample.Fragment.LastIndexOf(' ');
        var marked = DoesVerbRouteTranslator.MarkDoesFragment(sample.Fragment, sample.Verb, subjectLength, null) + sample.Tail;

        var ok = DoesVerbRouteTranslator.TryTranslateMarkedMessage(marked, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(sample.Expected));
        });

        return true.ToProperty();
    }

    private void WriteDictionary(
        IEnumerable<(string verb, string text)>? tier1 = null,
        IEnumerable<(string verb, string extra, string text)>? tier2 = null,
        IEnumerable<(string verb, string extra, string text)>? tier3 = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"entries\": [],");
        builder.AppendLine("  \"tier1\": [");
        WriteTier1(builder, tier1);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier2\": [");
        WriteTier2(builder, tier2);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier3\": [");
        WriteTier2(builder, tier3);
        builder.AppendLine("  ]");
        builder.AppendLine("}");

        File.WriteAllText(dictionaryPath, builder.ToString(), Utf8WithoutBom);
    }

    private static void WriteTier1(StringBuilder builder, IEnumerable<(string verb, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static void WriteTier2(StringBuilder builder, IEnumerable<(string verb, string extra, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"extra\": \"")
                .Append(EscapeJson(entry.extra))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
