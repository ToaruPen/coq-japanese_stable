using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GameManagerUpdateSelectedAbilityPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-gamemanager-selected-ability-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesSelectedAbilityNameAndCooldown_WithinTmpScaffold()
    {
        WriteDictionary(
            ("snapjaw", "スナップジョー"),
            ("[{0} turns]", "[{0}ターン]"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyGameManagerSelectedAbilityTarget
            {
                NextSelectedAbilityText =
                    "<color=#666666><sprite=0><sprite=1></color> <color=#FFFFFF><color=#FFFF00><sprite=2></color> snapjaw [empty] [3 turns]</color>",
            };

            target.UpdateSelectedAbility();

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.selectedAbilityText.text,
                    Is.EqualTo("<color=#666666><sprite=0><sprite=1></color> <color=#FFFFFF><color=#FFFF00><sprite=2></color> スナップジョー [空] [3ターン]</color>"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(QudJP.Patches.GameManagerUpdateSelectedAbilityPatch),
                        "GameManager.SelectedAbility"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(QudJP.Patches.GameManagerUpdateSelectedAbilityPatch),
                        "DisplayName.BracketedSuffix"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(QudJP.Patches.UITextSkinTranslationPatch),
                        nameof(QudJP.Patches.GameManagerUpdateSelectedAbilityPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "<color=#666666><sprite=0><sprite=1></color> <color=#FFFFFF><color=#FFFF00><sprite=2></color> snapjaw [empty] [3 turns]</color>",
                        "<color=#666666><sprite=0><sprite=1></color> <color=#FFFFFF><color=#FFFF00><sprite=2></color> snapjaw [empty] [3 turns]</color>"),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesNoneLeaf_WithoutChangingCommandFragments()
    {
        WriteDictionary(("<none>", "<なし>"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyGameManagerSelectedAbilityTarget
            {
                NextSelectedAbilityText = "<color=#666666><sprite=0><sprite=1></color> <none>",
            };

            target.UpdateSelectedAbility();

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.selectedAbilityText.text,
                    Is.EqualTo("<color=#666666><sprite=0><sprite=1></color> <なし>"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(QudJP.Patches.GameManagerUpdateSelectedAbilityPatch),
                        "GameManager.SelectedAbility"),
                    Is.GreaterThan(0));
            });
        });
    }

    private static void RunWithPostfixPatch(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGameManagerSelectedAbilityTarget), nameof(DummyGameManagerSelectedAbilityTarget.UpdateSelectedAbility)),
                postfix: new HarmonyMethod(RequirePatchMethod(
                    "QudJP.Patches.GameManagerUpdateSelectedAbilityPatch",
                    "Postfix",
                    typeof(object))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        var method = AccessTools.Method(type, methodName);
        Assert.That(method, Is.Not.Null, $"Method not found: {type.FullName}.{methodName}");
        return method!;
    }

    private static MethodInfo RequirePatchMethod(string typeName, string methodName, params Type[] parameterTypes)
    {
        var patchType = typeof(Translator).Assembly.GetType(typeName, throwOnError: false);
        Assert.That(patchType, Is.Not.Null, $"Patch type not found: {typeName}");

        var method = patchType!.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"Method not found: {typeName}.{methodName}");
        return method!;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("gamemanager-selected-ability-l2.ja.json", entries);
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var writer = new StreamWriter(path, append: false, Utf8WithoutBom);
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
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
