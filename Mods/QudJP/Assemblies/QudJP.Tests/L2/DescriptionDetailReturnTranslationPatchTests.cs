using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionDetailReturnTranslationPatchTests
{
    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-description-detail-return-l2", Guid.NewGuid().ToString("N"));
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

    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.CyberneticsChoiceGetDescription),
        "optical bioscanner (Face)",
        "光学バイオスキャナ（顔）",
        "CyberneticsChoiceDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.CyberneticsChoiceGetLongDescription),
        "{{C|-2 License Tier\n+1 Toughness}}",
        "{{C|-2 ライセンスティア\n+1 頑健}}",
        "CyberneticsChoiceLongDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.TinkerDataGetUnclippedDescription),
        "\n{{rules|Makes a batch of two.}}\n\nThis contraption hums quietly.\n",
        "\n{{rules|一度に2個作成する。}}\n\nこの装置は静かにうなっている。\n",
        "TinkerDataDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.TinkerDataGetDescription),
        "\n{{rules|Makes a batch of three.}}\n\nThis contraption hums quietly.\n",
        "\n{{rules|一度に3個作成する。}}\n\nこの装置は静かにうなっている。\n",
        "TinkerDataDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectCyberneticsUnitGetDescription),
        "Cybernetic implant installed",
        "サイバネティック・インプラント装着済み",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectSkillUnitGetDescription),
        "Has every Tinkering skill",
        "工匠の全スキルを所持",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectRelicUnitGetDescription),
        "Spawns with a mid-tier relic",
        "中ティアの聖遺物を所持して出現",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectGolemQuestRandomUnitGetDescription),
        "2 random effects",
        "ランダム効果2個",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectMetachromeUnitGetDescription),
        "Equipped with carbide fists",
        "カーバイドフィストを装備",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectBodyPartUnitGetDescription),
        "Extra arm slot",
        "腕スロットを追加",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectExperienceUnitGetDescription),
        "+500 experience",
        "経験値+500",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectMutationUnitGetDescription),
        "Temporal Fugue at level 3",
        "時間遁走（レベル3）",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectBaetylUnitGetDescription),
        "Spawns with 2 random baetyl rewards",
        "ランダムなベイティル報酬2個を所持して出現",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectCloneUnitGetDescription),
        "Spawns with a copy in a nearby cell",
        "近くのセルにコピー1体を伴って出現",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectReputationUnitGetDescription),
        "+200 reputation with {{C|the Barathrumites}}",
        "{{C|the Barathrumites}}との評判+200",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectSecretUnitGetDescription),
        "Reveals 3 secrets on creation",
        "生成時に秘密3件を明かす",
        "GameObjectUnitDescription")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectUnitGetDescription),
        "",
        "",
        "")]
    [TestCase(
        nameof(DummyDescriptionDetailReturnTarget.GameObjectUnitAggregateGetDescription),
        "Composite boon",
        "複合恩恵",
        "GameObjectUnitDescription")]
    public void Postfix_TranslatesCoveredDescriptionReturnValue_WhenPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        WriteDictionary(
            ("optical bioscanner", "光学バイオスキャナ"),
            ("This contraption hums quietly.", "この装置は静かにうなっている。"),
            ("Tinkering", "工匠"),
            ("carbide fists", "カーバイドフィスト"),
            ("arm", "腕"),
            ("Temporal Fugue", "時間遁走"),
            ("Composite boon", "複合恩恵"));

        WithPatchedOwner(ownerMethodName, () =>
        {
            var target = new DummyDescriptionDetailReturnTarget(source);

            var translated = InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(detail.Length == 0 ? 0 : 1));
            });
        });
    }

    [Test]
    public void Postfix_LeavesUnsupportedReturnValueUnchanged_WhenPatched()
    {
        WithPatchedOwner(
            nameof(DummyDescriptionDetailReturnTarget.CyberneticsChoiceGetDescription),
            () =>
            {
                var target = new DummyDescriptionDetailReturnTarget("made-up cyberware (Arm)");

                var translated = target.CyberneticsChoiceGetDescription();

                Assert.Multiple(() =>
                {
                    Assert.That(translated, Is.EqualTo("made-up cyberware (Arm)"));
                    Assert.That(HitCount("CyberneticsChoiceDescription"), Is.Zero);
                });
            });
    }

    private static void WithPatchedOwner(string ownerMethodName, Action action)
    {
        var harmonyId = "qudjp.tests.description-detail-return." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireOwnerMethod(ownerMethodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(DescriptionDetailReturnTranslationPatch),
                    nameof(DescriptionDetailReturnTranslationPatch.Postfix),
                    typeof(string).MakeByRefType(),
                    typeof(MethodBase))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string InvokeOwner(DummyDescriptionDetailReturnTarget target, string ownerMethodName)
    {
        var method = RequireOwnerMethod(ownerMethodName);
        var arguments = method.GetParameters().Length == 0 ? null : new object[] { false };
        return (string)method.Invoke(target, arguments)!;
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            DescriptionDetailReturnTranslationPatch.Context,
            DescriptionDetailReturnTranslationPatch.Family + "." + detail);
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
        File.WriteAllText(Path.Combine(dictionariesDirectory, "description-detail-return-l2.ja.json"), builder.ToString());
        Translator.ResetForTests();
        ChargenStructuredTextTranslator.ResetForTests();
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static MethodInfo RequireOwnerMethod(string name)
    {
        var method = AccessTools.Method(typeof(DummyDescriptionDetailReturnTarget), name, Type.EmptyTypes)
            ?? AccessTools.Method(typeof(DummyDescriptionDetailReturnTarget), name, new[] { typeof(bool) });
        Assert.That(method, Is.Not.Null, $"{typeof(DummyDescriptionDetailReturnTarget).FullName}.{name} not found");
        return method!;
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

internal sealed class DummyDescriptionDetailReturnTarget
{
    private readonly string source;

    public DummyDescriptionDetailReturnTarget(string source)
    {
        this.source = source;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string CyberneticsChoiceGetDescription() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string CyberneticsChoiceGetLongDescription() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string TinkerDataGetUnclippedDescription() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string TinkerDataGetDescription() => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectCyberneticsUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectSkillUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectRelicUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectGolemQuestRandomUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectMetachromeUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectBodyPartUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectExperienceUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectMutationUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectBaetylUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectCloneUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectReputationUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectSecretUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectUnitGetDescription(bool inscription = false) => source;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GameObjectUnitAggregateGetDescription(bool inscription = false) => source;
}
