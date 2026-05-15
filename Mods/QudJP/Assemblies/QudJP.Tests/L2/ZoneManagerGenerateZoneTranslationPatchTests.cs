using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ZoneManagerGenerateZoneTranslationPatchTests
{
    private const string ReportIssueSource =
        "There was an issue building this zone. Automatically report it to us? System.InvalidOperationException: boom";
    private const string ReportIssueExpected =
        "このゾーンの構築中に問題が発生した。自動的に報告しますか？ System.InvalidOperationException: boom";

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

    [Test]
    public void Patch_TranslatesReportIssueConfirmation_WhenOwnerPatched()
    {
        AssertOwnerPopup(ReportIssueSource, ReportIssueExpected, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslateReportIssueConfirmation_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowYesNo(ReportIssueSource));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(ReportIssueSource));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(ReportIssueSource),
            ReportIssueSource,
            expectedHits: 0);
    }

    [TestCase("This zone isn't building properly. Do you want to force it to stop and build immediately?")]
    [TestCase("Zone build failure:<none>")]
    public void Patch_DoesNotClaimOtherGenerateZoneShapes_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ZoneManagerGenerateZoneTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyZoneManagerGenerateZoneTarget
                {
                    PopupMessageToShow = source,
                }.GenerateZone("JoppaWorld.1.1.1.1.10");

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyZoneManagerGenerateZoneTarget),
                   nameof(DummyZoneManagerGenerateZoneTarget.GenerateZone),
                   [typeof(string)])
               ?? throw new MissingMethodException(
                   typeof(DummyZoneManagerGenerateZoneTarget).FullName,
                   nameof(DummyZoneManagerGenerateZoneTarget.GenerateZone));
    }

    private static int HitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(ZoneManagerGenerateZoneTranslationPatch),
            "GenerateZoneReportIssuePopup");
    }
}
