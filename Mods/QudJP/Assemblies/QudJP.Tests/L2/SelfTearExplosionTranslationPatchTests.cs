using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SelfTearExplosionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [TestCase(
        nameof(DummySelfTearExplosionTarget.ClockworkFireEvent),
        "The {{Y|clockwork beetle}}'s clockwork tears itself apart!",
        "{{Y|clockwork beetle}}のclockworkが自壊した！")]
    [TestCase(
        nameof(DummySelfTearExplosionTarget.FlywheelFireEvent),
        "The {{Y|gyrocopter}}'s flywheel tears itself apart!",
        "{{Y|gyrocopter}}のflywheelが自壊した！")]
    public void SelfTearExplosion_TranslatesOwnerMessage_WhenOwnerPatched(
        string methodName,
        string source,
        string expected)
    {
        AssertQueuedMessage(methodName, source, expected, expectedColor: "R");
    }

    [Test]
    public void SelfTearExplosion_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|clockwork beetle}}'s clockwork tears itself apart!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "R", Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SelfTearExplosion_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummySelfTearExplosionTarget.ClockworkFireEvent),
            MessageFrameTranslator.MarkDirectTranslation("The clockwork beetle's clockwork tears itself apart!"),
            "The clockwork beetle's clockwork tears itself apart!");
    }

    [TestCase("")]
    [TestCase("The clockwork beetle's clockwork tears apart.")]
    [TestCase("The clockwork beetle is torn apart!")]
    public void SelfTearExplosion_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertQueuedMessage(nameof(DummySelfTearExplosionTarget.ClockworkFireEvent), source, source);
        AssertQueuedMessage(nameof(DummySelfTearExplosionTarget.FlywheelFireEvent), source, source);
    }

    private static void AssertQueuedMessage(
        string methodName,
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

            DummySelfTearExplosionTarget.MessageToSend = source;
            DummySelfTearExplosionTarget.ColorToSend = expectedColor;
            InvokeOwnerMethod(methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummySelfTearExplosionTarget.Reset();
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
        foreach (var methodName in new[]
        {
            nameof(DummySelfTearExplosionTarget.ClockworkFireEvent),
            nameof(DummySelfTearExplosionTarget.FlywheelFireEvent),
        })
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySelfTearExplosionTarget), methodName, typeof(object)),
                prefix: new HarmonyMethod(RequireMethod(typeof(SelfTearExplosionTranslationPatch), nameof(SelfTearExplosionTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(SelfTearExplosionTranslationPatch), nameof(SelfTearExplosionTranslationPatch.Finalizer), typeof(Exception))));
        }
    }

    private static void InvokeOwnerMethod(string methodName)
    {
        _ = RequireMethod(typeof(DummySelfTearExplosionTarget), methodName, typeof(object))
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

    private static class DummySelfTearExplosionTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;
        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ClockworkFireEvent(object e)
        {
            _ = e;
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool FlywheelFireEvent(object e)
        {
            _ = e;
            _ = nameof(FlywheelFireEvent);
            DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
            return true;
        }

        public static void Reset()
        {
            MessageToSend = string.Empty;
            ColorToSend = null;
        }
    }
}
