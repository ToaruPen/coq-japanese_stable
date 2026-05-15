using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EngraverTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
    }

    [TestCase(
        "You engrave the mark of death on your right hand.",
        "あなたはあなたのright handに死の印を刻んだ。")]
    [TestCase(
        "You engrave {{W|a tiny spiral}} on {{Y|Issachari rifler}}'s left arm.",
        "あなたは{{Y|Issachari rifler}}'s left armに{{W|a tiny spiral}}を刻んだ。")]
    public void EngraverAttemptEngrave_TranslatesSuccessPopups_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [Test]
    public void EngraverAttemptEngrave_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "You engrave the mark of death on your right hand.";
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
    public void EngraverAttemptEngrave_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You engrave a tiny spiral on your right hand."),
            "You engrave a tiny spiral on your right hand.");
    }

    [Test]
    public void EngraverAttemptEngrave_LeavesUnsupportedDirectMarkedPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "You tattoo the mark of death on your right hand.";

        AssertPopupMessage(MessageFrameTranslator.MarkDirectTranslation(source), source);
    }

    [TestCase("")]
    [TestCase("You tattoo the mark of death on your right hand.")]
    [TestCase("You engrave a tiny spiral.")]
    public void EngraverAttemptEngrave_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
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

            DummyEngraverTarget.PopupMessageToShow = source;
            DummyEngraverTarget.AttemptEngrave(new object());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyEngraverTarget.Reset();
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
            original: RequireMethod(typeof(DummyEngraverTarget), nameof(DummyEngraverTarget.AttemptEngrave), typeof(object)),
            prefix: new HarmonyMethod(RequireMethod(typeof(EngraverTranslationPatch), nameof(EngraverTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(EngraverTranslationPatch), nameof(EngraverTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyEngraverTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool AttemptEngrave(object actor)
        {
            _ = actor;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
        }
    }
}
