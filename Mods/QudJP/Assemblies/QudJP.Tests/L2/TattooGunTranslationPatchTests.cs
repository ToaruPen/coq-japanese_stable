using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TattooGunTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You tattoo the mark of death on your right hand.",
        "あなたはあなたのright handに死の印を入れ墨した。")]
    [TestCase(
        "You tattoo {{W|a tiny spiral}} on {{Y|Issachari rifler}}'s left arm.",
        "あなたは{{Y|Issachari rifler}}'s left armに{{W|a tiny spiral}}を入れ墨した。")]
    public void TattooGunAttemptTattoo_TranslatesSuccessPopups_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerPopup(source, expected, expectedHits: 1);
    }

    [Test]
    public void TattooGunAttemptTattoo_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You tattoo the mark of death on your right hand.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void TattooGunAttemptTattoo_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You tattoo a tiny spiral on your right hand.";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You engrave the mark of death on your right hand.")]
    [TestCase("Choose a primary color.")]
    public void TattooGunAttemptTattoo_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(TattooGunTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyTattooGunProducer
                {
                    PopupMessageToShow = source,
                }.AttemptTattoo(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyTattooGunProducer),
            nameof(DummyTattooGunProducer.AttemptTattoo),
            typeof(DummyGameObject));
    }

    private static int HitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(TattooGunTranslationPatch), "SuccessPopup");
    }

    private sealed class DummyTattooGunProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AttemptTattoo(DummyGameObject actor)
        {
            _ = actor;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }
}
