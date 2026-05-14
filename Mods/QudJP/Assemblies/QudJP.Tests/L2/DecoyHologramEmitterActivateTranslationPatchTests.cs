using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DecoyHologramEmitterActivateTranslationPatchTests
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
        "The {{Y|hologram bracelet}} is still starting up.",
        "{{Y|hologram bracelet}}はまだ起動中だ。",
        "DecoyHologramStillStarting")]
    [TestCase(
        "{{Y|tri-hologram bracelet}} does not have enough charge to sustain the hologram.",
        "{{Y|tri-hologram bracelet}}にはホログラムを維持するのに十分な充電がない。",
        "DecoyHologramInsufficientCharge")]
    [TestCase(
        "The {{Y|hologram bracelet}} is unresponsive.",
        "{{Y|hologram bracelet}}は反応しない。",
        "DecoyHologramUnresponsive")]
    public void Patch_TranslatesActivationPopup_WhenOwnerPatched(string source, string expected, string detail)
    {
        AssertOwnerPopup(source, expected, detail, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|hologram bracelet}} is still starting up.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("DecoyHologramStillStarting"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "The hologram bracelet is still starting up.";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "DecoyHologramStillStarting",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You cannot do that on the world map.")]
    public void Patch_DoesNotClaimFixedOrEmptyPopup_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, "DecoyHologramStillStarting", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, string detail, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(DecoyHologramEmitterActivateTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyDecoyHologramEmitterActivateProducer
                {
                    PopupMessageToShow = source,
                }.ActivateHologramBracelet(new DummyGameObject(), new DummyEvent());

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
                   typeof(DummyDecoyHologramEmitterActivateProducer),
                   nameof(DummyDecoyHologramEmitterActivateProducer.ActivateHologramBracelet),
                   [typeof(DummyGameObject), typeof(DummyEvent)])
               ?? throw new MissingMethodException(
                   typeof(DummyDecoyHologramEmitterActivateProducer).FullName,
                   nameof(DummyDecoyHologramEmitterActivateProducer.ActivateHologramBracelet));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(DecoyHologramEmitterActivateTranslationPatch),
            detail);
    }

    private sealed class DummyDecoyHologramEmitterActivateProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ActivateHologramBracelet(DummyGameObject who, DummyEvent? e = null)
        {
            _ = who;
            _ = e;
            DummyPopupShow.Show(PopupMessageToShow);
            return false;
        }
    }

    private sealed class DummyEvent
    {
    }
}
