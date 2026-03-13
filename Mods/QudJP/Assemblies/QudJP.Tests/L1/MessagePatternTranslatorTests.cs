using System.Runtime.Serialization;
using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MessagePatternTranslatorTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-message-pattern-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Translate_AppliesSingleCapturePattern()
    {
        WritePatternDictionary(("^You miss (.+?)[.!]?$", "{0}への攻撃をはずした"));

        var translated = MessagePatternTranslator.Translate("You miss snapjaw.");

        Assert.That(translated, Is.EqualTo("snapjawへの攻撃をはずした"));
    }

    [Test]
    public void Translate_AppliesMultipleCapturePattern()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{0}に{1}ダメージを与えた"));

        var translated = MessagePatternTranslator.Translate("You hit glowfish for 12 damage!");

        Assert.That(translated, Is.EqualTo("glowfishに12ダメージを与えた"));
    }

    [Test]
    public void Translate_SupportsPlaceholderReordering()
    {
        WritePatternDictionary(("^(.+?) gives you (.+?)[.!]?$", "{1}を{0}から受け取った"));

        var translated = MessagePatternTranslator.Translate("warden gives you brass key.");

        Assert.That(translated, Is.EqualTo("brass keyをwardenから受け取った"));
    }

    [Test]
    public void Translate_UsesFirstMatchingPattern_WhenMultiplePatternsMatch()
    {
        WritePatternDictionary(
            ("^You hit (.+) for (\\d+) damage[.!]?$", "FIRST:{0}:{1}"),
            ("^You hit (.+) for (\\d+) damage[.!]?$", "SECOND:{0}:{1}"));

        var translated = MessagePatternTranslator.Translate("You hit goatfolk for 3 damage.");

        Assert.That(translated, Is.EqualTo("FIRST:goatfolk:3"));
    }

    [Test]
    public void Translate_HandlesPatternWithEscapedRegexSymbols()
    {
        WritePatternDictionary(("^You use \\((.+)\\)\\.$", "{0}を使用した"));

        var translated = MessagePatternTranslator.Translate("You use (phase cannon).");

        Assert.That(translated, Is.EqualTo("phase cannonを使用した"));
    }

    [Test]
    public void Translate_HandlesOptionalPunctuation()
    {
        WritePatternDictionary(("^You are stunned[.!]?$", "あなたは朦朧としている"));

        var first = MessagePatternTranslator.Translate("You are stunned");
        var second = MessagePatternTranslator.Translate("You are stunned!");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("あなたは朦朧としている"));
            Assert.That(second, Is.EqualTo("あなたは朦朧としている"));
        });
    }

    [Test]
    public void Translate_PreservesBraceColorMarkup()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{0}に{1}ダメージを与えた"));

        var translated = MessagePatternTranslator.Translate("{{W|You hit snapjaw for 7 damage}}!");

        Assert.That(translated, Is.EqualTo("{{W|snapjawに7ダメージを与えた}}"));
    }

    [Test]
    public void Translate_PreservesAmpersandAndCaretColorCodes()
    {
        WritePatternDictionary(("^You stop moving[.!]?$", "あなたは移動を止めた"));

        var translated = MessagePatternTranslator.Translate("&GYou stop moving^k.");

        Assert.That(translated, Is.EqualTo("&Gあなたは移動を止めた^k"));
    }

    [Test]
    public void Translate_ReturnsOriginal_WhenPatternDoesNotMatch()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));

        var translated = MessagePatternTranslator.Translate("You begin moving.");

        Assert.That(translated, Is.EqualTo("You begin moving."));
    }

    [Test]
    public void Translate_LogsContext_WhenPatternDoesNotMatch()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));
        using var writer = new StringWriter();
        using var listener = new System.Diagnostics.TextWriterTraceListener(writer);
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            var translated = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");

            listener.Flush();
            var output = writer.ToString();

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo("You begin moving."));
                Assert.That(output, Does.Contain("no pattern for 'You begin moving.'"));
                Assert.That(output, Does.Contain("context: MessageLogPatch"));
            });
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    [Test]
    public void Translate_ReturnsEmptyString_WhenInputIsNull()
    {
        WritePatternDictionary(("^You die![.!]?$", "あなたは死んだ！"));

        var translated = MessagePatternTranslator.Translate(null);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_ReturnsEmptyString_WhenInputIsEmpty()
    {
        WritePatternDictionary(("^You die![.!]?$", "あなたは死んだ！"));

        var translated = MessagePatternTranslator.Translate(string.Empty);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_LoadsPatternFileOnlyOnce_WhenCalledRepeatedly()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "あなたは{0}を聞いた"));

        var first = MessagePatternTranslator.Translate("You hear thunder.");
        var second = MessagePatternTranslator.Translate("You hear thunder.");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("あなたはthunderを聞いた"));
            Assert.That(second, Is.EqualTo("あなたはthunderを聞いた"));
            Assert.That(MessagePatternTranslator.LoadInvocationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenPatternFileMissing()
    {
        MessagePatternTranslator.SetPatternFileForTests(Path.Combine(tempDirectory, "missing-messages.ja.json"));

        Assert.Throws<FileNotFoundException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    [Test]
    public void Translate_ThrowsSerializationException_WhenPatternJsonIsCorrupt()
    {
        WriteRawPatternFile("{\"patterns\":[{\"pattern\":\"^You miss (.+)$\",\"template\":\"{0}\"}");

        Assert.Throws<SerializationException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenPatternsArrayIsMissing()
    {
        WriteRawPatternFile("{}");

        Assert.Throws<InvalidDataException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenPatternEntryIsMalformed()
    {
        WriteRawPatternFile("{\"patterns\":[{\"pattern\":\"^You miss (.+)$\"}]}");

        Assert.Throws<InvalidDataException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

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

    private void WriteRawPatternFile(string json)
    {
        File.WriteAllText(patternFilePath, json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
