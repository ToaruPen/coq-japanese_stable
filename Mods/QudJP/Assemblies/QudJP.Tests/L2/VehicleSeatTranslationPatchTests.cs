using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VehicleSeatTranslationPatchTests
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
        "Accessing the pilot console requires the permanent insertion of {{Y|a cybernetic credit wedge}}.\n\nAre you sure you want to proceed?",
        "操縦コンソールへアクセスするには{{Y|a cybernetic credit wedge}}を恒久的に挿入する必要がある。\n\n続行しますか？",
        "VehicleSeatPilotConsoleConfirmation")]
    [TestCase(
        "Accessing the pilot console requires the permanent insertion of a cybernetic credit wedge.",
        "操縦コンソールへアクセスするにはa cybernetic credit wedgeを恒久的に挿入する必要がある。",
        "VehicleSeatPilotConsoleRequirement")]
    public void Patch_TranslatesPilotConsoleRequirementPopup_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(source, expected, detail, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source =
            "Accessing the pilot console requires the permanent insertion of a cybernetic credit wedge.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("VehicleSeatPilotConsoleRequirement"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source =
            "Accessing the pilot console requires the permanent insertion of a cybernetic credit wedge.";

        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "VehicleSeatPilotConsoleRequirement",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("Access diodes flash in the affirmative.")]
    public void Patch_DoesNotClaimFixedOrEmptyPopup_WhenOwnerPatched(string source)
    {
        AssertOwnerPopup(source, source, "VehicleSeatPilotConsoleRequirement", expectedHits: 0);
    }

    private static void AssertOwnerPopup(string source, string expected, string detail, int expectedHits)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(VehicleSeatTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummyVehicleSeatProducer
                {
                    PopupMessageToShow = source,
                }.AttemptPilot(new DummyGameObject());

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
                   typeof(DummyVehicleSeatProducer),
                   nameof(DummyVehicleSeatProducer.AttemptPilot),
                   [typeof(DummyGameObject)])
               ?? throw new MissingMethodException(
                   typeof(DummyVehicleSeatProducer).FullName,
                   nameof(DummyVehicleSeatProducer.AttemptPilot));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(VehicleSeatTranslationPatch), detail);
    }

    private sealed class DummyVehicleSeatProducer
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AttemptPilot(DummyGameObject obj)
        {
            _ = obj;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }
}
