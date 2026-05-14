using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PointOfInterestNavigationPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyPointOfInterestTarget.PopupMessageToShow = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        DummyPointOfInterestTarget.PopupMessageToShow = string.Empty;
        DummyPopupShow.Reset();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You are already at {{Y|rust well}}.",
        "{{Y|rust well}}にすでにいる。",
        "AlreadyAtPointOfInterest")]
    [TestCase(
        "You are already near {{Y|campfire}}.",
        "{{Y|campfire}}の近くにすでにいる。",
        "AlreadyAtPointOfInterest")]
    [TestCase(
        "Somehow there seems to be no location for {{Y|forgotten ruins}}.",
        "どういうわけか{{Y|forgotten ruins}}の場所が見つからない。",
        "NoPointOfInterestLocation")]
    public void NavigateTo_TranslatesNavigationFailurePopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(source, expected, detail);
    }

    [TestCase(
        "You are already at {{Y|rust well}}.",
        "AlreadyAtPointOfInterest")]
    [TestCase(
        "Somehow there seems to be no location for {{Y|forgotten ruins}}.",
        "NoPointOfInterestLocation")]
    public void NavigateTo_DoesNotTranslateNavigationFailurePopup_WhenOwnerAbsent(string source, string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowFail(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    [Test]
    public void NavigateTo_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You are already at {{Y|rust well}}.";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "AlreadyAtPointOfInterest",
            expectedHits: 0);
    }

    [Test]
    public void NavigateTo_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty, "AlreadyAtPointOfInterest", expectedHits: 0);
    }

    [Test]
    public void NavigateTo_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup("You cannot navigate right now.", "You cannot navigate right now.", "AlreadyAtPointOfInterest", expectedHits: 0);
    }

    private static void AssertOwnerPopup(
        string source,
        string expected,
        string detail,
        int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(PointOfInterestNavigationPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyPointOfInterestTarget.PopupMessageToShow = source;

                _ = DummyPointOfInterestTarget.NavigateTo(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyPointOfInterestTarget),
            nameof(DummyPointOfInterestTarget.NavigateTo),
            typeof(DummyGameObject));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(PointOfInterestNavigationPopupTranslationPatch), detail);
    }
}
