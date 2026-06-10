using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class UITextSkinMixedDisplayNameSinkTextTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-uitextskin-mixed-display-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        ScopedDictionaryLookup.ResetForTests();
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

    [TestCase("{{Y-Y-Y-G-Y-Y-g-Y sequence|Chain of the Analog Sand}} 発見", true)]
    [TestCase("発見した Chain \u00040 \t0", true)]
    [TestCase("発見した Chain [1 lbs.]", true)]
    [TestCase("発見した Chain [1 kg]", true)]
    [TestCase("発見した Chain", false)]
    [TestCase("Chain \u00040 \t0", false)]
    [TestCase("発見した \u00040 \t0", false)]
    [TestCase("これは normal message です", false)]
    public void LooksLikeMixedDisplayNameSinkTextForTests_RequiresJapaneseEnglishAndDisplayMarkers(
        string source,
        bool expected)
    {
        Assert.That(UITextSkinTranslationPatch.LooksLikeMixedDisplayNameSinkTextForTests(source), Is.EqualTo(expected));
    }

    [TestCase("発見した Chain [1 lbs.]", true)]
    [TestCase("発見した Chain [1 kg]", true)]
    [TestCase("発見した Chain [\u00040]", true)]
    [TestCase("発見した Chain [\t0]", true)]
    [TestCase("発見した Chain", false)]
    public void LooksLikeInventoryDisplayNameLineForTests_DetectsInventorySuffixMarkers(string source, bool expected)
    {
        Assert.That(UITextSkinTranslationPatch.LooksLikeInventoryDisplayNameLineForTests(source), Is.EqualTo(expected));
    }

    [Test]
    public void ContainsCharacterForTests_DetectsControlCharacters()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.ContainsCharacterForTests("発見した Chain \u00040", '\u0004'), Is.True);
            Assert.That(UITextSkinTranslationPatch.ContainsCharacterForTests("発見した Chain \u00040", '\t'), Is.False);
        });
    }

    [Test]
    public void TryTranslateMixedDisplayNameSinkTextForTests_TranslatesValidMixedDisplayName()
    {
        Directory.CreateDirectory(Path.Combine(tempDirectory, "Scoped"));
        WriteContextDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("analog", null, "アナログの"));

        var source = "{{Y-Y-Y-G-Y-Y-g-Y sequence|Chain of the Analog Sand}} \u00040 \t0 [6ドラムのゲル]";
        var translated = UITextSkinTranslationPatch.TryTranslateMixedDisplayNameSinkTextForTests(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y-Y-Y-G-Y-Y-g-Y sequence|アナログの砂の鎖}} {{b|\u0004}}0 {{K|\t}}0 [6ドラムのゲル]"));
        });
    }

    [Test]
    public void TryTranslateMixedDisplayNameSinkTextForTests_RejectsNormalMixedLanguageMessage()
    {
        var source = "これは normal message です";
        var translated = UITextSkinTranslationPatch.TryTranslateMixedDisplayNameSinkTextForTests(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateMixedDisplayNameSinkTextForTests_RejectsMarkedNonDisplayNameAfterHeuristic()
    {
        var source = "これは Chain sequence| のログです";

        Assert.That(UITextSkinTranslationPatch.LooksLikeMixedDisplayNameSinkTextForTests(source), Is.True);
        var translated = UITextSkinTranslationPatch.TryTranslateMixedDisplayNameSinkTextForTests(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
        });
    }

    private void WriteContextDictionaryFile(
        string fileName,
        params (string key, string? context, string text)[] entries)
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
            builder.Append('"');
            if (!string.IsNullOrWhiteSpace(entries[index].context))
            {
                builder.Append(",\"context\":\"");
                builder.Append(EscapeJson(entries[index].context!));
                builder.Append('"');
            }

            builder.Append(",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ScopedDictionaryLookup.ResetForTests();
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
