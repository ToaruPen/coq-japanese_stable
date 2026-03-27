using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class MessageLogProducerTranslationHelpersTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-message-log-producer-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesCompositeComponents()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "Joppa, surface",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("ジョッパ, 地表"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesStrataTemplate()
    {
        WriteExactDictionary(
            ("a rusted archway", "錆びたアーチ道"),
            ("{0} strata deep", "地下{0}層"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "a rusted archway, 2 strata deep",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("錆びたアーチ道, 地下2層"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_ReturnsFalseForEmptyInput()
    {
        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            string.Empty,
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_PreservesColorTags()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "<color=#ff0>Joppa</color>, <color=#0f0>surface</color>",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("<color=#ff0>ジョッパ</color>, <color=#0f0>地表</color>"));
        });
    }

    [Test]
    public void PreparePassByMessage_MarksTranslatedMessage()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var result = MessageLogProducerTranslationHelpers.PreparePassByMessage(
            "You pass by ウォーターヴァイン.",
            "PhysicsEnterCellPassByTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001ウォーターヴァインのそばを通り過ぎた。"));
    }

    [Test]
    public void PreparePassByMessage_PreservesColorTags()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var result = MessageLogProducerTranslationHelpers.PreparePassByMessage(
            "You pass by <color=#ff0>ウォーターヴァイン</color>.",
            "PhysicsEnterCellPassByTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001<color=#ff0>ウォーターヴァイン</color>のそばを通り過ぎた。"));
    }

    [Test]
    public void PreparePassByMessage_PreservesDirectTranslationMarker()
    {
        const string source = "\u0001You pass by ウォーターヴァイン.";

        var result = MessageLogProducerTranslationHelpers.PreparePassByMessage(
            source,
            "PhysicsEnterCellPassByTranslationPatch");

        Assert.That(result, Is.EqualTo(source));
    }

    [Test]
    public void PrepareZoneBannerMessage_MarksAlreadyLocalizedBanner()
    {
        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            "ジョッパ, 地表, 06:00",
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001ジョッパ, 地表, 06:00"));
    }

    [Test]
    public void PrepareZoneBannerMessage_ReturnsEmptyInputUnchanged()
    {
        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            string.Empty,
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void PrepareZoneBannerMessage_PreservesColorTags()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));

        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            "<color=#ff0>Joppa</color>, <color=#0f0>surface</color>, 06:00",
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001<color=#ff0>ジョッパ</color>, <color=#0f0>地表</color>, 06:00"));
    }

    [Test]
    public void PrepareZoneBannerMessage_PreservesDirectTranslationMarker()
    {
        const string source = "\u0001ジョッパ, 地表, 06:00";

        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            source,
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo(source));
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

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
            Path.Combine(dictionaryDirectory, "ui-message-log-producer-l1.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WritePatternDictionary(params (string pattern, string template)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(entries[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(entries[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
