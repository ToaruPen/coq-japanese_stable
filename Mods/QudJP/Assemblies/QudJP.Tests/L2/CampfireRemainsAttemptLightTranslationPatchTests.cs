using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireRemainsAttemptLightTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
    }

    [TestCase(
        "You cannot light the {{Y|campfire remains}} while it is in the {{C|pool of water}}.",
        "{{Y|campfire remains}}が{{C|pool of water}}の中にある間は、火をつけられない。")]
    [TestCase(
        "You cannot light the {{Y|campfire remains}} while they are in the {{C|pool of water}}.",
        "{{Y|campfire remains}}が{{C|pool of water}}の中にある間は、火をつけられない。")]
    public void AttemptLight_TranslatesExtinguishingPoolPopup_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [Test]
    public void AttemptLight_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You cannot light the {{Y|campfire remains}} while it is in the {{C|pool of water}}.";
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
    public void AttemptLight_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You cannot light the campfire remains while it is in the pool of water.";
        AssertPopupMessage(MessageFrameTranslator.MarkDirectTranslation(source), source);
    }

    [TestCase("")]
    [TestCase("You cannot douse the campfire remains while it is in the pool of water.")]
    [TestCase("You cannot light the campfire remains while it is under the pool of water.")]
    public void AttemptLight_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
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

            DummyCampfireRemainsTarget.PopupMessageToShow = source;
            DummyCampfireRemainsTarget.AttemptLight(new object());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyCampfireRemainsTarget.Reset();
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
            original: RequireMethod(typeof(DummyCampfireRemainsTarget), nameof(DummyCampfireRemainsTarget.AttemptLight), typeof(object)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CampfireRemainsAttemptLightTranslationPatch), nameof(CampfireRemainsAttemptLightTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(CampfireRemainsAttemptLightTranslationPatch), nameof(CampfireRemainsAttemptLightTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyCampfireRemainsTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AttemptLight(object who)
        {
            _ = who;
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
        }
    }
}
