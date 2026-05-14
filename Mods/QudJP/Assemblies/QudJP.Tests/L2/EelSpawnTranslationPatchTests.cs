using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EelSpawnTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "A sewage eel tries to wrap itself around you, but cannot reach!",
        "下水ウナギがあなたに巻きつこうとしたが、届かなかった！",
        "EelSpawnCannotReach")]
    [TestCase(
        "A sewage eel tries to wrap itself around your feet, but passes through you!",
        "下水ウナギがあなたのfeetに巻きつこうとしたが、あなたをすり抜けた！",
        "EelSpawnPassesThrough")]
    public void HandleEvent_TranslatesWrapQueuedMessages_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyEelSpawnProducer
            {
                QueuedMessageToSend = source,
                ColorToSend = "white",
            }.HandleEvent(new DummyObjectEnteringCellEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("white"));
                Assert.That(QueueHitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void HandleEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "A sewage eel tries to wrap itself around you, but cannot reach!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);

            DummyMessageQueue.AddPlayerMessage(source, "white", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(QueueHitCount("EelSpawnCannotReach"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched()
    {
        const string source = "A sewage eel tries to wrap itself around you, but cannot reach!";

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyEelSpawnProducer
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(source),
            }.HandleEvent(new DummyObjectEnteringCellEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(QueueHitCount("EelSpawnCannotReach"), Is.Zero);
            });
        });
    }

    [TestCase(
        "A sewage eel wraps itself around you!",
        "下水ウナギがあなたに巻きついた！")]
    [TestCase(
        "A sewage eel wraps itself around your feet!",
        "下水ウナギがあなたのfeetに巻きついた！")]
    public void HandleEvent_TranslatesWrapPopup_WhenOwnerPatched(string source, string expected)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(EelSpawnTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyEelSpawnProducer
                {
                    PopupMessageToShow = source,
                }.HandleEvent(new DummyObjectEnteringCellEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(PopupHitCount("EelSpawnWrap"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "A sewage eel wraps itself around you!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(PopupHitCount("EelSpawnWrap"), Is.Zero);
        });
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "A sewage eel wraps itself around you!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(EelSpawnTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyEelSpawnProducer
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.HandleEvent(new DummyObjectEnteringCellEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(PopupHitCount("EelSpawnWrap"), Is.Zero);
                });
            });
    }

    [TestCase("You maintain your balance and kick the eel away.")]
    [TestCase("You maintain your balance and shake the eel off.")]
    public void HandleEvent_DoesNotClaimFixedBalancePopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(EelSpawnTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyEelSpawnProducer
                {
                    PopupMessageToShow = source,
                }.HandleEvent(new DummyObjectEnteringCellEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(PopupHitCount("EelSpawnWrap"), Is.Zero);
                });
            });
    }

    private static void WithPatchedOwnerAndQueue(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchMessageQueue(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchMessageQueue(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyMessageQueue),
                nameof(DummyMessageQueue.AddPlayerMessage),
                typeof(string),
                typeof(string),
                typeof(bool)),
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
            original: RequireOwnerMethod(),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(EelSpawnTranslationPatch),
                nameof(EelSpawnTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireOwnerMethod(),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(EelSpawnTranslationPatch),
                nameof(EelSpawnTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return RequireMethod(
            typeof(DummyEelSpawnProducer),
            nameof(DummyEelSpawnProducer.HandleEvent),
            typeof(DummyObjectEnteringCellEvent));
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.FullName, methodName);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.eelspawn." + Guid.NewGuid().ToString("N");
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(EelSpawnTranslationPatch) + "." + detail);
    }

    private static int PopupHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(EelSpawnTranslationPatch), detail);
    }

    private sealed class DummyEelSpawnProducer
    {
        public string QueuedMessageToSend { get; set; } = string.Empty;
        public string PopupMessageToShow { get; set; } = string.Empty;
        public string? ColorToSend { get; set; }

        public void HandleEvent(DummyObjectEnteringCellEvent e)
        {
            _ = e;

            if (!string.IsNullOrEmpty(QueuedMessageToSend))
            {
                DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, ColorToSend, Capitalize: false);
            }

            if (!string.IsNullOrEmpty(PopupMessageToShow))
            {
                DummyPopupShow.Show(PopupMessageToShow);
            }
        }
    }
}
