using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SifrahTokenItemPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        Translator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenGiftCheckTokenUse),
        "You do not have any more {{Y|engraved daggers}}.",
        "{{Y|engraved daggers}}をもう持っていない。",
        "SocialSifrahTokenGiftAnyMore")]
    [TestCase(
        nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenGiftCheckTokenUse),
        "You do not have {{C|an engraved dagger}}.",
        "{{C|an engraved dagger}}を持っていない。",
        "SocialSifrahTokenGiftHaveNone")]
    [TestCase(
        nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenItemCheckTokenUse),
        "You do not have any more {{M|woven baskets}}.",
        "{{M|woven baskets}}をもう持っていない。",
        "SocialSifrahTokenItemAnyMore")]
    [TestCase(
        nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenItemCheckTokenUse),
        "You do not have {{B|a woven basket}}.",
        "{{B|a woven basket}}を持っていない。",
        "SocialSifrahTokenItemHaveNone")]
    public void Patch_TranslatesSifrahTokenItemPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahTokenItemPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummySifrahTokenItemPopupProducerTarget
                {
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslateSifrahTokenItemPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                const string source = "You do not have any more {{Y|engraved daggers}}.";
                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("SocialSifrahTokenGiftAnyMore"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahTokenItemPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenGiftCheckTokenUse)),
            () =>
            {
                const string unmarked = "You do not have {{C|an engraved dagger}}.";
                var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);
                var target = new DummySifrahTokenItemPopupProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.SocialSifrahTokenGiftCheckTokenUse(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                    Assert.That(HitCount("SocialSifrahTokenGiftHaveNone"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahTokenItemPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenGiftCheckTokenUse)),
            () =>
            {
                var target = new DummySifrahTokenItemPopupProducerTarget();

                target.SocialSifrahTokenGiftCheckTokenUse(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("SocialSifrahTokenGiftHaveNone"), Is.Zero);
                });
            });
    }

    [TestCase(
        nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenGiftCheckTokenUse),
        "SocialSifrahTokenGiftAnyMore")]
    [TestCase(
        nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenItemCheckTokenUse),
        "SocialSifrahTokenItemAnyMore")]
    public void Patch_DefersFixedKindOfItemMessage_WhenOwnerPatched(string methodName, string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SifrahTokenItemPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                const string source = "You do not have any more of that kind of item.";
                var target = new DummySifrahTokenItemPopupProducerTarget
                {
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount(detail), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerExecution()
    {
        const string outerMethodName = nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenGiftCheckTokenUse);
        const string innerMethodName = nameof(DummySifrahTokenItemPopupProducerTarget.SocialSifrahTokenItemCheckTokenUse);
        OwnerPopupRouteTestHarness.WithPatchedPopupOwners(
            typeof(SifrahTokenItemPopupTranslationPatch),
            [RequireOwnerMethod(outerMethodName), RequireOwnerMethod(innerMethodName)],
            () =>
            {
                var innerTarget = new DummySifrahTokenItemPopupProducerTarget
                {
                    PopupMessageToShow = "You do not have {{B|a woven basket}}.",
                };
                var outerTarget = new DummySifrahTokenItemPopupProducerTarget
                {
                    PopupMessageToShow = "You do not have any more {{Y|engraved daggers}}.",
                    BeforePopup = () =>
                    {
                        InvokeOwnerMethod(innerTarget, innerMethodName);
                        Assert.Multiple(() =>
                        {
                            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{B|a woven basket}}を持っていない。"));
                            Assert.That(HitCount("SocialSifrahTokenItemHaveNone"), Is.EqualTo(1));
                            Assert.That(HitCount("SocialSifrahTokenGiftAnyMore"), Is.Zero);
                        });
                    },
                };

                InvokeOwnerMethod(outerTarget, outerMethodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|engraved daggers}}をもう持っていない。"));
                    Assert.That(HitCount("SocialSifrahTokenItemHaveNone"), Is.EqualTo(1));
                    Assert.That(HitCount("SocialSifrahTokenGiftAnyMore"), Is.EqualTo(1));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummySifrahTokenItemPopupProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static void InvokeOwnerMethod(DummySifrahTokenItemPopupProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, [new DummyGameObject()]);
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(SifrahTokenItemPopupTranslationPatch), detail);
    }
}
