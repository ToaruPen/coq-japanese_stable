using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class KeyMappingUiTranslationPatchTests
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
        "Can not remove the last binding for {{C|Move North}}.",
        "{{C|Move North}}の最後の割り当ては削除できない。")]
    [TestCase(
        "Can not remove the last binding for {{C|システムメニュー}}.",
        "{{C|システムメニュー}}の最後の割り当ては削除できない。")]
    public void Show_TranslatesLastBindingPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwner(() =>
        {
            new DummyKeyMappingProducer
            {
                PopupMethod = nameof(DummyPopupShow.Show),
                MessageToShow = source,
            }.Show();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount("LastBinding"), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "Are you sure you want to clear this binding for {{C|Move North}}?",
        "{{C|Move North}}のこの割り当てを消去してよいか？")]
    public void Show_TranslatesClearBindingPrompt_WhenOwnerPatched(
        string source,
        string expected)
    {
        WithPatchedOwner(() =>
        {
            new DummyKeyMappingProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNo),
                MessageToShow = source,
            }.Show();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
                Assert.That(HitCount("ClearBinding"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Show_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Can not remove the last binding for {{C|Move North}}.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("LastBinding"), Is.Zero);
        });
    }

    [Test]
    public void Show_DoesNotRetranslateDirectMarkedShow_WhenOwnerPatched()
    {
        const string source = "Can not remove the last binding for {{C|Move North}}.";

        WithPatchedOwner(() =>
        {
            new DummyKeyMappingProducer
            {
                PopupMethod = nameof(DummyPopupShow.Show),
                MessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.Show();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("LastBinding"), Is.Zero);
            });
        });
    }

    [Test]
    public void Show_DoesNotRetranslateDirectMarkedYesNo_WhenOwnerPatched()
    {
        const string source = "Are you sure you want to clear this binding for {{C|Move North}}?";

        WithPatchedOwner(() =>
        {
            new DummyKeyMappingProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNo),
                MessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.Show();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
                Assert.That(HitCount("ClearBinding"), Is.Zero);
            });
        });
    }

    [TestCase("")]
    [TestCase("Are you sure you want to override your keymap with the default?")]
    [TestCase("Would you like to save your changes?")]
    public void Show_DoesNotClaimFixedOrEmptyPrompts_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyKeyMappingProducer
            {
                PopupMethod = nameof(DummyPopupShow.ShowYesNo),
                MessageToShow = source,
            }.Show();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
                Assert.That(HitCount("LastBinding"), Is.Zero);
                Assert.That(HitCount("ClearBinding"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(KeyMappingUiTranslationPatch),
            RequireOwnerMethod(),
            action);
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(typeof(DummyKeyMappingProducer), nameof(DummyKeyMappingProducer.Show))
               ?? throw new MissingMethodException(
                   typeof(DummyKeyMappingProducer).FullName,
                   nameof(DummyKeyMappingProducer.Show));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(KeyMappingUiTranslationPatch), detail);
    }

    private sealed class DummyKeyMappingProducer
    {
        public string MessageToShow { get; set; } = string.Empty;
        public string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

        public void Show()
        {
            if (PopupMethod == nameof(DummyPopupShow.ShowYesNo))
            {
                DummyPopupShow.ShowYesNo(MessageToShow);
                return;
            }

            DummyPopupShow.Show(MessageToShow);
        }
    }
}
