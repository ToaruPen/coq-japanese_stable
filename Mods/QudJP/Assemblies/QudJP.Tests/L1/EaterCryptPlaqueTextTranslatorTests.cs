using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class EaterCryptPlaqueTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-eater-crypt-plaque-l1", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(Path.Combine(dictionaryDirectory, "Scoped"));

        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        WriteHistorySpiceDictionary(
            ("family", "家"),
            ("kinfolk", "一族の者たち"),
            ("learned", "学識ある"),
            ("kindred", "同族"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase("Here Rests *familyCognomen*", "ここに眠る *familyCognomen*")]
    [TestCase("Here Rest the tutors of", "ここに眠る 師の")]
    [TestCase("Inside Lies the learned tutors of", "ここに眠る 学識ある師の")]
    [TestCase("Sheltered Here under Gjaus are the kindred of", "ジャウスの庇護の下、ここに眠る 同族の")]
    [TestCase("The Family of *familyName*", "*familyName*の家")]
    [TestCase("The *familyName* Family", "*familyName*の家")]
    [TestCase("The kinfolk of *familyName*", "*familyName*の一族の者たち")]
    [TestCase("the learned tutors of", "学識ある師の")]
    [TestCase("Wisdom *markovSeed:is*", "知恵、*shortMarkov*")]
    [TestCase("Only the wise know *markovSeed:what*", "賢き者だけが*shortMarkov*を知る")]
    [TestCase("Knowledge, quills, and *markovSeed:a*", "知識、羽ペン、そして*shortMarkov*")]
    [TestCase("Question *markovSeed:the*", "*shortMarkov*を問え")]
    [TestCase("Bravery *markovSeed:is*", "勇敢、*shortMarkov*")]
    [TestCase("approach death *markovSeed:with*", "死に立ち向かえ、*shortMarkov*")]
    [TestCase("godliness *markovSeed:is*", "信心、*shortMarkov*")]
    [TestCase("love the sultan, *markovSeed:for,so,because*", "スルタンを愛せよ、*shortMarkov*")]
    [TestCase("voice a prayer *markovSeed:for*", "祈りを唱えよ、*shortMarkov*")]
    [TestCase("Our sultan *markovSeed:is*", "われらのスルタン、*shortMarkov*")]
    [TestCase("We *markovSeed:are,do,see,feel,know,have,say,go,take*", "われらは、*shortMarkov*")]
    [TestCase("hark! *shortMarkov*", "聞け！ *shortMarkov*")]
    [TestCase("Hark! *shortMarkov*", "聞け！ *shortMarkov*")]
    public void TryTranslate_TranslatesCryptPlaqueExpandedFragment(string source, string expected)
    {
        var ok = EaterCryptPlaqueTextTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorBoundary()
    {
        var ok = EaterCryptPlaqueTextTranslator.TryTranslate(
            "{{M|Here Rests *familyCognomen*}}",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{M|ここに眠る *familyCognomen*}}"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("Plain plaque text.")]
    [TestCase("The UnknownTerm of *familyName*")]
    public void TryTranslate_LeavesUnknownEmptyAndDirectText(string? source)
    {
        var ok = EaterCryptPlaqueTextTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source ?? string.Empty));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var ok = EaterCryptPlaqueTextTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "Here Rests *familyCognomen*",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("Here Rests *familyCognomen*"));
        });
    }

    private void WriteHistorySpiceDictionary(params (string key, string text)[] entries)
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
            Path.Combine(dictionaryDirectory, "Scoped", "historyspice-common.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
