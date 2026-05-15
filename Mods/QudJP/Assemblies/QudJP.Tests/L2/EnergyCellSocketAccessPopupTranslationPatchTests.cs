using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class EnergyCellSocketAccessPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "This is not owned by you. Are you sure you want to access its energy cell?",
        "これはあなたの所有物ではない。本当にそのエネルギーセルにアクセスしますか？")]
    [TestCase(
        "These are not owned by you. Are you sure you want to access their energy cell?",
        "これらはあなたの所有物ではない。本当にそのエネルギーセルにアクセスしますか？")]
    [TestCase(
        "{{Y|the phase cannon}} is not owned by you. Are you sure you want to access its energy cell?",
        "{{Y|the phase cannon}}はあなたの所有物ではない。本当にそのエネルギーセルにアクセスしますか？")]
    public void AttemptReplaceCell_TranslatesAccessWarning_WhenOwnerPatched(string source, string expected)
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            DummyEnergyCellSocketTarget.PopupMessageToShow = source;
            _ = DummyEnergyCellSocketTarget.AttemptReplaceCell(new object());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void AttemptReplaceCell_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "This is not owned by you. Are you sure you want to access its energy cell?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowYesNoCancel(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount(), Is.Zero);
        });
    }

    [Test]
    public void AttemptReplaceCell_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "This is not owned by you. Are you sure you want to access its energy cell?";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        RunWithOwnerAndPopupPatches(() =>
        {
            DummyEnergyCellSocketTarget.PopupMessageToShow = marked;
            _ = DummyEnergyCellSocketTarget.AttemptReplaceCell(new object());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("This is not owned by you. Are you sure you want to access its magazine?")]
    [TestCase("This is owned by you. Are you sure you want to access its energy cell?")]
    public void AttemptReplaceCell_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            DummyEnergyCellSocketTarget.PopupMessageToShow = source;
            _ = DummyEnergyCellSocketTarget.AttemptReplaceCell(new object());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        });
    }

    private static void RunWithOwnerAndPopupPatches(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(EnergyCellSocketAccessPopupTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(
                typeof(DummyEnergyCellSocketTarget),
                nameof(DummyEnergyCellSocketTarget.AttemptReplaceCell),
                typeof(object)),
            action);
    }

    private static int RouteHitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(EnergyCellSocketAccessPopupTranslationPatch),
            "AccessEnergyCellOwnershipWarning");
    }

    private static class DummyEnergyCellSocketTarget
    {
        public static string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool AttemptReplaceCell(object e)
        {
            _ = e;
            _ = DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
            return true;
        }
    }
}
