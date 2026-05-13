using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DamagePenetrationDebugTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [TestCase("Penned with Roll:6 Final:12", "貫通成功 ロール:6 最終:12")]
    [TestCase("Didn't pen with -1 Final:5", "貫通失敗 ロール:-1 最終:5")]
    [TestCase(
        "{{K|Penning Bonus: 7 Max: 5 Used: 5 Target: 11(Penned 2 times)}}",
        "{{K|貫通ボーナス: 7 最大: 5 使用: 5 目標: 11(貫通 2 回)}}")]
    public void DamagePenetrationDebug_TranslatesDebugMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void DamagePenetrationDebug_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "Penned with Roll:6 Final:12";
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
    public void DamagePenetrationDebug_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("Penned with Roll:6 Final:12"),
            "Penned with Roll:6 Final:12");
    }

    [TestCase("")]
    [TestCase("Penned with Roll:6")]
    [TestCase("Penning Bonus: 7 Max: 5 Used: 5 Target: 11")]
    public void DamagePenetrationDebug_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
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

            DummyDamagePenetrationTarget.MessageToSend = source;
            DummyDamagePenetrationTarget.ColorToSend = expectedColor;
            DummyDamagePenetrationTarget.RollDamagePenetrations();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyDamagePenetrationTarget.Reset();
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
            original: RequireMethod(typeof(DummyDamagePenetrationTarget), nameof(DummyDamagePenetrationTarget.RollDamagePenetrations)),
            prefix: new HarmonyMethod(RequireMethod(typeof(DamagePenetrationDebugTranslationPatch), nameof(DamagePenetrationDebugTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(DamagePenetrationDebugTranslationPatch), nameof(DamagePenetrationDebugTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyDamagePenetrationTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int RollDamagePenetrations()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return 0;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
