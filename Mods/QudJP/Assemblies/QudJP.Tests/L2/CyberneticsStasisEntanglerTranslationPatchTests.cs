using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CyberneticsStasisEntanglerTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [TestCase(
        "{{Y|Stasis fields}} appear all around.",
        "{{Y|Stasis fields}}が周囲一帯に出現した。")]
    [TestCase(
        "Several {{Y|stasis fields}} appear nearby.",
        "いくつかの{{Y|stasis fields}}が近くに出現した。")]
    public void CyberneticsStasisEntangler_TranslatesDeployMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void CyberneticsStasisEntangler_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "{{Y|Stasis fields}} appear all around.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CyberneticsStasisEntangler_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Stasis fields appear all around."),
            "Stasis fields appear all around.");
    }

    [TestCase("")]
    [TestCase("Stasis fields disappear all around.")]
    [TestCase("Several stasis fields shimmer nearby.")]
    public void CyberneticsStasisEntangler_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(source, source);
    }

    private static void AssertQueuedMessage(
        string source,
        string expected,
        string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony);

            DummyCyberneticsStasisEntanglerTarget.MessageToSend = source;
            DummyCyberneticsStasisEntanglerTarget.ColorToSend = expectedColor;
            DummyCyberneticsStasisEntanglerTarget.DeployToCells(new object(), new object(), new object(), 0, 0);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyCyberneticsStasisEntanglerTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CombatAndLogMessageQueuePatch), nameof(CombatAndLogMessageQueuePatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage), typeof(string), typeof(string), typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(MessageLogPatch), nameof(MessageLogPatch.Prefix), typeof(string).MakeByRefType(), typeof(string), typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyCyberneticsStasisEntanglerTarget), nameof(DummyCyberneticsStasisEntanglerTarget.DeployToCells), typeof(object), typeof(object), typeof(object), typeof(int), typeof(int)),
            prefix: new HarmonyMethod(RequireMethod(typeof(CyberneticsStasisEntanglerTranslationPatch), nameof(CyberneticsStasisEntanglerTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(CyberneticsStasisEntanglerTranslationPatch), nameof(CyberneticsStasisEntanglerTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyCyberneticsStasisEntanglerTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static object DeployToCells(object zone, object actor, object target, int computePower, int realityStabilizationPenetration)
        {
            _ = zone;
            _ = actor;
            _ = target;
            _ = computePower;
            _ = realityStabilizationPenetration;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return target;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
