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
    public void EffectDescriptionAndDetailsPatches_TranslateColoredDescriptionAndTemplatedPluralDetails_WhenPatched()
    {
        WriteDictionary(
            ("{{R|adrenaline flowing}}", "{{R|アドレナリン全開}}"),
            ("+{0} Quickness\n+1 rank to physical mutations", "+{0} Quickness\n肉体変異 +1ランク"),
            ("+{0} Quickness\n+{1} ranks to physical mutations", "+{0} Quickness\n肉体変異 +{1}ランク"));

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

            var effect = new DummyEffect
            {
                DescriptionText = "{{R|adrenaline flowing}}",
                DetailsText = "+20 Quickness\n+2 ranks to physical mutations",
            };

            Assert.Multiple(() =>
            {
                Assert.That(effect.GetDescription(), Is.EqualTo("{{R|アドレナリン全開}}"));
                Assert.That(effect.GetDetails(), Is.EqualTo("+20 Quickness\n肉体変異 +2ランク"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EffectDescriptionAndDetailsPatches_TranslateOverrideOwnerText_WhenPatched()
    {
        WriteDictionary(
            ("{{B|wet}}", "{{B|濡れている}}"),
            ("salty water", "塩水"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLiquidCoveredEffectTarget), nameof(DummyLiquidCoveredEffectTarget.GetDescription)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EffectDescriptionPatch), nameof(EffectDescriptionPatch.Postfix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyLiquidCoveredEffectTarget), nameof(DummyLiquidCoveredEffectTarget.GetDetails)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EffectDetailsPatch), nameof(EffectDetailsPatch.Postfix))));

            var effect = new DummyLiquidCoveredEffectTarget();

            Assert.Multiple(() =>
            {
                Assert.That(effect.GetDescription(), Is.EqualTo("{{B|濡れている}}"));
                Assert.That(effect.GetDetails(), Is.EqualTo("塩水を30ドラム浴びている。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EffectDescriptionPatch_TranslatesGeneratedDescriptionTemplates_WhenPatched()
    {
        WriteDictionary(
            ("dominated ({0} turns remaining)", "支配された（残り{0}ターン）"),
            ("time-dilated ({{C|-{0}}} Quickness)", "時間遅延 ({{C|-{0}}} Quickness)"),
            ("lying on {0}", "{0}に横たわっている"),
            ("engulfed by {0}", "{0}に呑み込まれている"),
            ("piloting {0}", "{0}を操縦中"),
            ("marked by {0}", "{0}にマークされている"),
            ("cleaved ({{C|-{0} AV}})", "裂かれた（{{C|-{0} AV}}）"),
            ("psionically cleaved (-{0} MA)", "精神的に裂かれた（-{0} MA）"),
            ("a chair", "椅子"),
            ("a starapple tree", "スターアップルの木"),
            ("a hovercraft", "ホバークラフト"),
            ("a snapjaw hunter", "スナップジョーの狩人"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEffect), nameof(DummyEffect.GetDescription)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EffectDescriptionPatch), nameof(EffectDescriptionPatch.Postfix))));

            Assert.Multiple(() =>
            {
                Assert.That(new DummyEffect { DescriptionText = "dominated (3 turns remaining)" }.GetDescription(), Is.EqualTo("支配された（残り3ターン）"));
                Assert.That(new DummyEffect { DescriptionText = "time-dilated ({{C|-40}} Quickness)" }.GetDescription(), Is.EqualTo("時間遅延 ({{C|-40}} Quickness)"));
                Assert.That(new DummyEffect { DescriptionText = "{{C|lying on a chair}}" }.GetDescription(), Is.EqualTo("{{C|椅子に横たわっている}}"));
                Assert.That(new DummyEffect { DescriptionText = "{{B|engulfed by a starapple tree}}" }.GetDescription(), Is.EqualTo("{{B|スターアップルの木に呑み込まれている}}"));
                Assert.That(new DummyEffect { DescriptionText = "{{C|piloting a hovercraft}}" }.GetDescription(), Is.EqualTo("{{C|ホバークラフトを操縦中}}"));
                Assert.That(new DummyEffect { DescriptionText = "{{R|marked by a snapjaw hunter}}" }.GetDescription(), Is.EqualTo("{{R|スナップジョーの狩人にマークされている}}"));
                Assert.That(new DummyEffect { DescriptionText = "{{r|cleaved ({{C|-3 AV}})}}" }.GetDescription(), Is.EqualTo("{{r|裂かれた（{{C|-3 AV}}）}}"));
                Assert.That(new DummyEffect { DescriptionText = "{{psionic|psionically cleaved (-2 MA)}}" }.GetDescription(), Is.EqualTo("{{psionic|精神的に裂かれた（-2 MA）}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EffectOwnerTargetResolver_IncludesBaseAndNonCookingOverrideDescriptionMethods()
    {
        WriteDictionary(
            ("base effect", "基本効果"),
            ("{{B|wet}}", "{{B|濡れている}}"));

        var targets = ActiveEffectOwnerTargetResolver.ResolveTargetMethods(
                typeof(DummyEffectBaseTarget),
                nameof(DummyEffectBaseTarget.GetDescription))
            .ToArray();

        Assert.That(
            targets.Select(static method => method.DeclaringType?.Name + "." + method.Name),
            Is.EquivalentTo(new[]
            {
                "DummyEffectBaseTarget.GetDescription",
                "DummyLiquidCoveredEffectTarget.GetDescription",
            }));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            for (var index = 0; index < targets.Length; index++)
            {
                harmony.Patch(
                    original: targets[index],
                    postfix: new HarmonyMethod(RequireMethod(typeof(EffectDescriptionPatch), nameof(EffectDescriptionPatch.Postfix))));
            }

            Assert.Multiple(() =>
            {
                Assert.That(new DummyEffectBaseTarget().GetDescription(), Is.EqualTo("基本効果"));
                Assert.That(new DummyLiquidCoveredEffectTarget().GetDescription(), Is.EqualTo("{{B|濡れている}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EffectOwnerTargetResolver_IncludesBaseAndNonCookingOverrideDetailsMethods()
    {
        WriteDictionary(
            ("base details", "基本詳細"),
            ("salty water", "塩水"));

        var targets = ActiveEffectOwnerTargetResolver.ResolveTargetMethods(
                typeof(DummyEffectBaseTarget),
                nameof(DummyEffectBaseTarget.GetDetails))
            .ToArray();

        Assert.That(
            targets.Select(static method => method.DeclaringType?.Name + "." + method.Name),
            Is.EquivalentTo(new[]
            {
                "DummyEffectBaseTarget.GetDetails",
                "DummyLiquidCoveredEffectTarget.GetDetails",
            }));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            for (var index = 0; index < targets.Length; index++)
            {
                harmony.Patch(
                    original: targets[index],
                    postfix: new HarmonyMethod(RequireMethod(typeof(EffectDetailsPatch), nameof(EffectDetailsPatch.Postfix))));
            }

            Assert.Multiple(() =>
            {
                Assert.That(new DummyEffectBaseTarget().GetDetails(), Is.EqualTo("基本詳細"));
                Assert.That(new DummyLiquidCoveredEffectTarget().GetDetails(), Is.EqualTo("塩水を30ドラム浴びている。"));
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
    public void EffectDescriptionAndDetailsPatches_DoNotCorruptNestedLiquidColorMarkup_WhenPatched()
    {
        WriteDictionary(
            ("wet", "{{B|濡れた}}"),
            ("salty water", "塩水"));

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

            var effect = new DummyEffect
            {
                DescriptionText = "{{B|{{B|wet}}}}",
                DetailsText = "Covered in 43 dram of {{Y|salty}} {{B|water}}.",
            };

            Assert.Multiple(() =>
            {
                Assert.That(effect.GetDescription(), Is.EqualTo("{{B|濡れた}}"));
                Assert.That(effect.GetDetails(), Is.EqualTo("塩水を43ドラム浴びている。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharacterStatusScreenHighlightEffectPatch_TranslatesRuntimeLiquidAndMovementDetails_WhenPatched()
    {
        WriteDictionary(
            ("{{B|wet}}", "{{B|濡れている}}"),
            ("wading", "浅瀬を進んでいる"),
            ("salty water", "塩水"));

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
                effect = new DummyEffect
                {
                    DescriptionText = "{{B|wet}}",
                    DetailsText = "Covered in 30 dram of salty water.\n\nwading\n-20 move speed.",
                },
            });

            Assert.That(
                screen.mutationsDetails.Text,
                Is.EqualTo("{{B|濡れている}}\n\n塩水を30ドラム浴びている。\n\n浅瀬を進んでいる\n移動速度 -20。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharacterStatusScreenHighlightEffectPatch_TranslatesTemplatedMultilineEffectText_WhenPatched()
    {
        WriteDictionary(
            ("{{R|adrenaline flowing}}", "{{R|アドレナリン全開}}"),
            ("+{0} Quickness\n+1 rank to physical mutations", "+{0} Quickness\n肉体変異 +1ランク"),
            ("+{0} Quickness\n+{1} ranks to physical mutations", "+{0} Quickness\n肉体変異 +{1}ランク"));

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
                effect = new DummyEffect
                {
                    DescriptionText = "{{R|adrenaline flowing}}",
                    DetailsText = "+20 Quickness\n+1 rank to physical mutations",
                },
            });

            Assert.That(screen.mutationsDetails.Text, Is.EqualTo("{{R|アドレナリン全開}}\n\n+20 Quickness\n肉体変異 +1ランク"));
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
