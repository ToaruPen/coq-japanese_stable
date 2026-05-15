using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DanceRitualOpponentTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
    }

    [TestCase("The {{Y|snapjaw}} is busy dancing!", "{{Y|snapjaw}}は踊りの最中だ！")]
    [TestCase("{{Y|glowfish}} are busy dancing!", "{{Y|glowfish}}は踊りの最中だ！")]
    public void DanceRitualOpponentFireEvent_TranslatesBusyDancingPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [Test]
    public void DanceRitualOpponentFireEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|snapjaw}} is busy dancing!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);

            DummyPopupShow.ShowFail(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DanceRitualOpponentFireEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("The snapjaw is busy dancing!"),
            "The snapjaw is busy dancing!");
    }

    [TestCase("")]
    [TestCase("The snapjaw is dancing.")]
    [TestCase("The snapjaw is busy waiting!")]
    public void DanceRitualOpponentFireEvent_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(harmony);

            DummyDanceRitualOpponentTarget.PopupMessageToShow = source;
            DummyDanceRitualOpponentTarget.FireEvent(new object());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyDanceRitualOpponentTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyDanceRitualOpponentTarget), nameof(DummyDanceRitualOpponentTarget.FireEvent), typeof(object)),
            prefix: new HarmonyMethod(RequireMethod(typeof(DanceRitualOpponentTranslationPatch), nameof(DanceRitualOpponentTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(DanceRitualOpponentTranslationPatch), nameof(DanceRitualOpponentTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyDanceRitualOpponentTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool FireEvent(object e)
        {
            _ = e;
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return false;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
        }
    }
}
