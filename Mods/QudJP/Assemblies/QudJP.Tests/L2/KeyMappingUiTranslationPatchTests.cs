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

    [TestCase(
        "Can not remove the last binding for {{C|Fire}}.",
        "{{C|Fire}}の最後の割り当ては削除できない。")]
    [TestCase(
        "Can not remove the last binding for {{C|発射}}.",
        "{{C|発射}}の最後の割り当ては削除できない。")]
    public void HandleMenuOption_TranslatesLastBindingAsyncPopup_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertKeybindsMenuOptionAsyncPopup(
            nameof(DummyPopupShow.ShowAsync),
            source,
            expected,
            "LastBinding",
            expectedHitCount: 1);
    }

    [TestCase(
        "Are you sure you want to clear the binding for {{C|Ctrl+F}}?",
        "{{C|Ctrl+F}}の割り当てを消去してよいか？")]
    public void HandleMenuOption_TranslatesClearBindingAsyncPrompt_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertKeybindsMenuOptionAsyncPopup(
            nameof(DummyPopupShow.ShowYesNoAsync),
            source,
            expected,
            "ClearBinding",
            expectedHitCount: 1);
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
    public void HandleMenuOption_DoesNotClaimAsyncPopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Can not remove the last binding for {{C|Fire}}.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.ShowAsync(source).GetAwaiter().GetResult());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(source));
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

    [Test]
    public void HandleMenuOption_DoesNotRetranslateDirectMarkedAsyncShow_WhenOwnerPatched()
    {
        const string source = "Can not remove the last binding for {{C|Fire}}.";

        AssertKeybindsMenuOptionAsyncPopup(
            nameof(DummyPopupShow.ShowAsync),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "LastBinding",
            expectedHitCount: 0);
    }

    [Test]
    public void HandleMenuOption_DoesNotRetranslateDirectMarkedAsyncYesNo_WhenOwnerPatched()
    {
        const string source = "Are you sure you want to clear the binding for {{C|Ctrl+F}}?";

        AssertKeybindsMenuOptionAsyncPopup(
            nameof(DummyPopupShow.ShowYesNoAsync),
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            "ClearBinding",
            expectedHitCount: 0);
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

    [TestCase("")]
    [TestCase("Are you sure you want to override your keymap with the default?")]
    public void HandleMenuOption_DoesNotClaimFixedOrEmptyAsyncPrompts_WhenOwnerPatched(string source)
    {
        AssertKeybindsMenuOptionAsyncPopup(
            nameof(DummyPopupShow.ShowYesNoAsync),
            source,
            source,
            "LastBinding",
            expectedHitCount: 0);
        Assert.That(HitCount("ClearBinding"), Is.Zero);
    }

    private static void WithPatchedOwner(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(KeyMappingUiTranslationPatch),
            RequireOwnerMethod(),
            action);
    }

    private static void WithPatchedKeybindsOwner(Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(KeyMappingUiTranslationPatch),
            RequireKeybindsOwnerMethod(),
            action);
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return AccessTools.Method(typeof(DummyKeyMappingProducer), nameof(DummyKeyMappingProducer.Show))
               ?? throw new MissingMethodException(
                   typeof(DummyKeyMappingProducer).FullName,
                   nameof(DummyKeyMappingProducer.Show));
    }

    private static MethodInfo RequireKeybindsOwnerMethod()
    {
        return AccessTools.Method(typeof(DummyKeybindsMenuOptionProducer), nameof(DummyKeybindsMenuOptionProducer.HandleMenuOption))
               ?? throw new MissingMethodException(
                   typeof(DummyKeybindsMenuOptionProducer).FullName,
                   nameof(DummyKeybindsMenuOptionProducer.HandleMenuOption));
    }

    private static void AssertKeybindsMenuOptionAsyncPopup(
        string popupMethod,
        string source,
        string expected,
        string detail,
        int expectedHitCount)
    {
        WithPatchedKeybindsOwner(() =>
        {
            new DummyKeybindsMenuOptionProducer
            {
                PopupMethod = popupMethod,
                MessageToShow = source,
            }.HandleMenuOption().GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(LastKeybindsPopupMessage(popupMethod), Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(expectedHitCount));
            });
        });
    }

    private static string? LastKeybindsPopupMessage(string popupMethod)
    {
        return popupMethod == nameof(DummyPopupShow.ShowYesNoAsync)
            ? DummyPopupShow.LastShowYesNoAsyncMessage
            : DummyPopupShow.LastShowAsyncMessage;
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

    private sealed class DummyKeybindsMenuOptionProducer
    {
        public string MessageToShow { get; set; } = string.Empty;
        public string PopupMethod { get; set; } = nameof(DummyPopupShow.ShowAsync);

        public async Task HandleMenuOption()
        {
            if (PopupMethod == nameof(DummyPopupShow.ShowYesNoAsync))
            {
                _ = await DummyPopupShow.ShowYesNoAsync(MessageToShow).ConfigureAwait(false);
                return;
            }

            await DummyPopupShow.ShowAsync(MessageToShow).ConfigureAwait(false);
        }
    }
}
