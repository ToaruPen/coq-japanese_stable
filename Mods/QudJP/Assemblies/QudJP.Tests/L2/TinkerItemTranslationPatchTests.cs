using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TinkerItemTranslationPatchTests
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

    [Test]
    public void HandleEvent_TranslatesCannotAffectPopup_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowFail),
                MessageToShow = "You cannot seem to affect {{Y|the phase cannon}} in any way.",
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|phase cannon}}にはどうやっても影響を与えられそうにない。"));
                Assert.That(HitCount("CannotAffect"), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "The bronze dagger is not owned by you. Are you sure you want to disassemble it?",
        "bronze daggerはあなたのものではない。それを分解してよいか？")]
    [TestCase(
        "{{Y|the bronze daggers}}are not owned by you. Are you sure you want to disassemble them?",
        "{{Y|bronze daggers}}はあなたのものではない。それらを分解してよいか？")]
    public void HandleEvent_TranslatesOwnedItemDisassemblyPrompt_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                MessageToShow = source,
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
                Assert.That(HitCount("OwnedItemDisassembly"), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "Are you sure you want to disassemble the bronze dagger?",
        "bronze daggerを分解してよいか？")]
    [TestCase(
        "Are you sure you want to disassemble all the bronze daggers?",
        "bronze daggersをすべて分解してよいか？")]
    public void HandleEvent_TranslatesDisassemblyConfirmation_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                MessageToShow = source,
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
                Assert.That(HitCount("DisassemblyConfirmation"), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "{{C|the chest}} is not owned by you. Are you sure you want to disassemble a bronze dagger inside it?",
        "{{C|chest}}はあなたのものではない。その中のbronze daggerを分解してよいか？")]
    [TestCase(
        "{{C|the chests}} are not owned by you. Are you sure you want to disassemble items inside them?",
        "{{C|chests}}はあなたのものではない。その中のアイテムを分解してよいか？")]
    public void HandleEvent_TranslatesContainerOwnedDisassemblyPrompt_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                MessageToShow = source,
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(expected));
                Assert.That(HitCount("ContainerOwnedDisassembly"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void HandleEvent_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You cannot seem to affect the phase cannon in any way.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowFail(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("CannotAffect"), Is.Zero);
        });
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedShowFail_WhenOwnerPatched()
    {
        const string source = "You cannot seem to affect the phase cannon in any way.";

        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowFail),
                MessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("CannotAffect"), Is.Zero);
            });
        });
    }

    [Test]
    public void HandleEvent_DirectMarkerPassThroughDoesNotLeakToNextPopup_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowFail),
                MessageToShow = MessageFrameTranslator.MarkDirectTranslation(
                    "You cannot seem to affect the phase cannon in any way."),
                SecondMessageToShow = "You cannot seem to affect the phase cannon in any way.",
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("phase cannonにはどうやっても影響を与えられそうにない。"));
                Assert.That(HitCount("CannotAffect"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedConfirmation_WhenOwnerPatched()
    {
        const string source = "Are you sure you want to disassemble the bronze dagger?";

        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                MessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
                Assert.That(HitCount("DisassemblyConfirmation"), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("You cannot use disassemble all with hostiles nearby.")]
    [TestCase("You need be near the bronze dagger in order to disassemble it.")]
    public void HandleEvent_DoesNotClaimFixedOrOutOfScopePopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyTinkerItemProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowFail),
                MessageToShow = source,
            }.HandleEvent(new DummyInventoryActionEvent());

            Assert.Multiple(() =>
            {
                Assert.That(HitCount("CannotAffect"), Is.Zero);
                Assert.That(HitCount("OwnedItemDisassembly"), Is.Zero);
                Assert.That(HitCount("DisassemblyConfirmation"), Is.Zero);
                Assert.That(HitCount("ContainerOwnedDisassembly"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(TinkerItemTranslationPatch),
            RequireOwnerMethod(),
            action);
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(
                   typeof(DummyTinkerItemProducer),
                   nameof(DummyTinkerItemProducer.HandleEvent),
                   [typeof(DummyInventoryActionEvent)])
               ?? throw new MissingMethodException(
                   typeof(DummyTinkerItemProducer).FullName,
                   nameof(DummyTinkerItemProducer.HandleEvent));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(TinkerItemTranslationPatch), detail);
    }

    private sealed class DummyTinkerItemProducer
    {
        public string MessageToShow { get; set; } = string.Empty;
        public string SecondMessageToShow { get; set; } = string.Empty;
        public string PopupMethod { get; set; } = nameof(DummyPopupShow.ShowFail);

        public void HandleEvent(DummyInventoryActionEvent e)
        {
            _ = e;
            if (PopupMethod == nameof(DummyPopupShow.ShowYesNoCancel))
            {
                DummyPopupShow.ShowYesNoCancel(MessageToShow);
                if (!string.IsNullOrEmpty(SecondMessageToShow))
                {
                    DummyPopupShow.ShowYesNoCancel(SecondMessageToShow);
                }

                return;
            }

            DummyPopupShow.ShowFail(MessageToShow);
            if (!string.IsNullOrEmpty(SecondMessageToShow))
            {
                DummyPopupShow.ShowFail(SecondMessageToShow);
            }
        }
    }

    private sealed class DummyInventoryActionEvent
    {
    }
}
