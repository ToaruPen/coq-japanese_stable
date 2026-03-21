using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ChargenStructuredTextTranslatorTests
{
    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-chargen-structured-l1", Guid.NewGuid().ToString("N"));
        dictionariesDirectory = Path.Combine(tempRoot, "Dictionaries");
        Directory.CreateDirectory(dictionariesDirectory);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempRoot);
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public void Translate_UsesSubtypeDisplayNamesFromSubtypesXml()
    {
        WriteXmlFile(
            "Subtypes.jp.xml",
            """
            <?xml version="1.0" encoding="utf-8" ?>
            <subtypes>
              <class ID="Callings" ChargenTitle="職能を選択" SingularTitle="職能">
                <subtype Name="Apostle" DisplayName="使徒" />
              </class>
            </subtypes>
            """);

        var translated = ChargenStructuredTextTranslator.Translate("Apostle");

        Assert.That(translated, Is.EqualTo("使徒"));
    }

    [Test]
    public void Translate_UsesMutationDisplayNamesFromMutationsXml()
    {
        WriteXmlFile(
            "Mutations.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <mutations>
              <category Name="Physical" DisplayName="{{G|肉体突然変異}}">
                <mutation Name="Adrenal Control" DisplayName="アドレナリン制御" />
              </category>
            </mutations>
            """);

        var translated = ChargenStructuredTextTranslator.Translate("Adrenal Control");

        Assert.That(translated, Is.EqualTo("アドレナリン制御"));
    }

    [Test]
    public void Translate_UsesMutationDisplayNamesFromHiddenMutationsXml()
    {
        WriteXmlFile(
            "HiddenMutations.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <mutations Hidden="true" ExcludeFromPool="true">
              <category Name="Mental">
                <mutation Name="Quantum Fugue" DisplayName="量子フーガ" />
              </category>
            </mutations>
            """);

        var translated = ChargenStructuredTextTranslator.Translate("Quantum Fugue");

        Assert.That(translated, Is.EqualTo("量子フーガ"));
    }

    [Test]
    public void TranslateMutationMenuDescription_PreservesVariantHotkeySuffix()
    {
        WriteXmlFile(
            "Mutations.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <mutations>
              <category Name="Physical" DisplayName="{{G|肉体突然変異}}">
                <mutation Name="Stinger (Confusing Venom)" DisplayName="毒針（混乱毒）" />
              </category>
            </mutations>
            """);

        var translated = ChargenStructuredTextTranslator.TranslateMutationMenuDescription("Stinger (Confusing Venom) [{{W|V}}]");

        Assert.That(translated, Is.EqualTo("毒針（混乱毒） [{{W|V}}]"));
    }

    [Test]
    public void TryTranslateMutationLongDescription_ComposesDescriptionAndRankText()
    {
        WriteDictionary(
            ("mutation:Adrenal Control", "アドレナリン分泌を制御できる。"),
            ("mutation:Adrenal Control:rank:1", "クールダウン: 200ターン"));

        var translated = ChargenStructuredTextTranslator.TryTranslateMutationLongDescription("Adrenal Control", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("アドレナリン分泌を制御できる。\n\nクールダウン: 200ターン"));
        });
    }

    [Test]
    public void Translate_TranslatesPointsRemainingLine()
    {
        WriteDictionary(("Points Remaining:", "残りポイント:"));

        var translated = ChargenStructuredTextTranslator.Translate("Points Remaining: 12");

        Assert.That(translated, Is.EqualTo("残りポイント: 12"));
    }

    [Test]
    public void Translate_TranslatesPointsRemainingLineWithSurroundingWhitespace()
    {
        WriteDictionary(("Points Remaining:", "残りポイント:"));

        var translated = ChargenStructuredTextTranslator.Translate("  Points Remaining: 12  ");

        Assert.That(translated, Is.EqualTo("残りポイント: 12"));
    }

    [Test]
    public void Translate_TranslatesNameLabelWithoutRequiringExactSpacingMatch()
    {
        WriteDictionary(("Name: ", "名前: "));

        var translated = ChargenStructuredTextTranslator.Translate("Name:");

        Assert.That(translated, Is.EqualTo("名前:"));
    }

    [Test]
    public void Translate_TranslatesPointToken()
    {
        var translated = ChargenStructuredTextTranslator.Translate("[2pts]");

        Assert.That(translated, Is.EqualTo("[2点]"));
    }

    [Test]
    public void Translate_TranslatesStructuredSubtypeDescriptionFromGeneratorFamilies()
    {
        WriteXmlFile(
            "Factions.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <factions Load="Merge">
              <faction Name="Bears" DisplayName="クマ" />
            </factions>
            """);
        WriteDictionary(
            ("Persuasion", "説得術"),
            ("Intimidate", "威圧"),
            ("Starts with random junk and artifacts", "ランダムなガラクタとアーティファクトを所持して開始"));

        var source = """
            {{c|ù}} Persuasion
              {{C|ù}} Intimidate
            {{c|ù}} +2 Ego
            {{c|ù}} +100 reputation with Bears
            {{c|ù}} Starts with random junk and artifacts
            """;

        var translated = ChargenStructuredTextTranslator.Translate(source);

        var expected = """
            {{c|ù}} 説得術
              {{C|ù}} 威圧
            {{c|ù}} 自我 +2
            {{c|ù}} クマとの評判 +100
            {{c|ù}} ランダムなガラクタとアーティファクトを所持して開始
            """;

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void Translate_TranslatesRawBulletStructuredDescriptionAndArticleFactionName()
    {
        WriteXmlFile(
            "Factions.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <factions Load="Merge">
              <faction Name="Issachari" DisplayName="イッサカリ族" />
            </factions>
            """);
        WriteDictionary(
            ("Wayfaring", "サバイバル"),
            ("Wilderness Lore: Random", "荒地巡り：ランダム"),
            ("Starts with random junk and artifacts", "ランダムなガラクタとアーティファクトを所持して開始"));

        var source = """
            ù +2 Toughness
            ù Wayfaring
              ù Wilderness Lore: Random
            ù +400 reputation with the Issachari
            ù Starts with random junk and artifacts
            """;

        var translated = ChargenStructuredTextTranslator.Translate(source);

        var expected = """
            ù 頑健 +2
            ù サバイバル
              ù 荒地巡り：ランダム
            ù イッサカリ族との評判 +400
            ù ランダムなガラクタとアーティファクトを所持して開始
            """;

        Assert.That(translated, Is.EqualTo(expected));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
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
            Path.Combine(dictionariesDirectory, "chargen-structured-l1.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteXmlFile(string relativePath, string content)
    {
        File.WriteAllText(
            Path.Combine(tempRoot, relativePath),
            content.ReplaceLineEndings(Environment.NewLine),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
