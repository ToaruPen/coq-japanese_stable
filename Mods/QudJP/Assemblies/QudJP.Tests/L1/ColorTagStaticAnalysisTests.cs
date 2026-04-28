using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ColorTagStaticAnalysisTests
{
    private const string ColorTaggedDisplayNameKey = "bloody Tam, dromad merchant [sitting]";
    private const string ColorTaggedDisplayNameText = "{{r|血まみれの}}タム、ドロマド商団 [座っている]";

    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-color-tag-static-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        WritePatternDictionary();
        WriteExactDictionary(CommonDeathEntries());
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void DeathPopup_DoesNotAccumulateNestedRedWrappers_OnAlreadyLocalizedKiller()
    {
        WriteDeathDictionaryWithLocalizedKiller();

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You died.\n\nYou were killed by {{r|bloody}} Tam, dromad merchant [sitting].");

        var translated = DeathWrapperFamilyTranslator.TryTranslatePopup(
            stripped,
            spans,
            nameof(PopupTranslationPatch),
            out var popupTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                popupTranslated,
                Is.EqualTo("あなたは死んだ。\n\n{{r|血まみれの}}タム、ドロマド商団 [座っている]に殺された。"));
            Assert.That(popupTranslated, Does.Not.Contain("{{r|{{r|"));
            Assert.That(popupTranslated, Does.Not.Match("\\[座ってい}}る\\]"));
        });
    }

    [Test]
    public void DeathReason_AmpersandCode_DoesNotSplitBracketedJapaneseToken()
    {
        WriteDeathDictionaryWithLocalizedKiller();

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by &rbloody Tam, dromad merchant [sitting]&y.");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(messageTranslated, Does.Contain("血まみれの").And.Contain("殺された"));
            Assert.That(messageTranslated, Does.Not.Match("\\[座ってい&[a-zA-Z]る\\]"));
        });
    }

    [Test]
    public void RestoreCapture_DoesNotInjectCloseTagInsideTranslatedDisplayName()
    {
        WriteDeathDictionaryWithLocalizedKiller();

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by {{C|bloody Tam, dromad merchant [sitting]}}.");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                messageTranslated,
                Is.EqualTo("{{C|{{r|血まみれの}}タム、ドロマド商団 [座っている]}}に殺された。"));
            Assert.That(messageTranslated, Does.Not.Contain("{{C|{{C|"));
            Assert.That(messageTranslated, Does.Not.Match("\\[座ってい}}る\\]"));
        });
    }

    [Test]
    public void Translator_IsIdempotent_OnAlreadyLocalizedColoredDisplayName()
    {
        var first = ColorAwareTranslationComposer.TranslatePreservingColors(ColorTaggedDisplayNameText);
        var second = ColorAwareTranslationComposer.TranslatePreservingColors(first);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(ColorTaggedDisplayNameText));
            Assert.That(second, Is.EqualTo(first));
        });
    }

    [Test]
    public void MessagePattern_CaptureRestoration_DoesNotDoubleWrapMarkupCarryingCapture()
    {
        WritePatternDictionary(("^You see (.+?)[.!]?$", "{t0}が見える。"));
        WriteExactDictionary((ColorTaggedDisplayNameKey, ColorTaggedDisplayNameText));

        var translated = MessagePatternTranslator.Translate(
            "You see {{r|bloody}} Tam, dromad merchant [sitting].");

        Assert.Multiple(() =>
        {
            Assert.That(
                translated,
                Is.EqualTo("{{r|血まみれの}}タム、ドロマド商団 [座っている]が見える。"));
            Assert.That(translated, Does.Not.Contain("{{r|{{r|"));
            Assert.That(translated, Does.Not.Match("\\[座ってい}}る\\]"));
        });
    }

    [Test]
    public void DescriptionText_BalancedCapture_DoesNotSpliceColorAcrossSentences()
    {
        WriteExactDictionary(("stealing", "盗み"));

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "{{W|Hated by the bandits for stealing.}}\nIt is dangerous.",
            nameof(DescriptionLongDescriptionPatch));

        Assert.That(
            translated,
            Is.EqualTo("{{W|the banditsに憎まれている。理由: 盗み。}}\nIt is dangerous."));
    }

    private static IEnumerable<(string key, string text)> CommonDeathEntries()
    {
        return
        [
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.KilledBy.Bare", "{killer}に殺された。"),
        ];
    }

    private void WriteDeathDictionaryWithLocalizedKiller()
    {
        WriteExactDictionary(CommonDeathEntries().Append((ColorTaggedDisplayNameKey, ColorTaggedDisplayNameText)));
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"patterns\":[");
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

        builder.AppendLine("]}");
        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    private void WriteExactDictionary(IEnumerable<(string key, string text)> entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        var first = true;
        foreach (var (key, text) in entries)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }

        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(dictionaryDirectory, "color-static-l1.ja.json"), builder.ToString(), Utf8WithoutBom);
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        WriteExactDictionary((IEnumerable<(string key, string text)>)entries);
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
