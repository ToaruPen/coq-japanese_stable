using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ModMagnetizedTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        UseRepositoryVerbDictionary();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(" to the ground; you pick it up.", "装置は地面に落ちた。あなたはそれを拾った。")]
    [TestCase(" to the ground.", "装置は地面に倒れた。")]
    public void CheckFloating_TranslatesDoesVerbPopup_WhenOwnerPatched(string tail, string expected)
    {
        var target = new DummyModMagnetizedProducerTarget
        {
            PopupMessageToShow = MarkDoesFragment("The 装置 falls") + tail,
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ModMagnetizedTranslationPatch),
            RequireOwnerMethod(),
            target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            Assert.That(ModMagnetizedHitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void CheckFloating_LeavesPlainPopupUnchanged_WhenOwnerAbsent()
    {
        const string source = "The 装置 falls to the ground.";
        var target = new DummyModMagnetizedProducerTarget
        {
            PopupMessageToShow = source,
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(ModMagnetizedHitCount(), Is.Zero);
        });
    }

    [Test]
    public void CheckFloating_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched()
    {
        const string translated = "装置は地面に倒れた。";
        var target = new DummyModMagnetizedProducerTarget
        {
            PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(translated),
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ModMagnetizedTranslationPatch),
            RequireOwnerMethod(),
            target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(translated));
            Assert.That(ModMagnetizedHitCount(), Is.Zero);
        });
    }

    [Test]
    public void CheckFloating_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var target = new DummyModMagnetizedProducerTarget();

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ModMagnetizedTranslationPatch),
            RequireOwnerMethod(),
            target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            Assert.That(ModMagnetizedHitCount(), Is.Zero);
        });
    }

    [Test]
    public void CheckFloating_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        var innerTarget = new DummyNestedModMagnetizedProducerTarget
        {
            PopupMessageToShow = MarkDoesFragment("The 装置 falls") + " to the ground; you pick it up.",
        };
        var outerTarget = new DummyNestedModMagnetizedProducerTarget
        {
            PopupMessageToShow = MarkDoesFragment("The 装置 falls") + " to the ground.",
            BeforePopup = () =>
            {
                innerTarget.NestedCheckFloating();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("装置は地面に落ちた。あなたはそれを拾った。"));
                    Assert.That(ModMagnetizedHitCount(), Is.EqualTo(1));
                });
            },
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwners(
            typeof(ModMagnetizedTranslationPatch),
            [
                RequireNestedOwnerMethod(nameof(DummyNestedModMagnetizedProducerTarget.CheckFloating)),
                RequireNestedOwnerMethod(nameof(DummyNestedModMagnetizedProducerTarget.NestedCheckFloating)),
            ],
            outerTarget.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("装置は地面に倒れた。"));
            Assert.That(ModMagnetizedHitCount(), Is.EqualTo(2));
        });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyModMagnetizedProducerTarget),
            nameof(DummyModMagnetizedProducerTarget.CheckFloating));
    }

    private static MethodInfo RequireNestedOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyNestedModMagnetizedProducerTarget), methodName);
    }

    private static string MarkDoesFragment(string fragment)
    {
        return DoesVerbRouteTranslator.MarkDoesFragment(fragment, "fall", "The 装置".Length, null);
    }

    private static int ModMagnetizedHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(ModMagnetizedTranslationPatch) + ".DoesVerb");
    }

    private static void UseRepositoryVerbDictionary()
    {
        var repositoryDictionaryPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json");
        MessageFrameTranslator.SetDictionaryPathForTests(repositoryDictionaryPath);
    }

    private sealed class DummyNestedModMagnetizedProducerTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;
        public Action? BeforePopup { get; init; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CheckFloating()
        {
            BeforePopup?.Invoke();
            DummyPopupShow.Show(PopupMessageToShow);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void NestedCheckFloating()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
