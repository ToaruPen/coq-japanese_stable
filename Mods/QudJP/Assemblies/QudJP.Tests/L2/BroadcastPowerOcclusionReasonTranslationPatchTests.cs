using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BroadcastPowerOcclusionReasonTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyBroadcastPowerReceiverTarget.Reset();
    }

    [Test]
    public void HandleEvent_TranslatesGeneratedOcclusionReason_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyBroadcastPowerReceiverTarget.HandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyBroadcastPowerReceiverTarget.Postfix, Does.Contain("{{R|酸性雨}}"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });

        var outsideOwner = DummyHistoricStringExpander.ExpandString("acid rain");
        Assert.That(outsideOwner, Is.EqualTo("acid rain"));
    }

    [Test]
    public void HandleEvent_StripsDirectMarkerWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedHandleEvent(() =>
        {
            DummyBroadcastPowerReceiverTarget.OcclusionReason =
                MessageFrameTranslator.DirectTranslationMarker + "acid rain";

            DummyBroadcastPowerReceiverTarget.HandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyBroadcastPowerReceiverTarget.Postfix, Does.Contain("{{R|acid rain}}"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedHandleEvent(Action action)
    {
        var harmonyId = "qudjp.tests.broadcast-power-occlusion-reason." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyBroadcastPowerReceiverTarget),
                    nameof(DummyBroadcastPowerReceiverTarget.HandleEvent)),
                transpiler: new HarmonyMethod(RequireMethod(
                    typeof(BroadcastPowerOcclusionReasonTranslationPatch),
                    nameof(BroadcastPowerOcclusionReasonTranslationPatch.Transpiler),
                    typeof(IEnumerable<CodeInstruction>))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(BroadcastPowerOcclusionReasonTranslationPatch),
            nameof(BroadcastPowerOcclusionReasonTranslationPatch) + ".ExpandString");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyBroadcastPowerReceiverTarget
{
    public static string OcclusionReason { get; set; } = "acid rain";

    public static string Postfix { get; private set; } = string.Empty;

    public static void Reset()
    {
        OcclusionReason = "acid rain";
        Postfix = string.Empty;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void HandleEvent()
    {
        var reason = DummyHistoricStringExpander.ExpandString(OcclusionReason);
        Postfix = "\n{{rules|Satellite broadcast power is currently occluded by {{R|" + reason + "}}.}}";
    }
}
