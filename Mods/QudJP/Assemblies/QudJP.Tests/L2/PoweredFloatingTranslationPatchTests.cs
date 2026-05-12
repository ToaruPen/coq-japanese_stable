using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PoweredFloatingTranslationPatchTests
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

    [TestCase("cease", " floating near you.", "装置はあなたの近くで浮遊するのをやめた")]
    [TestCase("fall", " to the ground; you scoop it up.", "装置は地面に落ちた。あなたはそれをすくい上げた。")]
    [TestCase("fall", " to the ground.", "装置は地面に倒れた。")]
    public void CheckFloating_TranslatesDoesVerbPopup_WhenOwnerPatched(
        string verb,
        string tail,
        string expected)
    {
        var target = new DummyPoweredFloatingProducerTarget
        {
            PopupMessageToShow = MarkDoesFragment("The 装置 " + BuildVerbForm(verb), verb) + tail,
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(PoweredFloatingTranslationPatch),
            RequireOwnerMethod(),
            target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            Assert.That(PoweredFloatingHitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void CheckFloating_LeavesPlainPopupUnchanged_WhenOwnerAbsent()
    {
        const string source = "The 装置 falls to the ground.";
        var target = new DummyPoweredFloatingProducerTarget
        {
            PopupMessageToShow = source,
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(PoweredFloatingHitCount(), Is.Zero);
        });
    }

    [Test]
    public void CheckFloating_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched()
    {
        const string translated = "装置は地面に倒れた。";
        var target = new DummyPoweredFloatingProducerTarget
        {
            PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(translated),
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(PoweredFloatingTranslationPatch),
            RequireOwnerMethod(),
            target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(translated));
            Assert.That(PoweredFloatingHitCount(), Is.Zero);
        });
    }

    [Test]
    public void CheckFloating_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        var innerTarget = new NestedPoweredFloatingProducerTarget
        {
            PopupMessageToShow = MarkDoesFragment("The 装置 " + BuildVerbForm("cease"), "cease") + " floating near you.",
        };
        var outerTarget = new NestedPoweredFloatingProducerTarget
        {
            PopupMessageToShow = MarkDoesFragment("The 装置 " + BuildVerbForm("fall"), "fall") + " to the ground.",
            BeforePopup = () =>
            {
                innerTarget.CheckFloating();
                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("装置はあなたの近くで浮遊するのをやめた"));
                    Assert.That(PoweredFloatingHitCount(), Is.EqualTo(1));
                });
            },
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(PoweredFloatingTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(typeof(NestedPoweredFloatingProducerTarget), nameof(NestedPoweredFloatingProducerTarget.CheckFloating)),
            outerTarget.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("装置は地面に倒れた。"));
            Assert.That(PoweredFloatingHitCount(), Is.EqualTo(2));
        });
    }

    [Test]
    public void CheckFloating_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var target = new DummyPoweredFloatingProducerTarget();

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(PoweredFloatingTranslationPatch),
            RequireOwnerMethod(),
            target.CheckFloating);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            Assert.That(PoweredFloatingHitCount(), Is.Zero);
        });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyPoweredFloatingProducerTarget),
            nameof(DummyPoweredFloatingProducerTarget.CheckFloating));
    }

    private static string MarkDoesFragment(string fragment, string verb)
    {
        return DoesVerbRouteTranslator.MarkDoesFragment(fragment, verb, "The 装置".Length, null);
    }

    private static string BuildVerbForm(string verb)
    {
        return string.Equals(verb, "cease", StringComparison.Ordinal) ? "ceases" : "falls";
    }

    private static int PoweredFloatingHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(PoweredFloatingTranslationPatch) + ".DoesVerb");
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

    private sealed class NestedPoweredFloatingProducerTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public Action? BeforePopup { get; set; }

        public void CheckFloating()
        {
            BeforePopup?.Invoke();
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
