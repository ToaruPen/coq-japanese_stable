using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

#pragma warning disable S4144 // NUnit setup/teardown intentionally reset the same static fixtures.

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CyberneticsLowLevelHackPopupTranslationPatchTests
{
    private const string ExpectedPrompt =
        "低レベルハックを使用しますか？低レベルハックを使用すると端末出力の解読が難しくなるが、セキュリティ警報を作動させる可能性が下がる。";

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
    public void Patch_TranslatesLowLevelHackPrompt_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsLowLevelHackPopupTranslationPatch),
            RequireMethod(nameof(DummyCyberneticsLowLevelHackTarget.AskLowLevelHack)),
            () =>
            {
                var target = new DummyCyberneticsLowLevelHackTarget
                {
                    PopupMessageToShow = CyberneticsLowLevelHackPopupTranslationPatch.SourcePrompt,
                };

                target.AskLowLevelHack();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowYesNoMessage,
                        Is.EqualTo(ExpectedPrompt));
                    Assert.That(GetHitCount(), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_TranslatesLowLevelHackPrompt_PreservesColorTags_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsLowLevelHackPopupTranslationPatch),
            RequireMethod(nameof(DummyCyberneticsLowLevelHackTarget.AskLowLevelHack)),
            () =>
            {
                var target = new DummyCyberneticsLowLevelHackTarget
                {
                    PopupMessageToShow = "{{W|" + CyberneticsLowLevelHackPopupTranslationPatch.SourcePrompt + "}}",
                };

                target.AskLowLevelHack();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowYesNoMessage,
                        Is.EqualTo("{{W|" + ExpectedPrompt + "}}"));
                    Assert.That(GetHitCount(), Is.EqualTo(1));
                });
            });
    }

    [TestCase("", "", 0)]
    [TestCase("{{W|Unknown low-level hack prompt.}}", "{{W|Unknown low-level hack prompt.}}", 0)]
    public void Patch_LeavesEmptyAndColorTaggedFallbackPromptsUnchanged_WhenOwnerPatched(
        string source,
        string expected,
        int expectedHitCount)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsLowLevelHackPopupTranslationPatch),
            RequireMethod(nameof(DummyCyberneticsLowLevelHackTarget.AskLowLevelHack)),
            () =>
            {
                var target = new DummyCyberneticsLowLevelHackTarget
                {
                    PopupMessageToShow = source,
                };

                target.AskLowLevelHack();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                    Assert.That(GetHitCount(), Is.EqualTo(expectedHitCount));
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => _ = DummyPopupShow.ShowYesNo(CyberneticsLowLevelHackPopupTranslationPatch.SourcePrompt));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(CyberneticsLowLevelHackPopupTranslationPatch.SourcePrompt));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_StripsDirectMarkedPopupOnlyTraffic_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => _ = DummyPopupShow.ShowYesNo(MessageFrameTranslator.MarkDirectTranslation("既に翻訳済みプロンプト")));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("既に翻訳済みプロンプト"));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPrompt_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsLowLevelHackPopupTranslationPatch),
            RequireMethod(nameof(DummyCyberneticsLowLevelHackTarget.AskLowLevelHack)),
            () =>
            {
                var target = new DummyCyberneticsLowLevelHackTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(
                        CyberneticsLowLevelHackPopupTranslationPatch.SourcePrompt),
                };

                target.AskLowLevelHack();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(CyberneticsLowLevelHackPopupTranslationPatch.SourcePrompt));
                    Assert.That(GetHitCount(), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyCyberneticsLowLevelHackTarget), methodName);
    }

    private static int GetHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + CyberneticsLowLevelHackPopupTranslationPatch.Family);
    }

    private sealed class DummyCyberneticsLowLevelHackTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AskLowLevelHack()
        {
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
    }
}
