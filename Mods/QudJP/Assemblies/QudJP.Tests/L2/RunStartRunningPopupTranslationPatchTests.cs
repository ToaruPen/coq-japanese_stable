using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RunStartRunningPopupTranslationPatchTests
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
        "You cannot run on the world map.",
        "ワールドマップでは走ることはできない。")]
    [TestCase(
        "You cannot sprint on the world map.",
        "ワールドマップでは全力疾走することはできない。")]
    [TestCase(
        "You cannot power skate on the world map.",
        "ワールドマップではパワースケートすることはできない。")]
    public void StartRunning_TranslatesWorldMapMovementModePopup_WhenOwnerPatched(string source, string expected)
    {
        AssertOwnerPopup(source, expected);
    }

    [Test]
    public void StartRunning_DoesNotTranslateWorldMapMovementModePopup_WhenOwnerAbsent()
    {
        const string source = "You cannot sprint on the world map.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowFail(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void StartRunning_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You cannot sprint on the world map.";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source, expectedHits: 0);
    }

    [Test]
    public void StartRunning_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty, expectedHits: 0);
    }

    [Test]
    public void StartRunning_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup("You cannot do that on the world map.", "You cannot do that on the world map.", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(RunStartRunningPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                var target = new DummyRunTarget { PopupMessageToShow = source };

                _ = target.StartRunning();

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
            typeof(DummyRunTarget),
            nameof(DummyRunTarget.StartRunning));
    }

    private static int HitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(RunStartRunningPopupTranslationPatch),
            "WorldMapMovementMode");
    }
}
