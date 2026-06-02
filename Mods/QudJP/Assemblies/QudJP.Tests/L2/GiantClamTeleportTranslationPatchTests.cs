using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GiantClamTeleportTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [Test]
    public void TeleportFromClamWorld_TranslatesHomeDimensionPopup_WhenOwnerPatched()
    {
        var target = new DummyGiantClamTeleportPopupTarget
        {
            PopupMessageToShow = "You find a passageway back to your home dimension.",
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(GiantClamTeleportTranslationPatch),
            RequireOwnerMethod(),
            () => target.TeleportFromClamWorld());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("元の次元へ戻る通路を見つけた。"));
            Assert.That(GetHitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void TeleportFromClamWorld_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You find a passageway back to your home dimension.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    [Test]
    public void TeleportFromClamWorld_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched()
    {
        const string translated = "元の次元へ戻る通路を見つけた。";
        var target = new DummyGiantClamTeleportPopupTarget
        {
            PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(translated),
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(GiantClamTeleportTranslationPatch),
            RequireOwnerMethod(),
            () => target.TeleportFromClamWorld());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(translated));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    [Test]
    public void TeleportFromClamWorld_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var target = new DummyGiantClamTeleportPopupTarget
        {
            PopupMessageToShow = string.Empty,
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(GiantClamTeleportTranslationPatch),
            RequireOwnerMethod(),
            () => target.TeleportFromClamWorld());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    [Test]
    public void TeleportFromClamWorld_PreservesColorTaggedFallback_WhenOwnerPatched()
    {
        const string source = "<color=red>You find a passageway back to your home dimension.</color>";
        var target = new DummyGiantClamTeleportPopupTarget
        {
            PopupMessageToShow = source,
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(GiantClamTeleportTranslationPatch),
            RequireOwnerMethod(),
            () => target.TeleportFromClamWorld());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyGiantClamTeleportPopupTarget),
            nameof(DummyGiantClamTeleportPopupTarget.TeleportFromClamWorld));
    }

    private static int GetHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(GiantClamTeleportTranslationPatch) + ".HomeDimensionPopup");
    }

    private sealed class DummyGiantClamTeleportPopupTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TeleportFromClamWorld()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
