using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MapRevealPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        PopupSurface.YesNoCancel,
        "{{Y|ancient map}} is not owned by you, and using {{Y|it}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{Y|ancient map}}はあなたのものではなく、{{Y|it}}を使うと{{Y|it}}は消費される。本当に行うか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        PopupSurface.YesNoCancel,
        "{{C|satchel}} are not owned by you, and using {{Y|the map}} will consume {{Y|it}}. Are you sure you want to do so?",
        "{{C|satchel}}はあなたのものではなく、{{Y|the map}}を使うと{{Y|it}}は消費される。本当に行うか？",
        "OwnerConsumptionWarning")]
    [TestCase(
        PopupSurface.Show,
        "{{Y|ancient map}} seems to be behaving as nothing more than an ordinary piece of paper.",
        "{{Y|ancient map}}は普通の紙切れとしてしか振る舞っていないようだ。",
        "OrdinaryPaper")]
    [TestCase(
        PopupSurface.Show,
        "It's a map of your surroundings!",
        "周囲の地図だ！",
        "MapOfSurroundings")]
    [TestCase(
        PopupSurface.Show,
        "They're a map of your surroundings!",
        "周囲の地図だ！",
        "MapOfSurroundings")]
    public void HandleEvent_TranslatesInventoriedPopupMessages_WhenOwnerPatched(
        PopupSurface surface,
        string source,
        string expected,
        string expectedDetail)
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMapRevealProducer
            {
                PopupMessageToShow = source,
                PopupSurface = surface,
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(surface), Is.EqualTo(expected));
                Assert.That(RouteHitCount(expectedDetail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "{{Y|ancient map}} is not owned by you, and using {{Y|it}} will consume {{Y|it}}. Are you sure you want to do so?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowYesNoCancel(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
        });
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "{{Y|ancient map}} seems to be behaving as nothing more than an ordinary piece of paper.";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMapRevealProducer
            {
                PopupMessageToShow = marked,
                PopupSurface = PopupSurface.Show,
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
            });
        });
    }

    [Test]
    public void HandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMapRevealProducer
            {
                PopupMessageToShow = string.Empty,
                PopupSurface = PopupSurface.Show,
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void HandleEvent_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "The map remains inscrutable.";

        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyMapRevealProducer
            {
                PopupMessageToShow = source,
                PopupSurface = PopupSurface.Show,
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(RouteHitCount("OwnerConsumptionWarning"), Is.Zero);
                Assert.That(RouteHitCount("OrdinaryPaper"), Is.Zero);
                Assert.That(RouteHitCount("MapOfSurroundings"), Is.Zero);
            });
        });
    }

    private static void RunWithOwnerAndPopupPatches(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(MapRevealPopupTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(
                typeof(DummyMapRevealProducer),
                nameof(DummyMapRevealProducer.HandleEvent),
                typeof(DummyInventoryActionEvent)),
            action);
    }

    private static string? LastPopupMessage(PopupSurface surface)
    {
        return surface switch
        {
            PopupSurface.Show => DummyPopupShow.LastShowMessage,
            PopupSurface.YesNoCancel => DummyPopupShow.LastShowYesNoCancelMessage,
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
        };
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(MapRevealPopupTranslationPatch), detail);
    }

    public enum PopupSurface
    {
        Show,
        YesNoCancel,
    }

    private sealed class DummyInventoryActionEvent;

    private sealed class DummyMapRevealProducer
    {
        public string PopupMessageToShow = string.Empty;
        public PopupSurface PopupSurface;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HandleEvent(DummyInventoryActionEvent e)
        {
            _ = e;

            if (PopupSurface == PopupSurface.YesNoCancel)
            {
                DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
            }
            else
            {
                DummyPopupShow.Show(PopupMessageToShow);
            }

            return true;
        }
    }
}
