using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LongBladesCoreTranslationPatchTests
{
    private static readonly string[] PopupDetails =
    {
        "AggressiveLungeBlocked",
        "PlayerLungePassesThrough",
    };

    private static readonly string[] QueueDetails =
    {
        "GuardDownCountdown",
        "ActorLungeInterrupted",
        "ActorLungePassesThrough",
        "StanceRequired",
        "AggressiveSwipePlayer",
        "AggressiveSwipeObserver",
        "DefensiveSwipePlayer",
        "DefensiveSwipeObserver",
        "EnGarde",
    };

    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DummyMessageQueue.Reset();
        DummyPopupShow.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You can't aggressively lunge through {{Y|snapjaw}}.",
        "{{Y|snapjaw}}を通り抜けて攻勢ランジすることはできない。",
        "AggressiveLungeBlocked")]
    [TestCase(
        "Your lunge passes through {{Y|snapjaw}}.",
        "あなたのランジは{{Y|snapjaw}}をすり抜けた。",
        "PlayerLungePassesThrough")]
    public void OwnerRoute_TranslatesOwnerPopups_WhenOwnerPatched(string source, string expected, string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(LongBladesCoreTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyLongBladesCoreProducer
                {
                    PopupMessageToShow = source,
                }.FireEvent(new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(PopupHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "1 turn remains until your guard is down.",
        "ガードが下がるまであと1ターン。",
        "GuardDownCountdown")]
    [TestCase(
        "2 turns remain until your guard is down.",
        "ガードが下がるまであと2ターン。",
        "GuardDownCountdown")]
    [TestCase(
        "{{Y|snapjaw}}'s lunge is interrupted.",
        "{{Y|snapjaw}}のランジは中断された。",
        "ActorLungeInterrupted")]
    [TestCase(
        "{{Y|snapjaw}}'s lunge passes through {{C|phase spider}}.",
        "{{Y|snapjaw}}のランジは{{C|phase spider}}をすり抜けた。",
        "ActorLungePassesThrough")]
    [TestCase(
        "You must be in a long blade stance to use that ability.",
        "ロングブレードの型に入っていないとそのアビリティは使えない。",
        "StanceRequired")]
    [TestCase(
        "You aggressively swipe your blade in the air.",
        "刃を空中で荒々しく振り払った。",
        "AggressiveSwipePlayer")]
    [TestCase(
        "{{Y|snapjaw}} aggressivelyswipes its blade in the air.",
        "{{Y|snapjaw}}は刃を空中で荒々しく振り払った。",
        "AggressiveSwipeObserver")]
    [TestCase(
        "You swipe your blade in the air, pushing your enemies backward.",
        "刃を空中で薙ぎ払い、敵を後退させた。",
        "DefensiveSwipePlayer")]
    [TestCase(
        "{{Y|snapjaw}}swipes its blade in the air, pushing its foes backward.",
        "{{Y|snapjaw}}は刃を空中で薙ぎ払い、敵を後退させた。",
        "DefensiveSwipeObserver")]
    [TestCase(
        "{{G|En garde!}}",
        "{{G|構えよ！}}",
        "EnGarde")]
    public void OwnerRoute_TranslatesOwnerQueuedMessages_WhenOwnerPatched(string source, string expected, string detail)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyLongBladesCoreProducer
            {
                QueuedMessageToSend = source,
            }.FireEvent(new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(QueueHitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void OwnerRoute_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Your lunge passes through {{Y|snapjaw}}.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(TotalPopupHitCount(), Is.Zero);
        });
    }

    [Test]
    public void OwnerRoute_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        PatchMessageQueueOnly(() => DummyMessageQueue.AddPlayerMessage("{{G|En garde!}}", "G", Capitalize: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("{{G|En garde!}}"));
            Assert.That(TotalQueueHitCount(), Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("custom long blades message")]
    public void OwnerRoute_DoesNotClaimEmptyOrUnsupportedQueuedMessages_WhenOwnerPatched(string source)
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyLongBladesCoreProducer
            {
                QueuedMessageToSend = source,
            }.FireEvent(new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(source));
                Assert.That(TotalQueueHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void OwnerRoute_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        const string popup = "Your lunge passes through {{Y|snapjaw}}.";
        const string queued = "{{G|En garde!}}";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(LongBladesCoreTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyLongBladesCoreProducer
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(popup),
                }.FireEvent(new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(popup));
                    Assert.That(TotalPopupHitCount(), Is.Zero);
                });
            });

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyLongBladesCoreProducer
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(queued),
            }.FireEvent(new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(queued));
                Assert.That(TotalQueueHitCount(), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You must have a long blade equipped to switch stances.")]
    [TestCase("There's nothing there to lunge at.")]
    [TestCase("There's nothing there you can lunge at.")]
    [TestCase("Your lunge is interrupted.")]
    [TestCase("There's nothing there to lunge away from.")]
    [TestCase("You must be in a long blade stance to use that ability.")]
    [TestCase("There's nothing there to swipe at.")]
    [TestCase("There's nothing there you can swipe at.")]
    [TestCase("You must have a long blade equipped to effectively yell out 'En garde!'")]
    public void OwnerRoute_DoesNotClaimFixedPopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(LongBladesCoreTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyLongBladesCoreProducer
                {
                    PopupMessageToShow = source,
                }.FireEvent(new DummyEvent());

                Assert.That(TotalPopupHitCount(), Is.Zero);
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

    private static void PatchMessageQueueOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchMessageQueue(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchMessageQueue(Harmony harmony)
    {
        var target = OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: target,
            prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
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
            prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                typeof(LongBladesCoreTranslationPatch),
                nameof(LongBladesCoreTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                typeof(LongBladesCoreTranslationPatch),
                nameof(LongBladesCoreTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyLongBladesCoreProducer),
            nameof(DummyLongBladesCoreProducer.FireEvent),
            typeof(DummyEvent));
    }

    private static int TotalPopupHitCount()
    {
        var total = 0;
        foreach (var detail in PopupDetails)
        {
            total += PopupHitCount(detail);
        }

        return total;
    }

    private static int TotalQueueHitCount()
    {
        var total = 0;
        foreach (var detail in QueueDetails)
        {
            total += QueueHitCount(detail);
        }

        return total;
    }

    private static int PopupHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(LongBladesCoreTranslationPatch), detail);
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(LongBladesCoreTranslationPatch) + "." + detail);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.long-blades-core-l2." + Guid.NewGuid().ToString("N");
    }

    private sealed class DummyLongBladesCoreProducer
    {
        public string? PopupMessageToShow { get; set; }

        public string? QueuedMessageToSend { get; set; }

        public void FireEvent(DummyEvent eventContext)
        {
            _ = eventContext;

            if (PopupMessageToShow is not null)
            {
                DummyPopupShow.Show(PopupMessageToShow);
            }

            if (QueuedMessageToSend is not null)
            {
                DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, "white", Capitalize: false);
            }
        }
    }
}
