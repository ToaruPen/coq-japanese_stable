using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VehicleFollowerPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
        Translator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
        Translator.ResetForTests();
    }

    [Test]
    public void Patch_TranslatesNoFollowersFailure_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(VehicleFollowerPopupTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyVehicleFollowerTarget), nameof(DummyVehicleFollowerTarget.HandleEvent)),
            () =>
            {
                new DummyVehicleFollowerTarget
                {
                    PopupMessageToShow = "You have no followers that can enter {{Y|the chrome steed}}.",
                }.HandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|the chrome steed}}に入れる仲間はいない。"));
                    Assert.That(RouteHitCount("NoFollowers"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_TranslatesFollowerPickerTitle_WhenOwnerActive()
    {
        VehicleFollowerPopupTranslationPatch.Prefix();
        try
        {
            var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
                "Choose a follower",
                nameof(VehicleFollowerPopupTranslationPatch));

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo("仲間を選ぶ。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(VehicleFollowerPopupTranslationPatch),
                        "Popup.ProducerText.VehicleFollowerPopupTranslationPatch.PickGameObjectTitle"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            VehicleFollowerPopupTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void Patch_DoesNotTranslateVehicleFollowerPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail("You have no followers that can enter {{Y|the chrome steed}}.");

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("You have no followers that can enter {{Y|the chrome steed}}."));
                Assert.That(RouteHitCount("NoFollowers"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyVehicleFollowerPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(VehicleFollowerPopupTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyVehicleFollowerTarget), nameof(DummyVehicleFollowerTarget.HandleEvent)),
            () =>
            {
                new DummyVehicleFollowerTarget
                {
                    PopupMessageToShow = string.Empty,
                }.HandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
                    Assert.That(RouteHitCount("NoFollowers"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_StripsDirectMarkerVehicleFollowerPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(VehicleFollowerPopupTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyVehicleFollowerTarget), nameof(DummyVehicleFollowerTarget.HandleEvent)),
            () =>
            {
                new DummyVehicleFollowerTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("翻訳済み"),
                }.HandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("翻訳済み"));
                    Assert.That(RouteHitCount("NoFollowers"), Is.Zero);
                });
            });
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(VehicleFollowerPopupTranslationPatch), detail);
    }

    private sealed class DummyVehicleFollowerTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;

        public void HandleEvent()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
    }
}
