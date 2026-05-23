using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MutationActionFailureTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
    }

    [Test]
    public void ElectricalGenerationHandleEvent_TranslatesDrinkChargeFailurePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            nameof(DummyMutationActionFailureTarget.ElectricalGenerationHandleEvent),
            "You can't seem to drink any of the juice from {{Y|drained chem cell}}.",
            "{{Y|drained chem cell}}から電荷を吸い取れないようだ。");
    }

    [Test]
    public void ElectricalGenerationPerformDischarge_TranslatesNoGroundTargetFailurePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            nameof(DummyMutationActionFailureTarget.ElectricalGenerationPerformDischarge),
            "There is nothing there that your electrical discharge can ground into.",
            "放電を接地できる対象がそこにはない。");
    }

    [Test]
    public void RepellingForceFireEvent_TranslatesWorldMapFailurePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            nameof(DummyMutationActionFailureTarget.RepellingForceFireEvent),
            "You cannot use {{G|Repulsion}} on the world map.",
            "{{G|Repulsion}}はワールドマップでは使えない。");
    }

    [Test]
    public void TeleportOtherFireEvent_TranslatesSelfTargetFailurePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            nameof(DummyMutationActionFailureTarget.TeleportOtherFireEvent),
            "You may not teleport yourself with Teleport Other!",
            "他者転送で自分自身を転送することはできない！");
    }

    [TestCase(
        nameof(DummyMutationActionFailureTarget.ElectricalGenerationHandleEvent),
        "You can't seem to drink any of the juice from {{Y|drained chem cell}}.")]
    [TestCase(
        nameof(DummyMutationActionFailureTarget.ElectricalGenerationPerformDischarge),
        "There is nothing there that your electrical discharge can ground into.")]
    [TestCase(
        nameof(DummyMutationActionFailureTarget.RepellingForceFireEvent),
        "You cannot use Repulsion on the world map.")]
    [TestCase(
        nameof(DummyMutationActionFailureTarget.TeleportOtherFireEvent),
        "You may not teleport yourself with Teleport Other!")]
    public void MutationActionFailure_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent(
        string methodName,
        string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchPopupShowFail(harmony);

            if (string.Equals(methodName, nameof(DummyMutationActionFailureTarget.TeleportOtherFireEvent), StringComparison.Ordinal)
                || string.Equals(methodName, nameof(DummyMutationActionFailureTarget.RepellingForceFireEvent), StringComparison.Ordinal)
                || string.Equals(methodName, nameof(DummyMutationActionFailureTarget.ElectricalGenerationPerformDischarge), StringComparison.Ordinal))
            {
                DummyPopupShow.ShowFail(source);
            }
            else
            {
                DummyPopupShow.Show(source);
            }

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void MutationActionFailure_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(harmony);

            const string source = "You may not teleport yourself with Teleport Other!";
            DummyMutationActionFailureTarget.PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source);
            InvokeOwnerMethod(nameof(DummyMutationActionFailureTarget.TeleportOtherFireEvent));

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            DummyMutationActionFailureTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("")]
    [TestCase("You can drink the juice from {{Y|charged cell}}.")]
    [TestCase("You may teleport yourself with Teleport Other.")]
    public void MutationActionFailure_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(nameof(DummyMutationActionFailureTarget.ElectricalGenerationHandleEvent), source, source);
        AssertPopupMessage(nameof(DummyMutationActionFailureTarget.TeleportOtherFireEvent), source, source);
    }

    private static void AssertPopupMessage(string methodName, string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchPopupShowFail(harmony);
            PatchOwner(harmony);

            DummyMutationActionFailureTarget.PopupMessageToShow = source;
            InvokeOwnerMethod(methodName);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyMutationActionFailureTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        foreach (var methodName in new[]
        {
            nameof(DummyMutationActionFailureTarget.ElectricalGenerationHandleEvent),
            nameof(DummyMutationActionFailureTarget.ElectricalGenerationPerformDischarge),
            nameof(DummyMutationActionFailureTarget.RepellingForceFireEvent),
            nameof(DummyMutationActionFailureTarget.TeleportOtherFireEvent),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMutationActionFailureTarget), methodName, typeof(object)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MutationActionFailureTranslationPatch), nameof(MutationActionFailureTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(MutationActionFailureTranslationPatch), nameof(MutationActionFailureTranslationPatch.Finalizer), typeof(Exception))));
        }
    }

    private static void InvokeOwnerMethod(string methodName)
    {
        _ = RequireMethod(typeof(DummyMutationActionFailureTarget), methodName, typeof(object))
            .Invoke(null, new object[] { new object() });
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

    private static class DummyMutationActionFailureTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ElectricalGenerationHandleEvent(object e)
        {
            _ = e;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ElectricalGenerationPerformDischarge(object e)
        {
            return ShowFailAndReturn(e, nameof(ElectricalGenerationPerformDischarge));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool RepellingForceFireEvent(object e)
        {
            return ShowFailAndReturn(e, nameof(RepellingForceFireEvent));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TeleportOtherFireEvent(object e)
        {
            return ShowFailAndReturn(e, nameof(TeleportOtherFireEvent));
        }

        private static bool ShowFailAndReturn(object e, string route)
        {
            _ = e;
            _ = route;
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return true;
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
        }
    }
}
