using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SoundManagerSetChannelTrackTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummySoundManagerTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummySoundManagerTarget.Reset();
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase("music: Music/Overworld", "music：Music/Overworld")]
    [TestCase("ambient: {{Y|Sounds/Ambience/Ruins}}", "ambient：{{Y|Sounds/Ambience/Ruins}}")]
    public void SetChannelTrack_TranslatesSoundLogTrackMessage_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerQueuedMessage(source, expected);
    }

    [Test]
    public void SetChannelTrack_PreservesMessageColor_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage("music: Music/Overworld", "music：Music/Overworld", "white");
    }

    [Test]
    public void SetChannelTrack_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "music: Music/Overworld";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("white"));
                Assert.That(HitCount(), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SetChannelTrack_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched()
    {
        const string source = "music: Music/Overworld";

        AssertOwnerQueuedMessage(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [Test]
    public void SetChannelTrack_LeavesEmptyMessageUnchanged_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(string.Empty, string.Empty, expectedHits: 0);
    }

    [Test]
    public void SetChannelTrack_LeavesDebugMissingTrackMessageUnchanged_WhenOwnerPatched()
    {
        const string source = "music: Music/Missing (Wasn't found)";

        AssertOwnerQueuedMessage(source, source, expectedHits: 0);
    }

    [Test]
    public void SetChannelTrack_LeavesUnsupportedMessageUnchanged_WhenOwnerPatched()
    {
        const string source = "Music/Overworld";

        AssertOwnerQueuedMessage(source, source, expectedHits: 0);
    }

    private static void AssertOwnerQueuedMessage(
        string source,
        string expected,
        string? color = null,
        int expectedHits = 1)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);
            PatchOwner(harmony);

            DummySoundManagerTarget.MessageToSend = source;
            DummySoundManagerTarget.ColorToSend = color;
            DummySoundManagerTarget.SetChannelTrack().GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo(color));
                Assert.That(HitCount(), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchMessageQueue(Harmony harmony)
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
        var sourceMethod = RequireMethod(typeof(DummySoundManagerTarget), nameof(DummySoundManagerTarget.SetChannelTrack));
        var moveNext = ResolveStateMachineMoveNext(sourceMethod)
            ?? throw new InvalidOperationException("Dummy SetChannelTrack state machine MoveNext not found.");

        harmony.Patch(
            original: moveNext,
            prefix: new HarmonyMethod(RequireMethod(typeof(SoundManagerSetChannelTrackTranslationPatch), nameof(SoundManagerSetChannelTrackTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(SoundManagerSetChannelTrackTranslationPatch), nameof(SoundManagerSetChannelTrackTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo? ResolveStateMachineMoveNext(MethodInfo sourceMethod)
    {
        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        return asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
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

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(SoundManagerSetChannelTrackTranslationPatch));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
