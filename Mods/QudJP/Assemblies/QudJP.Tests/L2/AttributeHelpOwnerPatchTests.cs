using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AttributeHelpOwnerPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-attribute-help-owner-l2", Guid.NewGuid().ToString("N"));
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
    public void StatisticGetHelpTextPatch_TranslatesExactOwnerText_WhenPatched()
    {
        WriteDictionary((
            "Your {{W|Strength}} determines how much melee damage you do (by improving your armor penetration), your ability to resist forced movement, and your carry capacity.",
            "あなたの{{W|筋力}}は（装甲貫通で増幅された）近接攻撃のダメージ、強制移動への抵抗、所持重量の上限を決める。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyStatistic), nameof(DummyStatistic.GetHelpText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(StatisticGetHelpTextPatch), nameof(StatisticGetHelpTextPatch.Postfix))));

            var statistic = new DummyStatistic
            {
                HelpText = "Your {{W|Strength}} determines how much melee damage you do (by improving your armor penetration), your ability to resist forced movement, and your carry capacity.",
            };

            Assert.That(
                statistic.GetHelpText(),
                Is.EqualTo("あなたの{{W|筋力}}は（装甲貫通で増幅された）近接攻撃のダメージ、強制移動への抵抗、所持重量の上限を決める。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharacterStatusScreenAttributeHighlightPatch_TranslatesStatisticAndComputePowerBranches_WhenPatched()
    {
        WriteDictionary(
            (
                "Your {{W|Strength}} determines how much melee damage you do (by improving your armor penetration), your ability to resist forced movement, and your carry capacity.",
                "あなたの{{W|筋力}}は（装甲貫通で増幅された）近接攻撃のダメージ、強制移動への抵抗、所持重量の上限を決める。"),
            (
                "Your {{W|Compute Power (CP)}} scales the bonuses of certain compute-enabled items and cybernetic implants. Your base compute power is 0.",
                "あなたの{{W|演算能力 (CP)}}は特定の計算機構付きアイテムやサイバネティクスのボーナス倍率を決める。基礎演算能力は0。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterAttributeHighlightScreen), nameof(DummyCharacterAttributeHighlightScreen.HandleHighlightAttribute)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenAttributeHighlightPatch), nameof(CharacterStatusScreenAttributeHighlightPatch.Postfix))));

            var screen = new DummyCharacterAttributeHighlightScreen();
            screen.HandleHighlightAttribute(new DummyAttributeHighlightElement
            {
                statistic = new DummyStatistic
                {
                    HelpText = "Your {{W|Strength}} determines how much melee damage you do (by improving your armor penetration), your ability to resist forced movement, and your carry capacity.",
                },
            });
            Assert.That(
                screen.primaryAttributesDetails.Text,
                Is.EqualTo("あなたの{{W|筋力}}は（装甲貫通で増幅された）近接攻撃のダメージ、強制移動への抵抗、所持重量の上限を決める。"));

            screen.HandleHighlightAttribute(new DummyAttributeHighlightElement { TargetField = "secondary" });
            Assert.That(
                screen.secondaryAttributesDetails.Text,
                Is.EqualTo("あなたの{{W|演算能力 (CP)}}は特定の計算機構付きアイテムやサイバネティクスのボーナス倍率を決める。基礎演算能力は0。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
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
            Path.Combine(tempDirectory, "attribute-help-owner-l2.ja.json"),
            builder.ToString(),
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
