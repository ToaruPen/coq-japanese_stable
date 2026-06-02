using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CyberneticsOnboardRecoilerPopupTranslationPatchTests
{
    private const string ExpectedPrompt = "まだリコイルできない。";

    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [Test]
    public void Patch_TranslatesCooldownPrompt_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsOnboardRecoilerPopupTranslationPatch),
            RequireMethod(nameof(DummyCyberneticsOnboardRecoilerTarget.ActuateTeleport)),
            () =>
            {
                var target = new DummyCyberneticsOnboardRecoilerTarget
                {
                    PopupMessageToShow = CyberneticsOnboardRecoilerPopupTranslationPatch.SourcePrompt,
                };

                target.ActuateTeleport();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowMessage,
                        Is.EqualTo(ExpectedPrompt));
                    Assert.That(GetHitCount(), Is.EqualTo(1));
                });
            });
    }

    [TestCase("", "", 0)]
    [TestCase("{{W|You can't recoil yet.}}", "{{W|まだリコイルできない。}}", 1)]
    public void Patch_PassesThroughEmptyAndTranslatesColorTaggedPrompt_WhenOwnerPatched(
        string source,
        string expected,
        int expectedHitCount)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsOnboardRecoilerPopupTranslationPatch),
            RequireMethod(nameof(DummyCyberneticsOnboardRecoilerTarget.ActuateTeleport)),
            () =>
            {
                var target = new DummyCyberneticsOnboardRecoilerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ActuateTeleport();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(GetHitCount(), Is.EqualTo(expectedHitCount));
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowFail(CyberneticsOnboardRecoilerPopupTranslationPatch.SourcePrompt));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(CyberneticsOnboardRecoilerPopupTranslationPatch.SourcePrompt));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPrompt_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsOnboardRecoilerPopupTranslationPatch),
            RequireMethod(nameof(DummyCyberneticsOnboardRecoilerTarget.ActuateTeleport)),
            () =>
            {
                var target = new DummyCyberneticsOnboardRecoilerTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(
                        CyberneticsOnboardRecoilerPopupTranslationPatch.SourcePrompt),
                };

                target.ActuateTeleport();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(CyberneticsOnboardRecoilerPopupTranslationPatch.SourcePrompt));
                    Assert.That(GetHitCount(), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyCyberneticsOnboardRecoilerTarget), methodName);
    }

    private static int GetHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + CyberneticsOnboardRecoilerPopupTranslationPatch.Family);
    }

    private sealed class DummyCyberneticsOnboardRecoilerTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ActuateTeleport()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
    }
}
