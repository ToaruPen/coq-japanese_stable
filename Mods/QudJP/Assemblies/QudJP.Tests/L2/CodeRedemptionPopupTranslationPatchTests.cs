using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CodeRedemptionPopupTranslationPatchTests
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

    [TestCase(nameof(DummyCodeRedemptionManagerTarget.redeemNoProgress))]
    [TestCase(nameof(DummyCodeRedemptionManagerTarget.redeemProgressDelegate))]
    public void Patch_TranslatesPetDownloadErrorPopup_WhenOwnerPatched(string ownerMethodName)
    {
        const string source = "Error downloading pet: System.Exception: boom";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CodeRedemptionPopupTranslationPatch),
            RequireOwnerMethod(ownerMethodName),
            () =>
            {
                var target = new DummyCodeRedemptionManagerTarget
                {
                    PopupMessageToShow = source,
                };
                CallOwner(target, ownerMethodName);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowAsyncMessage,
                        Is.EqualTo("ペットのダウンロード中にエラーが発生した: System.Exception: boom"));
                    Assert.That(RouteHitCount(), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_PreservesColorMarkupInPetDownloadError_WhenOwnerPatched()
    {
        const string source = "{{R|Error downloading pet: System.Exception: boom}}";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CodeRedemptionPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyCodeRedemptionManagerTarget.redeemNoProgress)),
            () =>
            {
                new DummyCodeRedemptionManagerTarget
                {
                    PopupMessageToShow = source,
                }.redeemNoProgress();

                Assert.That(
                    DummyPopupShow.LastShowAsyncMessage,
                    Is.EqualTo("{{R|ペットのダウンロード中にエラーが発生した: System.Exception: boom}}"));
            });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Error downloading pet: System.Exception: boom";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowAsync(source).GetAwaiter().GetResult());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Error downloading pet: System.Exception: boom";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CodeRedemptionPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyCodeRedemptionManagerTarget.redeemNoProgress)),
            () =>
            {
                new DummyCodeRedemptionManagerTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.redeemNoProgress();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount(), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CodeRedemptionPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyCodeRedemptionManagerTarget.redeemNoProgress)),
            () =>
            {
                new DummyCodeRedemptionManagerTarget
                {
                    PopupMessageToShow = string.Empty,
                }.redeemNoProgress();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.Empty);
                    Assert.That(RouteHitCount(), Is.Zero);
                });
            });
    }

    [TestCase("That code is invalid.")]
    [TestCase("Your new pet is ready to love.")]
    public void Patch_DoesNotClaimFixedCodeRedemptionPopups_WhenOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CodeRedemptionPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyCodeRedemptionManagerTarget.redeemNoProgress)),
            () =>
            {
                new DummyCodeRedemptionManagerTarget
                {
                    PopupMessageToShow = source,
                }.redeemNoProgress();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount(), Is.Zero);
                });
            });
    }

    private static void CallOwner(DummyCodeRedemptionManagerTarget target, string ownerMethodName)
    {
        if (string.Equals(ownerMethodName, nameof(DummyCodeRedemptionManagerTarget.redeemNoProgress), StringComparison.Ordinal))
        {
            target.redeemNoProgress();
            return;
        }

        if (string.Equals(ownerMethodName, nameof(DummyCodeRedemptionManagerTarget.redeemProgressDelegate), StringComparison.Ordinal))
        {
            target.redeemProgressDelegate();
            return;
        }

        throw new ArgumentException("Unknown code redemption owner method.", nameof(ownerMethodName));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyCodeRedemptionManagerTarget), methodName);
    }

    private static int RouteHitCount()
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(CodeRedemptionPopupTranslationPatch),
            "CodeRedemptionPetDownloadError");
    }
}
