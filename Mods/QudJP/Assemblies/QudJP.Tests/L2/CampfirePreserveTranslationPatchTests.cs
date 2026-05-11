using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfirePreserveTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        CampfirePreserveTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        CampfirePreserveTranslationPatch.ResetForTests();
    }

    [TestCase(
        "You preserved:\n\nan apple into 1 serving of dried apple.",
        "保存した:\n\nan appleを1 servingのdried appleに保存した。")]
    [TestCase(
        "You preserved:\n\n{{G|two-faced banana}} into 2 servings of {{W|dried banana}}.\n{{Y|glowfish}} into 1 dram of {{C|glowfish paste}}.",
        "保存した:\n\n{{G|two-faced banana}}を2 servingsの{{W|dried banana}}に保存した。\n{{Y|glowfish}}を1 dramの{{C|glowfish paste}}に保存した。")]
    public void Preserve_TranslatesGeneratedPreservedPopup_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.Preserve)),
            source,
            expected);
    }

    [Test]
    public void PreserveExotic_TranslatesGeneratedPreservedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.PreserveExotic)),
            "You preserved:\n\n{{M|phase fruit}} into 3 servings of {{C|phase preserves}}.",
            "保存した:\n\n{{M|phase fruit}}を3 servingsの{{C|phase preserves}}に保存した。");
    }

    [Test]
    public void CampfirePreserve_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You preserved:\n\nan apple into 1 serving of dried apple.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You preserved:\n\nan apple into 1 serving of dried apple."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CampfirePreserve_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.Preserve)),
            MessageFrameTranslator.MarkDirectTranslation("You preserved:\n\nan apple into 1 serving of dried apple."),
            "You preserved:\n\nan apple into 1 serving of dried apple.");
    }

    [Test]
    public void CampfirePreserve_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(
            RequireMethod(typeof(DummyCampfirePreserveTarget), nameof(DummyCampfirePreserveTarget.Preserve)),
            string.Empty,
            string.Empty);
    }

    private static void AssertPopupMessage(MethodInfo ownerMethod, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, ownerMethod);

            var target = new DummyCampfirePreserveTarget
            {
                PopupMessageToSend = source,
            };
            _ = ownerMethod.Invoke(target, Array.Empty<object>());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(CampfirePreserveTranslationPatch), nameof(CampfirePreserveTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(CampfirePreserveTranslationPatch), nameof(CampfirePreserveTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        if (parameters.Length == 0)
        {
            var methodByName = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(methodByName, Is.Not.Null, $"{type.FullName}.{name} not found");
            return methodByName!;
        }

        var method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
            binder: null,
            types: parameters,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";
}
