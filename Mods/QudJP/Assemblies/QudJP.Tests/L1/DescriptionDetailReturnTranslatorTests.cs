using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DescriptionDetailReturnTranslatorTests
{
    private const DescriptionDetailReturnKind GameObjectUnitDescriptionKind =
        DescriptionDetailReturnKind.GameObjectUnitDescription;

    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-description-detail-return-l1", Guid.NewGuid().ToString("N"));
        dictionariesDirectory = Path.Combine(tempRoot, "Dictionaries");
        Directory.CreateDirectory(dictionariesDirectory);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempRoot);
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        DynamicTextObservability.ResetForTests();
        ChargenStructuredTextTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public void TryTranslate_TranslatesCyberneticsChoiceDescriptionSlotPattern()
    {
        WriteDictionary(("optical bioscanner", "光学バイオスキャナ"));

        var translated = DescriptionDetailReturnTranslator.TryTranslate(
            "optical bioscanner (Face)",
            DescriptionDetailReturnKind.CyberneticsChoiceDescription,
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("光学バイオスキャナ（顔）"));
            Assert.That(detail, Is.EqualTo("CyberneticsChoiceDescription"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesCyberneticsChoiceLongDescriptionDefaultChoice()
    {
        var source = "{{C|-2 License Tier\n+1 Toughness}}";

        var translated = DescriptionDetailReturnTranslator.TryTranslate(
            source,
            DescriptionDetailReturnKind.CyberneticsChoiceLongDescription,
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{C|-2 ライセンスティア\n+1 頑健}}"));
            Assert.That(detail, Is.EqualTo("CyberneticsChoiceLongDescription"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesCyberneticsChoiceLongDescriptionBodyAndRules()
    {
        WriteDictionary(("Bio scan.", "生体スキャン。"));
        var source = "Bio scan.\n\n{{rules|You gain access to every schematic of low tier pistols.}}";

        var translated = DescriptionDetailReturnTranslator.TryTranslate(
            source,
            DescriptionDetailReturnKind.CyberneticsChoiceLongDescription,
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("生体スキャン。\n\n{{rules|下位のピストルの全設計図にアクセスできる。}}"));
            Assert.That(detail, Is.EqualTo("CyberneticsChoiceLongDescription"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesTinkerDataDescriptionBatchFrameAndBody()
    {
        WriteDictionary(("This contraption hums quietly.", "この装置は静かにうなっている。"));
        var source = "\n{{rules|Makes a batch of two.}}\n\nThis contraption hums quietly.\n";

        var translated = DescriptionDetailReturnTranslator.TryTranslate(
            source,
            DescriptionDetailReturnKind.TinkerDataDescription,
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("\n{{rules|一度に2個作成する。}}\n\nこの装置は静かにうなっている。\n"));
            Assert.That(detail, Is.EqualTo("TinkerDataDescription"));
        });
    }

    [TestCase("Cybernetic implant installed", "サイバネティック・インプラント装着済み")]
    [TestCase("Has every Tinkering skill", "工匠の全スキルを所持")]
    [TestCase("Has the Recharge skill", "充電スキルを所持")]
    [TestCase("Spawns with a mid-tier relic", "中ティアの聖遺物を所持して出現")]
    [TestCase("2 random effects", "ランダム効果2個")]
    [TestCase("Equipped with carbide fists", "カーバイドフィストを装備")]
    [TestCase("Extra arm slot", "腕スロットを追加")]
    [TestCase("+3 levels", "レベル+3")]
    [TestCase("+500 experience", "経験値+500")]
    [TestCase("Temporal Fugue at level 3", "時間遁走（レベル3）")]
    [TestCase("Quantum Fugue", "量子フーガ")]
    [TestCase("Spawns with 2 random baetyl rewards", "ランダムなベイティル報酬2個を所持して出現")]
    [TestCase("Spawns with 1 random baetyl reward", "ランダムなベイティル報酬1個を所持して出現")]
    [TestCase("Spawns with a copy in a nearby cell", "近くのセルにコピー1体を伴って出現")]
    [TestCase("Reveals 3 secrets on creation", "生成時に秘密3件を明かす")]
    [TestCase("+200 reputation with {{C|the Barathrumites}}", "{{C|the Barathrumites}}との評判+200")]
    [TestCase("-100 reputation with the Issachari tribe", "the Issachari tribeとの評判-100")]
    public void TryTranslate_TranslatesGameObjectUnitDescriptionPatterns(string source, string expected)
    {
        WriteDictionary(
            ("Tinkering", "工匠"),
            ("Recharge", "充電"),
            ("carbide fists", "カーバイドフィスト"),
            ("arm", "腕"),
            ("Temporal Fugue", "時間遁走"),
            ("Quantum Fugue", "量子フーガ"));

        var translated = DescriptionDetailReturnTranslator.TryTranslate(
            source,
            GameObjectUnitDescriptionKind,
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(detail, Is.EqualTo("GameObjectUnitDescription"));
        });
    }

    [TestCase("")]
    [TestCase("unknown phrase")]
    public void TryTranslate_LeavesUnsupportedValuesUnchanged(string source)
    {
        WriteDictionary(("optical bioscanner", "光学バイオスキャナ"));

        var translated = DescriptionDetailReturnTranslator.TryTranslate(
            source,
            DescriptionDetailReturnKind.CyberneticsChoiceDescription,
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
            Assert.That(detail, Is.Empty);
        });
    }

    [Test]
    public void TryTranslate_StripsMarkedValuesWithoutRetranslating()
    {
        WriteDictionary(("optical bioscanner", "光学バイオスキャナ"));

        var translated = DescriptionDetailReturnTranslator.TryTranslate(
            "\u0001optical bioscanner (Face)",
            DescriptionDetailReturnKind.CyberneticsChoiceDescription,
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("optical bioscanner (Face)"));
            Assert.That(detail, Is.Empty);
        });
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"entries\": [");
        for (var index = 0; index < entries.Length; index++)
        {
            var (key, text) = entries[index];
            builder.Append("    { \"key\": \"")
                .Append(EscapeJson(key))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(text))
                .Append("\" }");
            builder.AppendLine(index == entries.Length - 1 ? string.Empty : ",");
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        File.WriteAllText(Path.Combine(dictionariesDirectory, "description-detail-return-l1.ja.json"), builder.ToString());
        Translator.ResetForTests();
        ChargenStructuredTextTranslator.ResetForTests();
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
