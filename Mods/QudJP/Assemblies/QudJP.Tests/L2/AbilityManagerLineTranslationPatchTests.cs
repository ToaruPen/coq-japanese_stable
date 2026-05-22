using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    private static void PatchAbilityManagerLine(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyAbilityManagerLineTarget), nameof(DummyAbilityManagerLineTarget.setData)),
            prefix: new HarmonyMethod(RequireMethod(typeof(AbilityManagerLineTranslationPatch), nameof(AbilityManagerLineTranslationPatch.Prefix))),
            postfix: new HarmonyMethod(RequireMethod(typeof(AbilityManagerLineTranslationPatch), nameof(AbilityManagerLineTranslationPatch.Postfix))));
    }

    [Test]
    public void AbilityManagerLinePatch_TranslatesCategoryAbilityAndMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("Mental Mutations", "精神変異"),
            ("Force Bubble", "力場球"),
            ("Move Down", "下へ移動"),
            ("Move Up", "上へ移動"),
            ("Bind Key", "キー割り当て"),
            ("Unbind Key", "キー解除"),
            ("attack", "攻撃"),
            ("turn cooldown", "ターンのクールダウン"),
            ("Toggled on", "オン"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAbilityManagerLine(harmony);

            var categoryTarget = new DummyAbilityManagerLineTarget();
            categoryTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                category = "Mental Mutations",
                collapsed = false,
            });

            var abilityTarget = new DummyAbilityManagerLineTarget();
            abilityTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Force Bubble",
                    Cooldown = 3,
                    CooldownRounds = 3,
                    Toggleable = true,
                    ToggleState = true,
                },
                hotkeyDescription = "F",
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.text.Text, Is.EqualTo("[-] 精神変異"));
                Assert.That(abilityTarget.text.Text, Does.Contain("力場球"));
                Assert.That(abilityTarget.text.Text, Does.Contain("ターンのクールダウン"));
                Assert.That(abilityTarget.text.Text, Does.Contain("オン"));
                Assert.That(abilityTarget.text.Text, Does.Contain("<{{w|F}}>"));
                Assert.That(abilityTarget.OriginalExecuted, Is.True);
                Assert.That(abilityTarget.icon.gameObject.activeSelf, Is.True);
                Assert.That(abilityTarget.icon.LastRenderable, Is.Not.Null);
                Assert.That(DummyAbilityManagerLineTarget.MOVE_DOWN.Description, Is.EqualTo("下へ移動"));
                Assert.That(DummyAbilityManagerLineTarget.MOVE_UP.Description, Is.EqualTo("上へ移動"));
                Assert.That(DummyAbilityManagerLineTarget.BIND_KEY.Description, Is.EqualTo("キー割り当て"));
                Assert.That(DummyAbilityManagerLineTarget.UNBIND_KEY.Description, Is.EqualTo("キー解除"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AbilityManagerLineTranslationPatch), "AbilityManagerLine.AbilityText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AbilityManagerLineTranslationPatch), "AbilityManagerLine.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerLinePatch_TranslatesGeneratedReleaseGasAbilityName_FromMutationDisplayName()
    {
        WriteDictionary(
            ("Aggressive Stance", "攻勢の構え"),
            ("Defensive Stance", "守勢の構え"),
            ("Lase", "レーザー照射"),
            ("Rebuke Robot", "ロボットを叱責"),
            ("turn cooldown", "ターンのクールダウン"),
            ("Toggled on", "オン"));
        WriteMutationsXml(("Corrosive Gas Generation", "腐食性ガス生成"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAbilityManagerLine(harmony);

            var abilityTarget = new DummyAbilityManagerLineTarget();
            abilityTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Release Corrosive Gas",
                    Cooldown = 3,
                    CooldownRounds = 3,
                    Toggleable = true,
                    ToggleState = true,
                },
                hotkeyDescription = "G",
            });

            var laseTarget = new DummyAbilityManagerLineTarget();
            laseTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Lase (4 charges)",
                },
            });

            var rebukeRobotTarget = new DummyAbilityManagerLineTarget();
            rebukeRobotTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Rebuke Robot",
                },
            });

            var aggressiveStanceTarget = new DummyAbilityManagerLineTarget();
            aggressiveStanceTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Aggressive Stance",
                },
            });

            var defensiveStanceTarget = new DummyAbilityManagerLineTarget();
            defensiveStanceTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Defensive Stance",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(abilityTarget.text.Text, Does.Contain("腐食性ガス放出"));
                Assert.That(abilityTarget.text.Text, Does.Not.Contain("Release Corrosive Gas"));
                Assert.That(laseTarget.text.Text, Does.Contain("レーザー照射 (4チャージ)"));
                Assert.That(laseTarget.text.Text, Does.Not.Contain("Lase (4 charges)"));
                Assert.That(rebukeRobotTarget.text.Text, Does.Contain("ロボットを叱責"));
                Assert.That(rebukeRobotTarget.text.Text, Does.Not.Contain("Rebuke Robot"));
                Assert.That(aggressiveStanceTarget.text.Text, Does.Contain("攻勢の構え"));
                Assert.That(aggressiveStanceTarget.text.Text, Does.Not.Contain("Aggressive Stance"));
                Assert.That(defensiveStanceTarget.text.Text, Does.Contain("守勢の構え"));
                Assert.That(defensiveStanceTarget.text.Text, Does.Not.Contain("Defensive Stance"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AbilityManagerLineTranslationPatch), "AbilityManagerLine.AbilityText"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerLinePatch_UsesAbilityDisplayHotkey_WhenLineHotkeyIsEmpty()
    {
        WriteDictionary(("Force Bubble", "力場球"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAbilityManagerLine(harmony);

            var abilityTarget = new DummyAbilityManagerLineTarget();
            abilityTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Force Bubble",
                    DisplayForHotkey = "Ctrl+F",
                    Command = "CommandForceBubble",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(abilityTarget.text.Text, Does.Contain("力場球"));
                Assert.That(abilityTarget.text.Text, Does.Contain("<{{w|Ctrl+F}}>"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerLinePatch_FallsBackToAbilityBarSlotHotkey_WhenConfiguredAbilityHotkeyIsMissing()
    {
        WriteDictionary(("Force Bubble", "力場球"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAbilityManagerLine(harmony);

            var abilityTarget = new DummyAbilityManagerLineTarget();
            abilityTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                quickKey = 'a',
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Force Bubble",
                    Command = "CommandForceBubble",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(abilityTarget.text.Text, Does.Contain("力場球"));
                Assert.That(abilityTarget.text.Text, Does.Contain("<{{w|1}}>"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerLinePatch_PreservesOriginalIconRefresh_WhenTranslatingTextAndSlotHotkey()
    {
        WriteDictionary(("Force Bubble", "力場球"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAbilityManagerLine(harmony);

            var abilityTarget = new DummyAbilityManagerLineTarget();
            abilityTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                quickKey = 'c',
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Force Bubble",
                    Command = "CommandForceBubble",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(abilityTarget.OriginalExecuted, Is.True);
                Assert.That(abilityTarget.icon.gameObject.activeSelf, Is.True);
                Assert.That(abilityTarget.icon.LastRenderable, Is.Not.Null);
                Assert.That(abilityTarget.text.Text, Does.Contain("力場球"));
                Assert.That(abilityTarget.text.Text, Does.Contain("<{{w|3}}>"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerLinePatch_MapsQuickKeyToAbilityBarSlotFallback()
    {
        var method = RequireMethod(typeof(AbilityManagerLineTranslationPatch), "ResolveQuickKeySlotNumber");
        var fallbackHotkey = RequireMethod(typeof(AbilityManagerLineTranslationPatch), "GetFallbackAbilityBarSlotHotkey");

        Assert.Multiple(() =>
        {
            Assert.That(method.Invoke(null, new object[] { new DummyAbilityManagerLineDataTarget { quickKey = 'a' } }), Is.EqualTo(1));
            Assert.That(method.Invoke(null, new object[] { new DummyAbilityManagerLineDataTarget { quickKey = 'd' } }), Is.EqualTo(4));
            Assert.That(method.Invoke(null, new object[] { new DummyAbilityManagerLineDataTarget { quickKey = 'j' } }), Is.EqualTo(10));
            Assert.That(method.Invoke(null, new object[] { new DummyAbilityManagerLineDataTarget { quickKey = 'k' } }), Is.Null);
            Assert.That(fallbackHotkey.Invoke(null, new object[] { 1 }), Is.EqualTo("1"));
            Assert.That(fallbackHotkey.Invoke(null, new object[] { 10 }), Is.EqualTo("0"));
            Assert.That(fallbackHotkey.Invoke(null, new object[] { 11 }), Is.Null);
        });
    }


    [Test]
    public void AbilityManagerLinePatch_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchAbilityManagerLine(harmony);

            var target = new DummyAbilityManagerLineTarget();
            target.setData(new DummyFallbackAbilityManagerLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("ability fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
