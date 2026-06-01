using System.Reflection;
using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CyberneticsTerminalInterfacePopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(RepositoryMessageFramePath());
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase("unpowered", "変容の窪みは無電力だ。")]
    [TestCase("still starting up", "変容の窪みはまだ起動中だ")]
    public void AttemptInterface_TranslatesPoweredStatusFailurePopup_WhenOwnerPatched(
        string status,
        string expected)
    {
        var target = new DummyCyberneticsTerminal2InterfaceTarget
        {
            PopupMessageToShow = MarkDoesFragment("The Becoming nook is", "are") + " " + status + ".",
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsTerminalInterfacePopupTranslationPatch),
            RequireOwnerMethod(),
            () => target.AttemptInterface());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            Assert.That(GetHitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void AttemptInterface_LeavesPlainPopupUnchanged_WhenOwnerAbsent()
    {
        const string source = "The Becoming nook is unpowered.";
        var target = new DummyCyberneticsTerminal2InterfaceTarget
        {
            PopupMessageToShow = source,
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => target.AttemptInterface());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    [Test]
    public void AttemptInterface_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched()
    {
        const string translated = "変容の窪みは無電力だ。";
        var target = new DummyCyberneticsTerminal2InterfaceTarget
        {
            PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(translated),
        };

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CyberneticsTerminalInterfacePopupTranslationPatch),
            RequireOwnerMethod(),
            () => target.AttemptInterface());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(translated));
            Assert.That(GetHitCount(), Is.Zero);
        });
    }

    private static string MarkDoesFragment(string fragment, string verb)
    {
        return DoesVerbRouteTranslator.MarkDoesFragment(fragment, verb, fragment.LastIndexOf(' '), null);
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyCyberneticsTerminal2InterfaceTarget),
            nameof(DummyCyberneticsTerminal2InterfaceTarget.AttemptInterface));
    }

    private static int GetHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.CyberneticsTerminalInterfacePopupTranslationPatch.DoesVerb");
    }

    private static string RepositoryMessageFramePath()
    {
        return Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json");
    }

    private sealed class DummyCyberneticsTerminal2InterfaceTarget
    {
        public string PopupMessageToShow { get; init; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AttemptInterface()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return false;
        }
    }
}
