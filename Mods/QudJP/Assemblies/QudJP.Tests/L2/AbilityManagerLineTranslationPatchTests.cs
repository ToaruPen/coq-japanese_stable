using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void AbilityManagerLinePrefix_TranslatesCategoryAbilityAndMenuOptions_WhenPatched()
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
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerLineTarget), nameof(DummyAbilityManagerLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(AbilityManagerLineTranslationPatch), nameof(AbilityManagerLineTranslationPatch.Prefix))));

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
    public void AbilityManagerLinePrefix_TranslatesGeneratedReleaseGasAbilityName_FromMutationDisplayName()
    {
        WriteDictionary(
            ("turn cooldown", "ターンのクールダウン"),
            ("Toggled on", "オン"));
        WriteMutationsXml(("Corrosive Gas Generation", "腐食性ガス生成"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerLineTarget), nameof(DummyAbilityManagerLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(AbilityManagerLineTranslationPatch), nameof(AbilityManagerLineTranslationPatch.Prefix))));

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

            Assert.Multiple(() =>
            {
                Assert.That(abilityTarget.text.Text, Does.Contain("腐食性ガス放出"));
                Assert.That(abilityTarget.text.Text, Does.Not.Contain("Release Corrosive Gas"));
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
    public void AbilityManagerLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerLineTarget), nameof(DummyAbilityManagerLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(AbilityManagerLineTranslationPatch), nameof(AbilityManagerLineTranslationPatch.Prefix))));

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
