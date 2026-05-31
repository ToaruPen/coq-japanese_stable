using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DanceRitualOpponentTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase("The {{Y|snapjaw}} is busy dancing!", "{{Y|snapjaw}}は踊りの最中だ！")]
    [TestCase("{{Y|glowfish}} are busy dancing!", "{{Y|glowfish}}は踊りの最中だ！")]
    public void DanceRitualOpponentFireEvent_TranslatesBusyDancingPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertPopupMessage(source, expected);
    }

    [Test]
    public void DanceRitualOpponentFireEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|snapjaw}} is busy dancing!";
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
    public void DanceRitualOpponentFireEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("The snapjaw is busy dancing!"),
            "The snapjaw is busy dancing!");
    }

    [TestCase("")]
    [TestCase("The snapjaw is dancing.")]
    [TestCase("The snapjaw is busy waiting!")]
    public void DanceRitualOpponentFireEvent_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source);
    }

    [TestCase("&KDebug: Angor taking a turn...", "&Kデバッグ: Angorがターンを実行中...")]
    [TestCase("&KDebug: Dance Phase Ends Positive:3 Negative:2", "&Kデバッグ: ダンスフェーズ終了 成功:3 失敗:2")]
    [TestCase("&KDebug: Angor chooses Mimic", "&Kデバッグ: AngorがMimicを選択")]
    public void DanceRitualOpponentHandleEvent_TranslatesDebugQueue_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertQueuedMessage(
            nameof(DummyDanceRitualOpponentTarget.HandleBeforeAiTakingAction),
            source,
            expected,
            "HandleEvent.Debug");
    }

    [Test]
    public void DanceRitualOpponentRegister_TranslatesDebugQueue_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummyDanceRitualOpponentTarget.Register),
            "Debug: Angor Began The Dance",
            "デバッグ: Angorがダンスを始めた",
            "Register.Debug");
    }

    [Test]
    public void DanceRitualOpponent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "&KDebug: Angor taking a turn...";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, null, Capitalize: false);

            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DanceRitualOpponent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        AssertQueuedMessage(
            nameof(DummyDanceRitualOpponentTarget.HandleBeforeAiTakingAction),
            MessageFrameTranslator.MarkDirectTranslation("&KDebug: Angor taking a turn..."),
            "&KDebug: Angor taking a turn...",
            "HandleEvent.Debug",
            expectedHitCount: 0);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(harmony);

            DummyDanceRitualOpponentTarget.PopupMessageToShow = source;
            DummyDanceRitualOpponentTarget.FireEvent(new object());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            DummyDanceRitualOpponentTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchQueue(Harmony harmony)
    {
        var target = RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyDanceRitualOpponentTarget), nameof(DummyDanceRitualOpponentTarget.FireEvent), typeof(object)),
            prefix: new HarmonyMethod(RequireMethod(typeof(DanceRitualOpponentTranslationPatch), nameof(DanceRitualOpponentTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(DanceRitualOpponentTranslationPatch), nameof(DanceRitualOpponentTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static void AssertQueuedMessage(
        string ownerMethodName,
        string source,
        string expected,
        string expectedDetail,
        int expectedHitCount = 1)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyDanceRitualOpponentTarget), ownerMethodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(DanceRitualOpponentTranslationPatch), nameof(DanceRitualOpponentTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(DanceRitualOpponentTranslationPatch), nameof(DanceRitualOpponentTranslationPatch.Finalizer), typeof(Exception))));

            DummyDanceRitualOpponentTarget.QueueMessageToSend = source;
            RequireMethod(typeof(DummyDanceRitualOpponentTarget), ownerMethodName).Invoke(null, null);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(QueueHitCount(expectedDetail), Is.EqualTo(expectedHitCount));
            });
        }
        finally
        {
            DummyDanceRitualOpponentTarget.Reset();
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(DanceRitualOpponentTranslationPatch) + "." + detail);
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

    private static class DummyDanceRitualOpponentTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;
        public static string QueueMessageToSend { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool FireEvent(object e)
        {
            _ = e;
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HandleBeforeAiTakingAction()
        {
            DummyMessageQueue.AddPlayerMessage(QueueMessageToSend, null, Capitalize: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register()
        {
            DummyMessageQueue.AddPlayerMessage(QueueMessageToSend, "K", Capitalize: false);
        }

        public static void Reset()
        {
            PopupMessageToShow = string.Empty;
            QueueMessageToSend = string.Empty;
        }
    }
}
