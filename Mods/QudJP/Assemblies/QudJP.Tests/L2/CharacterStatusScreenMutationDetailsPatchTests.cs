using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CharacterStatusScreenMutationDetailsPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-characterstatus-mutationdetails-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesMutationDetails_WhenPatched()
    {
        WriteDictionary(
            ("mutation:Force Wall", "力の壁を生み出し、9マス連続の力場で敵を遮る。\n\n飛び道具は壁を通過させられる。"),
            ("mutation:Force Wall:rank:1", "9マス分の連続した固定力場を作る。\n持続: 16ラウンド\nクールダウン: 100ラウンド\n力場越しに飛び道具を撃てる。"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterStatusMutationScreen), nameof(DummyCharacterStatusMutationScreen.HandleHighlightMutation)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenMutationDetailsPatch), nameof(CharacterStatusScreenMutationDetailsPatch.Postfix))));

            var screen = new DummyCharacterStatusMutationScreen();
            screen.HandleHighlightMutation(new DummyCharacterMutationLineData
            {
                mutation = new DummyCharacterMutation
                {
                    Name = "Force Wall",
                    Level = 1,
                },
            });

            Assert.That(
                screen.mutationsDetails.Text,
                Is.EqualTo("力の壁を生み出し、9マス連続の力場で敵を遮る。\n\n飛び道具は壁を通過させられる。\n\n9マス分の連続した固定力場を作る。\n持続: 16ラウンド\nクールダウン: 100ラウンド\n力場越しに飛び道具を撃てる。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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

        var path = Path.Combine(tempDirectory, "character-status-mutation-details-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
