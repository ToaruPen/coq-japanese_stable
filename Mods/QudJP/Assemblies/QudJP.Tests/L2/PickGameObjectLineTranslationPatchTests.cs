using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void PickGameObjectLinePrefix_TranslatesCategoryItemAndMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("Artifacts", "遺物"),
            ("Laser Pistol", "レーザーピストル"),
            ("owned by you", "あなたの所有"),
            ("readied", "装備中"),
            ("Expand", "展開"),
            ("Collapse", "折りたたむ"),
            ("Select", "選択"));
        using var notePlayerOwnedOverride = StaticBooleanSettingOverride.ForResolvedType(
            "Qud.UI.PickGameObjectScreen",
            "PickGameObjectScreen",
            "NotePlayerOwned",
            value: true);
        using var showContextOverride = StaticBooleanSettingOverride.ForResolvedType(
            "Qud.UI.PickGameObjectScreen",
            "PickGameObjectScreen",
            "ShowContext",
            value: true);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPickGameObjectLineTarget), nameof(DummyPickGameObjectLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PickGameObjectLineTranslationPatch), nameof(PickGameObjectLineTranslationPatch.Prefix))));

            var categoryTarget = new DummyPickGameObjectLineTarget();
            categoryTarget.setData(new DummyPickGameObjectLineDataTarget
            {
                category = "Artifacts",
                collapsed = true,
            });

            var itemTarget = new DummyPickGameObjectLineTarget();
            itemTarget.setData(new DummyPickGameObjectLineDataTarget
            {
                go = new DummyPickGameObjectTargetObject
                {
                    DisplayName = "Laser Pistol",
                    OwnedByPlayer = true,
                    Weight = 7,
                    ListDisplayContext = "readied",
                },
                hotkeyDescription = "a",
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.text.Text, Is.EqualTo("[+] {{K|遺物}}"));
                Assert.That(itemTarget.text.Text, Does.Contain("レーザーピストル"));
                Assert.That(itemTarget.text.Text, Does.Contain("あなたの所有"));
                Assert.That(itemTarget.text.Text, Does.Contain("装備中"));
                Assert.That(itemTarget.rightFloatText.Text, Is.EqualTo("{{K|7#}}"));
                Assert.That(itemTarget.hotkey.Text, Is.EqualTo("{{Y|{{w|a}}}} "));
                Assert.That(DummyPickGameObjectLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyPickGameObjectLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyPickGameObjectLineTarget.itemOptions[0].Description, Is.EqualTo("選択"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(PickGameObjectLineTranslationPatch), "PickGameObjectLine.CategoryText"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(PickGameObjectLineTranslationPatch), "PickGameObjectLine.ItemText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(PickGameObjectLineTranslationPatch), "PickGameObjectLine.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PickGameObjectLinePrefix_UpdatesIconUsingGameRenderForUiSignature_WhenPatched()
    {
        WriteDictionary(("Laser Pistol", "レーザーピストル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPickGameObjectLineTarget), nameof(DummyPickGameObjectLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PickGameObjectLineTranslationPatch), nameof(PickGameObjectLineTranslationPatch.Prefix))));

            var item = new DummyGameSignaturePickGameObjectTargetObject
            {
                DisplayName = "Laser Pistol",
                Weight = 7,
            };
            var target = new DummyPickGameObjectLineTarget();
            target.icon.FromRenderable(new DummyRenderable("stale-water-tile"));

            target.setData(new DummyGameSignaturePickGameObjectLineDataTarget
            {
                go = item,
                hotkeyDescription = "a",
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("レーザーピストル"));
                Assert.That(target.icon.LastRenderable, Is.InstanceOf<DummyRenderable>());
                Assert.That(((DummyRenderable)target.icon.LastRenderable!).Tile, Is.EqualTo("Laser Pistol"));
                Assert.That(item.LastRenderContext, Is.Null);
                Assert.That(item.LastAsIfKnown, Is.False);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PickGameObjectLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPickGameObjectLineTarget), nameof(DummyPickGameObjectLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PickGameObjectLineTranslationPatch), nameof(PickGameObjectLineTranslationPatch.Prefix))));

            var target = new DummyPickGameObjectLineTarget();
            target.setData(new DummyFallbackPickGameObjectLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("pick fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
