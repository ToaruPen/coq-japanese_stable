using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DoesVerbRouteTranslatorTests
{
    private string tempDirectory = null!;
    private string dictionaryPath = null!;
    private string dictionariesPath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-does-route", Guid.NewGuid().ToString("N"));
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

    [Test]
    public void TryTranslateMarkedMessage_TranslatesTail()
    {
        WriteDictionary(tier2: new[] { ("are", "stuck", "動けなくなった") });
        var marked = DoesVerbRouteTranslator.MarkDoesFragment("The 熊 are", "are", "The 熊".Length, null) + " stuck.";

        var ok = DoesVerbRouteTranslator.TryTranslateMarkedMessage(marked, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("熊は動けなくなった。"));
        });
    }

    [Test]
    public void TryTranslatePlainSentenceForTests_FallsBackToEarliestVerb()
    {
        WriteDictionary(tier2: new[]
        {
            ("ask", "about its location and is no longer lost", "{subject}は自分の居場所について尋ね、もう迷っていない"),
            ("are", "no longer lost", "lost状態ではなくなった")
        });

        var ok = DoesVerbRouteTranslator.TryTranslatePlainSentenceForTests(
            "The 熊 asks about its location and is no longer lost.",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("熊は自分の居場所について尋ね、もう迷っていない"));
        });
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

        File.WriteAllText(dictionaryPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
