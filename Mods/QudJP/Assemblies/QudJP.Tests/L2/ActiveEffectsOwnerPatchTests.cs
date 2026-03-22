using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActiveEffectsOwnerPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-active-effects-owner-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DummyBookUI.Reset();
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
    public void EffectDescriptionAndDetailsPatches_TranslateEffectOwnerText_WhenPatched()
    {
        WriteDictionary(
            ("Wet", "濡れ"),
            ("Covered in slime.", "スライムに覆われている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEffect), nameof(DummyEffect.GetDescription)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EffectDescriptionPatch), nameof(EffectDescriptionPatch.Postfix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyEffect), nameof(DummyEffect.GetDetails)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EffectDetailsPatch), nameof(EffectDetailsPatch.Postfix))));

            var effect = new DummyEffect { DescriptionText = "Wet", DetailsText = "Covered in slime." };

            Assert.Multiple(() =>
            {
                Assert.That(effect.GetDescription(), Is.EqualTo("濡れ"));
                Assert.That(effect.GetDetails(), Is.EqualTo("スライムに覆われている。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharacterStatusScreenHighlightEffectPatch_TranslatesStatusPaneOwnerText_WhenPatched()
    {
        WriteDictionary(
            ("Wet", "濡れ"),
            ("Covered in slime.", "スライムに覆われている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEffect), nameof(DummyEffect.GetDescription)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EffectDescriptionPatch), nameof(EffectDescriptionPatch.Postfix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyEffect), nameof(DummyEffect.GetDetails)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EffectDetailsPatch), nameof(EffectDetailsPatch.Postfix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterEffectStatusScreen), nameof(DummyCharacterEffectStatusScreen.HandleHighlightEffect)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenHighlightEffectPatch), nameof(CharacterStatusScreenHighlightEffectPatch.Postfix))));

            var screen = new DummyCharacterEffectStatusScreen();
            screen.HandleHighlightEffect(new DummyCharacterEffectLineData
            {
                effect = new DummyEffect { DescriptionText = "Wet", DetailsText = "Covered in slime." },
            });

            Assert.That(screen.mutationsDetails.Text, Is.EqualTo("濡れ\n\nスライムに覆われている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void GameObjectShowActiveEffectsPatch_TranslatesBookOwnerTitleAndEmptyText_WhenPatched()
    {
        WriteDictionary(
            ("Active Effects - {0}", "発動中の効果 - {0}"),
            ("No active effects.", "発動中の効果はない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGameObjectActiveEffectsTarget), nameof(DummyGameObjectActiveEffectsTarget.ShowActiveEffects)),
                transpiler: new HarmonyMethod(RequireMethod(typeof(GameObjectShowActiveEffectsPatch), nameof(GameObjectShowActiveEffectsPatch.Transpiler))));

            var target = new DummyGameObjectActiveEffectsTarget { TitleSuffix = "Slime" };
            target.ShowActiveEffects();

            Assert.Multiple(() =>
            {
                Assert.That(DummyBookUI.LastTitle, Is.EqualTo("&W発動中の効果&Y - Slime"));
                Assert.That(DummyBookUI.LastText, Is.EqualTo("発動中の効果はない。"));
            });
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
            Path.Combine(tempDirectory, "active-effects-owner-l2.ja.json"),
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
