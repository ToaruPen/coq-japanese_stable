using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupPickSeveralTranslationPatchTests
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

    [TestCase("You cannot select more than 3 options!", "選択肢は3個までしか選べない！")]
    [TestCase("You cannot select more than three options!", "選択肢は3個までしか選べない！")]
    public void Patch_TranslatesSelectionLimitPopup_WhenOwnerPatched(string source, string expected)
    {
        WithPatchedOwnerAndPopup(() =>
        {
            var target = new DummyPopupPickSeveralProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.PickSeveral();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount("SelectionLimit"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslateSelectionLimitPopup_WhenOwnerAbsent()
    {
        const string source = "You cannot select more than 3 options!";

        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.Show(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("SelectionLimit"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string unmarked = "You cannot select more than 3 options!";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        WithPatchedOwnerAndPopup(() =>
        {
            var target = new DummyPopupPickSeveralProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.PickSeveral();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                Assert.That(HitCount("SelectionLimit"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_PassesThroughUnsupportedDirectMarkedPopup_WhenOwnerPatched()
    {
        const string unmarked = "You can select as many options as you want.";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        WithPatchedOwnerAndPopup(() =>
        {
            var target = new DummyPopupPickSeveralProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.PickSeveral();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                Assert.That(HitCount("SelectionLimit"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesUnsupportedEnglishPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "You can select as many options as you want.";

        WithPatchedOwnerAndPopup(() =>
        {
            var target = new DummyPopupPickSeveralProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.PickSeveral();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("SelectionLimit"), Is.Zero);
            });
        });
    }

    [TestCase("{{W|You cannot select more than 3 options!}}", "{{W|選択肢は3個までしか選べない！}}")]
    [TestCase("&GYou cannot select more than three options!", "&G選択肢は3個までしか選べない！")]
    public void Patch_PreservesColorTags_WhenOwnerPatched(string source, string expected)
    {
        WithPatchedOwnerAndPopup(() =>
        {
            var target = new DummyPopupPickSeveralProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.PickSeveral();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount("SelectionLimit"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwnerAndPopup(() =>
        {
            var target = new DummyPopupPickSeveralProducerTarget
            {
                PopupMessageToShow = string.Empty,
            };

            target.PickSeveral();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("SelectionLimit"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwnerAndPopup(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupPickSeveralProducerTarget), nameof(DummyPopupPickSeveralProducerTarget.PickSeveral)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickSeveralTranslationPatch), nameof(PopupPickSeveralTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickSeveralTranslationPatch), nameof(PopupPickSeveralTranslationPatch.Finalizer))));

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(PopupPickSeveralTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyPopupPickSeveralProducerTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PickSeveral()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
