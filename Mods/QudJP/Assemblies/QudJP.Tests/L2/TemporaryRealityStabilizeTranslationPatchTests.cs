using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TemporaryRealityStabilizeTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [TestCase(
        "The {{Y|fugue clone}}'s worldline through spacetime snaps back to its canonical path, and {{Y|fugue clone}} vanishes.",
        "{{Y|fugue clone}}の時空を通る世界線が本来の経路へ戻り、{{Y|fugue clone}}は消滅した。")]
    [TestCase(
        "The {{Y|fugue clone}}'s worldline through spacetime snaps back to its canonical path, and it vanishes.",
        "{{Y|fugue clone}}の時空を通る世界線が本来の経路へ戻り、{{Y|fugue clone}}は消滅した。")]
    public void TemporaryRealityStabilize_TranslatesWorldlineMessages_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(source, expected, expectedColor: "white");
    }

    [Test]
    public void TemporaryRealityStabilize_DoesNotTranslateTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|fugue clone}}'s worldline through spacetime snaps back to its canonical path, and it vanishes.";
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
    public void TemporaryRealityStabilize_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            MessageFrameTranslator.MarkDirectTranslation("The fugue clone's worldline through spacetime snaps back to its canonical path, and it vanishes."),
            "The fugue clone's worldline through spacetime snaps back to its canonical path, and it vanishes.");
    }

    [TestCase("")]
    [TestCase("The fugue clone's worldline snaps back.")]
    [TestCase("The fugue clone vanishes.")]
    public void TemporaryRealityStabilize_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
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

            DummyTemporaryRealityStabilizeTarget.MessageToSend = source;
            DummyTemporaryRealityStabilizeTarget.ColorToSend = expectedColor;
            DummyTemporaryRealityStabilizeTarget.HandleEvent(new object());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(expectedColor));
            });
        }
        finally
        {
            DummyTemporaryRealityStabilizeTarget.Reset();
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
            original: RequireMethod(typeof(DummyTemporaryRealityStabilizeTarget), nameof(DummyTemporaryRealityStabilizeTarget.HandleEvent), typeof(object)),
            prefix: new HarmonyMethod(RequireMethod(typeof(TemporaryRealityStabilizeTranslationPatch), nameof(TemporaryRealityStabilizeTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(TemporaryRealityStabilizeTranslationPatch), nameof(TemporaryRealityStabilizeTranslationPatch.Finalizer), typeof(Exception))));
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

    private static class DummyTemporaryRealityStabilizeTarget
    {
        public static string MessageToSend { get; set; } = string.Empty;

        public static string? ColorToSend { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HandleEvent(object e)
        {
            _ = e;
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
