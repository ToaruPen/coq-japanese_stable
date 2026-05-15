using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AnimateObjectTranslationPatchTests
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
        "The {{Y|nano-neuro animator}} is unresponsive.",
        "{{Y|nano-neuro animator}}は反応しない。",
        "AnimateObjectUnresponsive")]
    [TestCase(
        "You imbue the {{Y|chair}} with life.",
        "{{Y|chair}}に生命を吹き込んだ。",
        "AnimateObjectImbueLife")]
    public void Patch_TranslatesAnimateObjectPopup_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(source, expected, detail, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You imbue the {{Y|chair}} with life.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("AnimateObjectImbueLife"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "The nano-neuro animator is unresponsive.";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "AnimateObjectUnresponsive",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("There's nothing viable to animate here.")]
    [TestCase("You can't animate an object that already has a brain.")]
    public void Patch_DoesNotClaimFixedOrEmptyPopup_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, "AnimateObjectUnresponsive", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, string detail, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(AnimateObjectTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyAnimateObjectProducer
                {
                    PopupMessageToShow = source,
                }.HandleEvent(new DummyInventoryActionEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyAnimateObjectProducer),
                   nameof(DummyAnimateObjectProducer.HandleEvent),
                   [typeof(DummyInventoryActionEvent)])
               ?? throw new MissingMethodException(
                   typeof(DummyAnimateObjectProducer).FullName,
                   nameof(DummyAnimateObjectProducer.HandleEvent));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(AnimateObjectTranslationPatch), detail);
    }

    private sealed class DummyAnimateObjectProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HandleEvent(DummyInventoryActionEvent e)
        {
            _ = e;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }

    private sealed class DummyInventoryActionEvent
    {
    }
}
