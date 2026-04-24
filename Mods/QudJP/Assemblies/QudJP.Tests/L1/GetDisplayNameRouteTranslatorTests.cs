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
    public void TranslatePreservingColors_UsesDisplayNameScopedBracketedStateLookups()
    {
        WriteDictionary(("water flask", "水袋"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"),
            ("[empty, sealed]", "[空／密封]"),
            ("[sealed]", "[密封]"),
            ("[auto-collecting]", "[自動採取中]"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "<color=#44ff88>water flask [empty]</color>",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("<color=#44ff88>水袋 [空]</color>"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "<color=#44ff88>water flask [empty, sealed]</color>",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("<color=#44ff88>水袋 [空／密封]</color>"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "<color=#44ff88>water flask [auto-collecting]</color>",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("<color=#44ff88>水袋 [自動採取中]</color>"));
        });
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
    public void TranslatePreservingColors_PrefersExactWholeDisplayNameLookupBeforeProperNameModifierHeuristic()
    {
        WriteDictionary(("Water Containers", "水容器"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("water", "{{B|水の}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "Water Containers",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("水容器"));
    }

    [Test]
    public void TranslatePreservingColors_PrefersTrimmedExactLookupBeforeProperNameModifierHeuristic()
    {
        WriteDictionary(("Water Containers", "水容器"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("water", "{{B|水の}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "Water Containers  ",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("水容器  "));
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

    [Test]
    public void TranslatePreservingColors_PreservesMarkupWrappedEnglishModifierWithoutNestedColorCorruption()
    {
        WriteDictionary(("dromad merchant", "ドロマド商人"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"),
            ("[sitting]", "[座っている]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{r|bloody}} Tam, dromad merchant [sitting]",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{r|血まみれの}}Tam、ドロマド商人 [座っている]"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesLocalizedPrefixWithAsciiTailStructurally()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("neutronic", "{{neutronic|中性子質の}}"),
            ("oozing", "{{K|滲み出ている}}"),
            ("spiced", "香辛料入りの"),
            ("tetrasludge", "テトラスラッジ"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "伝説の芳醇な neutronic oozing spiced tetrasludge",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("伝説の芳醇な{{neutronic|中性子質の}}{{K|滲み出ている}}香辛料入りのテトラスラッジ"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMarkupOnlyAdjective()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("jewel-encrusted", "宝石をちりばめた"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{m-G-R-W-c-y-B-m-r-W-r-W-c-R-b sequence|jewel-encrusted}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{m-G-R-W-c-y-B-m-r-W-r-W-c-R-b sequence|宝石をちりばめた}}"));
    }

    [Test]
    public void TranslatePreservingColors_SuppressesIdentityVisageMissingKeyNoise()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("VISAGE", "VISAGE"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{visage|VISAGE}}",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{visage|VISAGE}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("VISAGE"), Is.EqualTo(0));
        });
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
