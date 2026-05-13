using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MentalShieldTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [TestCase(nameof(DummyMentalShieldTarget.HandleBeforeApplyDamageEvent))]
    [TestCase(nameof(DummyMentalShieldTarget.HandleBeginMentalDefendEvent))]
    public void MentalShield_TranslatesMentalAttackNoEffectMessage_WhenOwnerPatched(string methodName)
    {
        AssertQueuedMessage(
            methodName,
            "Your mental attack does not affect {{Y|forcefield}}.",
            "あなたの精神攻撃は{{Y|forcefield}}に効かない。",
            expectedColor: "white");
    }

    [Test]
    public void MentalShield_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "Your mental attack does not affect {{Y|forcefield}}.";
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
    public void MentalShield_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummyMentalShieldTarget.HandleBeforeApplyDamageEvent),
            MessageFrameTranslator.MarkDirectTranslation("Your mental attack does not affect the forcefield."),
            "Your mental attack does not affect the forcefield.");
    }

    [TestCase("")]
    [TestCase("Your attack does not affect {{Y|forcefield}}.")]
    [TestCase("Your mental attack affects {{Y|forcefield}}.")]
    public void MentalShield_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(
            nameof(DummyMentalShieldTarget.HandleBeforeApplyDamageEvent),
            source,
            source);
    }

    private static void AssertQueuedMessage(
        string ownerMethodName,
        string source,
        string expected,
        string? expectedColor = null)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, ownerMethodName);

            DummyMentalShieldTarget.MessageToSend = source;
            DummyMentalShieldTarget.ColorToSend = expectedColor;
            InvokeOwner(ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyMentalShieldTarget.Reset();
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

    private static void PatchOwner(Harmony harmony, string methodName)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyMentalShieldTarget), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(MentalShieldTranslationPatch), nameof(MentalShieldTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(MentalShieldTranslationPatch), nameof(MentalShieldTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void InvokeOwner(string methodName)
    {
        _ = methodName switch
        {
            nameof(DummyMentalShieldTarget.HandleBeforeApplyDamageEvent) => DummyMentalShieldTarget.HandleBeforeApplyDamageEvent(),
            nameof(DummyMentalShieldTarget.HandleBeginMentalDefendEvent) => DummyMentalShieldTarget.HandleBeginMentalDefendEvent(),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unknown owner method."),
        };
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

    private static class DummyMentalShieldTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HandleBeforeApplyDamageEvent()
        {
            return SendMessage();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HandleBeginMentalDefendEvent()
        {
            _ = nameof(HandleBeginMentalDefendEvent);
            return SendMessage();
        }

        private static bool SendMessage()
        {
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return false;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
