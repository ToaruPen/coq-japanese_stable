using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SurvivalCampAttemptCampPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "There is already a {{Y|campfire}} to the north. Do you want to go to it?",
        "北側にすでに{{Y|campfire}}がある。そこへ向かう？")]
    [TestCase(
        "There are already some {{Y|campfire remains}} to the southwest. Do you want to go to them?",
        "南西側にすでに{{Y|campfire remains}}がある。そこへ向かう？")]
    public void AttemptCamp_TranslatesExistingCampfireNavigationPrompt_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerPopup(source, expected, "ExistingCampfireNavigation");
    }

    [TestCase(
        "You can't cook with hostiles nearby.",
        "敵対者が近くにいると料理できない。",
        "HostilesNearby")]
    [TestCase(
        "You can't cook on the world map.",
        "ワールドマップ上では料理できない。",
        "WorldMap")]
    [TestCase(
        "You can only build a campfire in the same zone you are in.",
        "キャンプファイアは現在いるゾーンにしか作れない。",
        "SameZone")]
    [TestCase(
        "There is nothing there you can build a campfire on.",
        "そこにはキャンプファイアを作れるものがない。",
        "NoBuildSurface")]
    [TestCase(
        "Something is in the way!",
        "何かが邪魔をしている！",
        "Blocked")]
    public void AttemptCamp_TranslatesExactFailurePopup_WhenOwnerPatched(
        string source,
        string expected,
        string routeDetail)
    {
        AssertOwnerPopup(source, expected, routeDetail);
    }

    [Test]
    public void AttemptCamp_TranslatesExistingCampfireHerePrompt_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            "There is already a {{Y|campfire}} here.",
            "ここにはすでに{{Y|campfire}}がある。",
            "ExistingCampfireHere");
    }

    [Test]
    public void AttemptCamp_TranslatesCampfireInPoolPrompt_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            "You cannot start a campfire in {{B|deep pools of water}}.",
            "{{B|deep pools of water}}の中ではキャンプファイアを起こせない。",
            "ExtinguishingPool");
    }

    [Test]
    public void AttemptCamp_DoesNotClaimExistingCampfireNavigationPrompt_WhenOwnerAbsent()
    {
        const string source = "There is already a {{Y|campfire}} to the north. Do you want to go to it?";

        var claimed = SurvivalCampAttemptCampPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            nameof(PopupShowTranslationPatch),
            nameof(SurvivalCampAttemptCampPopupTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(claimed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("ExistingCampfireNavigation"), Is.Zero);
        });
    }

    [Test]
    public void AttemptCamp_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "There is already a campfire to the north. Do you want to go to it?";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "ExistingCampfireNavigation",
            expectedHits: 0);
    }

    [Test]
    public void AttemptCamp_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty, "ExistingCampfireNavigation", expectedHits: 0);
    }

    [Test]
    public void AttemptCamp_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "The camp refuses to become a tiny moon.";

        AssertOwnerPopup(source, source, "ExistingCampfireNavigation", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, string routeDetail, int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SurvivalCampAttemptCampPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                var target = new DummySurvivalCampTarget { PopupMessageToShow = source };

                target.AttemptCamp(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(routeDetail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummySurvivalCampTarget),
            nameof(DummySurvivalCampTarget.AttemptCamp),
            typeof(DummyGameObject));
    }

    private static int HitCount(string routeDetail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(SurvivalCampAttemptCampPopupTranslationPatch),
            routeDetail);
    }
}
