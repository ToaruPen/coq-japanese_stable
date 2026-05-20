using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class FriendOrFoeReasonTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-friend-foe-reason-l1", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslate_UsesExistingWorldPartsExactReason()
    {
        WriteDictionaryFile("world-parts.ja.json", ("stealing a cherished heirloom", "大切にしていた家宝を盗んだ"));

        var ok = FriendOrFoeReasonTranslator.TryTranslate("stealing a cherished heirloom", out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("大切にしていた家宝を盗んだ"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesExpandedNormalReasonCapture()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("suns", "太陽"));

        var ok = FriendOrFoeReasonTranslator.TryTranslate("insulting their suns", out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("太陽を侮辱した"));
        });
    }

    [TestCase("inventing the irrational numbers", "無理数を発明した")]
    [TestCase("destroying the shining numbers", "輝く数を破壊した")]
    [TestCase("dreaming an uncharted dimension into being", "an uncharted dimensionを夢見て存在させた")]
    [TestCase("inventing the concept of moons", "月という概念を発明した")]
    [TestCase("swapping how moons and suns are perceived", "月と太陽の知覚のされ方を入れ替えた")]
    [TestCase("warping a pocket of spacetime into crystal caves", "結晶洞へと時空の小片を歪めた")]
    public void TryTranslate_TranslatesHebReasonFrames(string source, string expected)
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("shining", "輝く"),
            ("moons", "月"),
            ("suns", "太陽"),
            ("crystal caves", "結晶洞"));

        var ok = FriendOrFoeReasonTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesColorBoundaryWrappers()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("suns", "太陽"));

        var ok = FriendOrFoeReasonTranslator.TryTranslate("{{Y|praising their suns}}", out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{Y|太陽を称賛した}}"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerBeforeTranslating()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("suns", "太陽"));

        var ok = FriendOrFoeReasonTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "praising their suns",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("太陽を称賛した"));
        });
    }

    [Test]
    public void TryTranslate_LeavesUnknownReasonUnchanged()
    {
        const string source = "for reasons no one can parse";

        var ok = FriendOrFoeReasonTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    private void WriteDictionaryFile(string fileName, params (string Key, string Text)[] entries)
    {
        var path = Path.Combine(dictionaryDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].Key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].Text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(path, builder.ToString(), Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}
