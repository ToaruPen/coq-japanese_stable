using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TeleporterPairTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You must wait 1 turn before using this again.",
        "これを再び使うには1ターン待たなければならない。")]
    [TestCase(
        "You must wait 3 turns before using these again.",
        "これらを再び使うには3ターン待たなければならない。")]
    public void AttemptTeleport_TranslatesCooldownPopup_WhenOwnerPatched(string source, string expected)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(TeleporterPairTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyTeleporterPairProducer
                {
                    PopupMessageToShow = source,
                }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(RouteHitCount("TeleporterPairCooldown"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void AttemptTeleport_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You must wait 3 turns before using this again.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount("TeleporterPairCooldown"), Is.Zero);
        });
    }

    [Test]
    public void AttemptTeleport_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You must wait 3 turns before using this again.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(TeleporterPairTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyTeleporterPairProducer
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("TeleporterPairCooldown"), Is.Zero);
                });
            });
    }

    [TestCase("")]
    [TestCase("Nothing happens.")]
    [TestCase("You can't teleport with hostiles nearby!")]
    public void AttemptTeleport_DoesNotClaimFixedOrEmptyPopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(TeleporterPairTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyTeleporterPairProducer
                {
                    PopupMessageToShow = source,
                }.AttemptTeleport(new DummyGameObject(), new DummyEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("TeleporterPairCooldown"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyTeleporterPairProducer),
            nameof(DummyTeleporterPairProducer.AttemptTeleport),
            typeof(DummyGameObject),
            typeof(DummyEvent));
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(TeleporterPairTranslationPatch), detail);
    }

    private sealed class DummyTeleporterPairProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public void AttemptTeleport(DummyGameObject who, DummyEvent? fromEvent = null)
        {
            _ = who;
            _ = fromEvent;
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }

    private sealed class DummyEvent
    {
    }
}
