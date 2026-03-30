using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class WorldModsTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-world-mods-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslate_UsesScopedExactLookup()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Airfoil: This item can be thrown at +4 throwing range.", "エアフォイル: この品は投擲射程が+4される。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Airfoil: This item can be thrown at +4 throwing range.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("エアフォイル: この品は投擲射程が+4される。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesColorsForScopedExactLookup()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Scoped: This weapon has increased accuracy.", "スコープ付き: この武器は命中精度が向上する。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{Y|Scoped: This weapon has increased accuracy.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{Y|スコープ付き: この武器は命中精度が向上する。}}"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesImprovedMutationTemplate()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Grants you {0} at level {1}. If you already have {0}, its level is increased by {1}.", "{0}をレベル{1}で得る。すでに{0}を持っている場合、そのレベルが{1}上昇する。"),
            ("Temporal Fugue", "時間遁走"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Grants you Temporal Fugue at level 3. If you already have Temporal Fugue, its level is increased by 3.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("時間遁走をレベル3で得る。すでに時間遁走を持っている場合、そのレベルが3上昇する。"));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesBowsAndRifles()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class:", "武器カテゴリ:"),
            ("Bows && Rifles", "弓・ライフル"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Bows && Rifles",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: 弓・ライフル"));
        });
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
    {
        using var writer = new StreamWriter(Path.Combine(tempDirectory, fileName), append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
