using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void AccessibilityLocalizationPostfix_AppliesToDummyAccessibilityManager()
    {
        var original = RequireMethod(typeof(DummyAccessibilityManager), nameof(DummyAccessibilityManager.Localize_Internal));
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: original,
                postfix: new HarmonyMethod(RequireMethod(typeof(AccessibilityLocalizationPatch), nameof(AccessibilityLocalizationPatch.Postfix))));

            var patchInfo = Harmony.GetPatchInfo(original);

            Assert.That(patchInfo, Is.Not.Null);
            Assert.That(patchInfo!.Postfixes.Any(patch => patch.owner == harmonyId), Is.True);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AccessibilityLocalizationPostfix_ReturnsJapanese_ForKnownKey()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAccessibilityManager), nameof(DummyAccessibilityManager.Localize_Internal)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AccessibilityLocalizationPatch), nameof(AccessibilityLocalizationPatch.Postfix))));

            var result = DummyAccessibilityManager.Localize_Internal("Desktop_HintButton");

            Assert.That(result, Is.EqualTo("Enterキーで選択"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AccessibilityLocalizationPostfix_PassesThroughUnknownKey()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAccessibilityManager), nameof(DummyAccessibilityManager.Localize_Internal)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AccessibilityLocalizationPatch), nameof(AccessibilityLocalizationPatch.Postfix))));

            var result = DummyAccessibilityManager.Localize_Internal("Unknown_Key");

            Assert.That(result, Is.EqualTo("Unknown_Key"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
