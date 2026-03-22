using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MessageFrameTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryPath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-message-frame-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryPath = Path.Combine(tempDirectory, "verbs.ja.json");

        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(dictionaryPath);
    }

    [TearDown]
    public void TearDown()
    {
        MessageFrameTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslateXDidY_UsesTier1Verb()
    {
        WriteDictionary(
            tier1: new[] { ("block", "防いだ") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("クマ", "block", extra: null, endMark: ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("クマは防いだ。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_UsesTier2VerbExtraPair()
    {
        WriteDictionary(
            tier2: new[] { ("are", "stunned", "気絶した") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("ゴア", "are", "stunned", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("ゴアは気絶した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_UsesTier3Template()
    {
        WriteDictionary(
            tier3: new[] { ("gain", "{{rules|{0}}} XP", "{{rules|{0}}}XPを獲得した") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("あなた", "gain", "{{rules|150}} XP", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは{{rules|150}}XPを獲得した。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_UsesExactObjectPair()
    {
        WriteDictionary(
            tier2: new[] { ("stare", "at {0} menacingly", "{0}を睨みつけた") });

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ("熊", "stare", "at", "タム", "menacingly", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("熊はタムを睨みつけた。"));
        });
    }

    [Test]
    public void TryTranslateWDidXToYWithZ_UsesTier3TemplateWithTwoObjects()
    {
        WriteDictionary(
            tier3: new[] { ("strike", "{0} with {1} for {2} damage", "{1}で{0}に{2}ダメージを与えた") });

        var translated = MessageFrameTranslator.TryTranslateWDidXToYWithZ(
            "熊",
            "strike",
            directPreposition: null,
            directObject: "スナップジョー",
            indirectPreposition: "with",
            indirectObject: "青銅の短剣",
            extra: "for 5 damage",
            endMark: "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("熊は青銅の短剣でスナップジョーに5ダメージを与えた！"));
        });
    }

    [Test]
    public void TryTranslateWDidXToYWithZ_FallsBackToGenericParticleOrdering()
    {
        WriteDictionary(
            tier1: new[] { ("strike", "攻撃した") });

        var translated = MessageFrameTranslator.TryTranslateWDidXToYWithZ(
            "熊",
            "strike",
            directPreposition: null,
            directObject: "スナップジョー",
            indirectPreposition: "with",
            indirectObject: "青銅の短剣",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("熊はスナップジョーを青銅の短剣で攻撃した。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_ReturnsFalseWhenVerbIsUnknown()
    {
        WriteDictionary(
            tier1: new[] { ("block", "防いだ") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("熊", "teleport", extra: null, endMark: ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(sentence, Is.Empty);
        });
    }

    [Test]
    public void MarkerHelpers_AddAndStripDirectTranslationMarker()
    {
        var marked = MessageFrameTranslator.MarkDirectTranslation("熊は防いだ。");

        var stripped = MessageFrameTranslator.TryStripDirectTranslationMarker(marked, out var unmarked);

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.EqualTo("\u0001熊は防いだ。"));
            Assert.That(stripped, Is.True);
            Assert.That(unmarked, Is.EqualTo("熊は防いだ。"));
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
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
