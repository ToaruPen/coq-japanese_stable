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

    [TestCase("The 技師 deploys a タレット.", "技師はタレットを展開した")]
    [TestCase("{{g|The 技師 deploys a タレット.}}", "{{g|技師はタレットを展開した}}")]
    public void TryTranslatePlainSentence_RepositoryDictionary_TranslatesDeployFamily(string source, string expected)
    {
        UseRepositoryDictionary();

        var ok = DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("The 技師 readies a タレット.")]
    [TestCase("{{g|The 技師 readies a タレット.}}")]
    public void TryTranslatePlainSentence_RepositoryDictionary_ReturnsFalse_ForDeployFallbacks(string source)
    {
        UseRepositoryDictionary();

        var ok = DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslatePlainSentence_RepositoryDictionary_ReturnsFalse_ForEmptyDeployInput()
    {
        UseRepositoryDictionary();

        var ok = DoesVerbRouteTranslator.TryTranslatePlainSentence(string.Empty, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslatePlainSentence_RepositoryDictionary_ReturnsFalse_ForDirectMarkedDeployOutput()
    {
        UseRepositoryDictionary();
        const string source = "\u0001技師はタレットを展開した";

        var ok = DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [TestCase("The 熊 is", "are", " stunned!", "熊は気絶した！")]
    [TestCase("The 扉 is", "are", " open.", "扉は開いている")]
    [TestCase("The 水筒 is", "are", " already full.", "水筒はすでに満タンだ")]
    [TestCase("The 武器 is", "are", " already fully loaded.", "武器はすでに完全に装填されている")]
    [TestCase("The 熊 falls", "fall", " to the ground.", "熊は地面に倒れた。")]
    [TestCase("The 熊 returns", "return", " to the ground.", "熊は地上に戻った")]
    [TestCase("The 水筒 has", "have", " no room for more water.", "水筒にはこれ以上の水を入れる余地がない。")]
    [TestCase(
        "The リコイラー is",
        "are",
        " encoded with an imprint of the Thin World that has no meaning in the Thick World.",
        "リコイラーは薄界の刻印で符号化されているが、厚界では意味を成さない")]
    [TestCase(
        "The リコイラー is",
        "are",
        " encoded with an imprint that has no meaning in your present context.",
        "リコイラーは現在の状況では意味を成さない刻印で符号化されている")]
    [TestCase(
        "The リコイラー is",
        "are",
        " encoded with the imprint of a remote pocket dimension, 秘境, that is inaccessible from your present context.",
        "リコイラーは遠方のポケット次元秘境の刻印で符号化されているが、現在の状況ではアクセスできない")]
    public void TryTranslateMarkedMessage_RepositoryDictionary_TranslatesVerifiedDoesFamilies(
        string fragment,
        string verb,
        string tail,
        string expected)
    {
        UseRepositoryDictionary();
        var subjectLength = fragment.LastIndexOf(' ');
        var marked = DoesVerbRouteTranslator.MarkDoesFragment(fragment, verb, subjectLength, null) + tail;

        var ok = DoesVerbRouteTranslator.TryTranslateMarkedMessage(marked, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
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

    private static void UseRepositoryDictionary()
    {
        var repositoryDictionaryPath = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));

        MessageFrameTranslator.SetDictionaryPathForTests(repositoryDictionaryPath);
    }
}
