using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ITeleporterTranslationPatchTests
{
    private static readonly string[] PopupDetails =
    {
        "ProtocolThinWorldThickWorld",
        "ProtocolThinWorldPresentContext",
        "ProtocolPresentContext",
        "RemotePocketDimension",
        "RustedActivationButton",
        "Broken",
        "Booting",
        "PartialChargeShutdown",
        "NoCharge",
    };

    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(RepositoryDictionaryDirectory());
        MessageFrameTranslator.SetDictionaryPathForTests(RepositoryMessageFramePath());
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
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "{{Y|recoiler}} is encoded with an imprint of the Thin World that has no meaning in the Thick World.",
        "{{Y|recoiler}}はThick Worldでは意味を持たないThin Worldの刻印を帯びている。",
        "ProtocolThinWorldThickWorld")]
    [TestCase(
        "{{Y|recoiler}} is encoded with an imprint of the Thin World that has no meaning in your present context.",
        "{{Y|recoiler}}は現在のコンテキストでは意味を持たないThin Worldの刻印を帯びている。",
        "ProtocolThinWorldPresentContext")]
    [TestCase(
        "{{Y|recoiler}} is encoded with an imprint that has no meaning in your present context.",
        "{{Y|recoiler}}は現在のコンテキストでは意味を持たない刻印を帯びている。",
        "ProtocolPresentContext")]
    [TestCase(
        "{{Y|recoiler}} is encoded with the imprint of a remote pocket dimension, {{B|Palladium Reef}}, that is inaccessible from your present vibrational plane.",
        "{{Y|recoiler}}は現在の振動面からは到達できない遠隔ポケット次元{{B|Palladium Reef}}の刻印を帯びている。",
        "RemotePocketDimension")]
    [TestCase(
        "{{Y|recoiler}}'s activation button is rusted in place.",
        "{{Y|recoiler}}の起動ボタンは錆びついて動かない。",
        "RustedActivationButton")]
    [TestCase("{{Y|recoiler}} is broken...", "{{Y|recoiler}}は壊れている...", "Broken")]
    [TestCase("{{Y|recoiler}} is still starting up.", "{{Y|recoiler}}はまだ起動中だ。", "Booting")]
    [TestCase(
        "{{Y|recoiler}} hums for a moment, then powers down. It doesn't have enough charge to function.",
        "{{Y|recoiler}}は一瞬うなったあと停止した。機能するだけのチャージが足りない。",
        "PartialChargeShutdown")]
    [TestCase(
        "{{Y|recoiler}} doesn't have enough charge to function.",
        "{{Y|recoiler}}には機能するだけのチャージが足りない。",
        "NoCharge")]
    public void AttemptTeleport_TranslatesOwnerPopups_WhenOwnerPatched(string source, string expected, string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ITeleporterTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyITeleporterProducer
                {
                    PopupMessageToShow = source,
                }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(PopupHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void AttemptTeleport_TranslatesQueuedActivation_WhenOwnerPatched()
    {
        WithPatchedOwnerAndQueue(() =>
        {
            new DummyITeleporterProducer
            {
                QueuedMessageToSend = "You activate the recoiler.",
            }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("リコイラーを起動した。"));
                Assert.That(QueueHitCount("ActivateRecoiler"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void AttemptTeleport_TranslatesMarkedDoesVerbPopup_WhenOwnerPatched()
    {
        const string subject = "The リコイラー";
        var source = DoesVerbRouteTranslator.MarkDoesFragment(
            subject + " is",
            "are",
            subject.Length,
            null) + " encoded with an imprint of the Thin World that has no meaning in the Thick World.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ITeleporterTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyITeleporterProducer
                {
                    PopupMessageToShow = source,
                }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("リコイラーは薄界の刻印で符号化されているが、厚界では意味を成さない"));
                    Assert.That(PopupHitCount("DoesVerb"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void AttemptTeleport_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "{{Y|recoiler}} is still starting up.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowFail(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(TotalPopupHitCount(), Is.Zero);
        });
    }

    [Test]
    public void AttemptTeleport_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent()
    {
        PatchMessageQueueOnly(() => DummyMessageQueue.AddPlayerMessage("You activate the recoiler.", "white", Capitalize: false));

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("You activate the recoiler."));
            Assert.That(QueueHitCount("ActivateRecoiler"), Is.Zero);
        });
    }

    [Test]
    public void AttemptTeleport_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched()
    {
        const string popup = "{{Y|recoiler}} is still starting up.";
        const string queued = "You activate the recoiler.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ITeleporterTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyITeleporterProducer
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(popup),
                }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(popup));
                    Assert.That(TotalPopupHitCount(), Is.Zero);
                });
            });

        WithPatchedOwnerAndQueue(() =>
        {
            new DummyITeleporterProducer
            {
                QueuedMessageToSend = MessageFrameTranslator.MarkDirectTranslation(queued),
            }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(queued));
                Assert.That(QueueHitCount("ActivateRecoiler"), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You have no bodily tether to recoil.")]
    [TestCase("You are stuck in a remote pocket dimension and cannot recoil out.")]
    [TestCase("You cannot do that here.")]
    [TestCase("Nothing happens.")]
    [TestCase("custom teleport failure")]
    public void AttemptTeleport_DoesNotClaimFixedRuntimeOrEmptyPopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ITeleporterTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyITeleporterProducer
                {
                    PopupMessageToShow = source,
                }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

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
                typeof(ITeleporterTranslationPatch),
                nameof(ITeleporterTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                typeof(ITeleporterTranslationPatch),
                nameof(ITeleporterTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyITeleporterProducer),
            nameof(DummyITeleporterProducer.AttemptTeleport),
            typeof(DummyGameObject),
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

    private static int PopupHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ITeleporterTranslationPatch), detail);
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            nameof(ITeleporterTranslationPatch) + "." + detail);
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.iteleporter-l2." + Guid.NewGuid().ToString("N");
    }

    private static string RepositoryMessageFramePath()
    {
        return Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json");
    }

    private static string RepositoryDictionaryDirectory()
    {
        return Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries");
    }

    private sealed class DummyITeleporterProducer
    {
        public string? PopupMessageToShow { get; set; }

        public string? QueuedMessageToSend { get; set; }

        public void AttemptTeleport(DummyGameObject actor, DummyEvent? fromEvent = null)
        {
            _ = actor;
            _ = fromEvent;

            if (PopupMessageToShow is not null)
            {
                DummyPopupShow.ShowFail(PopupMessageToShow);
            }

            if (QueuedMessageToSend is not null)
            {
                DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, "white", Capitalize: false);
            }
        }
    }

    private sealed class DummyEvent
    {
    }
}
