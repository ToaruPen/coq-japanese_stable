using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GetDisplayNameRouteTranslatorTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-displayname-route-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
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
    public void TranslatePreservingColors_PreservesQudWrapperMarkup()
    {
        WriteDictionary(("water flask", "水袋"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{C|water flask x2}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{C|水袋 x2}}"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesTmpColorMarkup()
    {
        WriteDictionary(
            ("water flask", "水袋"),
            ("[empty]", "[空]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "<color=#44ff88>water flask [empty]</color>",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("<color=#44ff88>水袋 [空]</color>"));
    }

    [TestCase("花瓶 [空]")]
    [TestCase("タム、ドロマド商人 [座っている]")]
    public void TranslatePreservingColors_PassesThroughAlreadyLocalizedBracketedDisplayName(string source)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_UsesLowerAsciiFallbackForParenthesizedState()
    {
        WriteDictionary(
            ("lead slug", "鉛の弾"),
            ("frozen", "凍結"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "lead slug (Frozen)",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("鉛の弾 (凍結)"));
    }

    [Test]
    public void TranslatePreservingColors_UsesExactLookupForWholeDisplayName()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "worn bronze sword",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("使い込まれた青銅の剣"));
    }

    [Test]
    public void TranslatePreservingColors_UsesTrimmedExactLookupForWholeDisplayName()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "  worn bronze sword  ",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("  使い込まれた青銅の剣  "));
    }

    [Test]
    public void TranslatePreservingColors_PrefersDisplayNameScopedDictionaryForConflictingLiquidKey()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("water", "{{B|水の}}"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("water", "水"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "water",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{B|水の}}"));
    }

    [Test]
    public void TranslatePreservingColors_PrefersDisplayNameScopedDictionaryForConflictingLiquidAdjectiveKey()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"));
        WriteDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            ("bloody", "血混じりの"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "bloody Naruur",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{r|血まみれの}}Naruur"));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("ui-displayname-route.ja.json", entries);
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
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
            Path.Combine(tempDirectory, fileName),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
