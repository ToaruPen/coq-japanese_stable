using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MutationInfectionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        StatusScreenPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        StatusScreenPopupTranslationPatch.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [TestCase(
        "You gain {{C|Light Manipulation}}!",
        "{{C|光操作}}を得た！")]
    [TestCase(
        "You gain Quills!",
        "棘を得た！")]
    public void MutationInfectionFireEvent_TranslatesGainedMutationPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [Test]
    public void MutationInfectionFireEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You gain {{C|Light Manipulation}}!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MutationInfectionFireEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You gain Light Manipulation!"),
            "You gain Light Manipulation!");
    }

    [TestCase("")]
    [TestCase("You lose Quills!")]
    [TestCase("You gain a level.")]
    public void MutationInfectionFireEvent_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);

            DummyMutationInfectionTarget.PopupMessageToShow = source;
            _ = DummyMutationInfectionTarget.FireEvent(new object());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyMutationInfectionTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMutationInfectionTarget), nameof(DummyMutationInfectionTarget.FireEvent), typeof(object)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MutationInfectionTranslationPatch), nameof(MutationInfectionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(MutationInfectionTranslationPatch), nameof(MutationInfectionTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Localization"));
    }

    private static class DummyMutationInfectionTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool FireEvent(object e)
        {
            _ = e;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
        }
    }
}
