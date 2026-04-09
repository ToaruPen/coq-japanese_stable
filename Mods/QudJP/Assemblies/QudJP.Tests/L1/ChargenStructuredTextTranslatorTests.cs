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
    public void TryTranslateMutationLongDescription_UsesVariantMutationNameKey()
    {
        WriteDictionary(
            ("mutation:Stinger (Confusing Venom)", "臀部の毒針を持つ。"),
            ("mutation:Stinger (Confusing Venom):rank:1", "刺突では混乱毒を与える。"));

        var translated = ChargenStructuredTextTranslator.TryTranslateMutationLongDescription(
            "Stinger (Confusing Venom)",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("臀部の毒針を持つ。\n\n刺突では混乱毒を与える。"));
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

    [Test]
    public void Translate_PrefersChargenScopedSkillDictionaryOverGlobalCollision()
    {
        WriteDictionaryFile(
            "ui-skillsandpowers.ja.json",
            ("Persuasion", "説得"),
            ("Wayfaring", "辺境行"));
        WriteDictionaryFile(
            Path.Combine("Scoped", "ui-chargen-skill-context.ja.json"),
            ("{{c|ù}} Persuasion", "{{c|ù}} 説得術"),
            ("Persuasion", "説得術"),
            ("Wayfaring", "サバイバル"));

        var source = """
            {{c|ù}} Persuasion
            ù Wayfaring
            """;

        var translated = ChargenStructuredTextTranslator.Translate(source);

        var expected = """
            {{c|ù}} 説得術
            ù サバイバル
            """;

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("optical bioscanner (Face)", "光学バイオスキャナ（顔）")]
    [TestCase("hyper-elastic ankle tendons (Feet)", "高弾性足首腱（足）")]
    [TestCase("parabolic muscular subroutine (Arm)", "放物筋サブルーチン（腕）")]
    [TestCase("translucent skin (Back)", "透明皮膚（背中）")]
    [TestCase("dermal insulation (Body)", "皮膚用断熱材（胴）")]
    [TestCase("dermal insulation (Head)", "皮膚用断熱材（頭）")]
    [TestCase("night vision (Face)", "暗視システム（顔）")]
    [TestCase("stabilizer arm locks (Arm)", "安定化アームロック（腕）")]
    [TestCase("rapid release finger flexors (Hands)", "高速リリース屈筋（手）")]
    [TestCase("carbide hand bones (Hands)", "カーバイド製手骨（手）")]
    [TestCase("pentaceps (Feet)", "ペンタセプス（足）")]
    [TestCase("inflatable axons (Head)", "膨張軸索（頭）")]
    public void Translate_TranslatesCyberneticsSlotPatternWithEnglishKeys(string source, string expected)
    {
        WriteDictionary(
            ("optical bioscanner", "光学バイオスキャナ"),
            ("hyper-elastic ankle tendons", "高弾性足首腱"),
            ("parabolic muscular subroutine", "放物筋サブルーチン"),
            ("translucent skin", "透明皮膚"),
            ("dermal insulation", "皮膚用断熱材"),
            ("night vision", "暗視システム"),
            ("stabilizer arm locks", "安定化アームロック"),
            ("rapid release finger flexors", "高速リリース屈筋"),
            ("carbide hand bones", "カーバイド製手骨"),
            ("pentaceps", "ペンタセプス"),
            ("inflatable axons", "膨張軸索"));

        var translated = ChargenStructuredTextTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("Back")]
    [TestCase("Arm")]
    [TestCase("Face")]
    [TestCase("Body")]
    [TestCase("Head")]
    [TestCase("Feet")]
    [TestCase("Hands")]
    public void Translate_DoesNotTranslateStandaloneSlotNameViaSlotNamesTable(string slotName)
    {
        var translated = ChargenStructuredTextTranslator.Translate(slotName);

        Assert.That(translated, Is.EqualTo(slotName));
    }

    [TestCase("made-up cyberware (Arm)")]
    [TestCase("")]
    [TestCase("\u0001optical bioscanner (Face)")]
    public void Translate_PreservesCyberneticsSlotFallbackAndEdgeCases(string source)
    {
        WriteDictionary(("optical bioscanner", "光学バイオスキャナ"));

        var translated = ChargenStructuredTextTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(source));
    }

    [TestCase("{{y|optical bioscanner (Face)}}", "{{y|光学バイオスキャナ（顔）}}")]
    [TestCase("optical bioscanner {{y|(Face)}}", "光学バイオスキャナ{{y|（顔）}}")]
    public void Translate_PreservesCyberneticsSlotColorTags(string source, string expected)
    {
        WriteDictionary(("optical bioscanner", "光学バイオスキャナ"));

        var translated = ChargenStructuredTextTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(expected));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("chargen-structured-l1.ja.json", entries);
    }

    private void WriteDictionaryFile(string relativePath, params (string key, string text)[] entries)
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

        var path = Path.Combine(dictionariesDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
