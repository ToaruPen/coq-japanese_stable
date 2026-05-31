using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ProselytizationSifrahTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(RepositoryDictionaryDirectory());
        MessageFrameTranslator.SetDictionaryPathForTests(RepositoryMessageFramePath());
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultCriticalFailure),
        "{{R|怒れる遊牧民}} is offended by your impertinence.",
        "{{R|怒れる遊牧民}}はあなたの無礼に気分を害した。",
        "CriticalFailure")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultFailure),
        "{{Y|砂漠の隠者}} is unconvinced by your pleas.",
        "{{Y|砂漠の隠者}}はあなたの懇願に納得しなかった。",
        "Failure")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultPartialSuccess),
        "{{C|眠たげな商人}} is unconvinced by your pleas, but interested in hearing more.",
        "{{C|眠たげな商人}}はあなたの懇願に納得しなかったが、さらに聞きたがっている。",
        "PartialSuccess")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultSuccess),
        "{{G|輝く巡礼者 is sympathetic, but unable to join you.}}",
        "{{G|輝く巡礼者は同情的だが、あなたに加われない。}}",
        "SympatheticButUnable")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultExceptionalSuccess),
        "古代の番人 are sympathetic, but unable to join you.",
        "古代の番人は同情的だが、あなたに加われない。",
        "SympatheticButUnable")]
    public void Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ProselytizationSifrahTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummyProselytizationSifrahProducerTarget
                {
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(ProselytizationHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                const string source = "{{Y|砂漠の隠者}} is unconvinced by your pleas.";
                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(ProselytizationHitCount("Failure"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_TranslatesMarkedDoesVerbPopup_WhenOwnerPatched()
    {
        const string subject = "The 巡礼者";
        var source = DoesVerbRouteTranslator.MarkDoesFragment(
            subject + " is",
            "are",
            subject.Length,
            null) + " unconvinced by your pleas.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ProselytizationSifrahTranslationPatch),
            RequireOwnerMethod(nameof(DummyProselytizationSifrahProducerTarget.ResultFailure)),
            () =>
            {
                var target = new DummyProselytizationSifrahProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("巡礼者はあなたの懇願に納得していない"));
                    Assert.That(ProselytizationHitCount("DoesVerb"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ProselytizationSifrahTranslationPatch),
            RequireOwnerMethod(nameof(DummyProselytizationSifrahProducerTarget.ResultFailure)),
            () =>
            {
                var source = MessageFrameTranslator.MarkDirectTranslation("{{Y|砂漠の隠者}} is unconvinced by your pleas.");
                var target = new DummyProselytizationSifrahProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|砂漠の隠者}} is unconvinced by your pleas."));
                    Assert.That(ProselytizationHitCount("Failure"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ProselytizationSifrahTranslationPatch),
            RequireOwnerMethod(nameof(DummyProselytizationSifrahProducerTarget.ResultFailure)),
            () =>
            {
                var target = new DummyProselytizationSifrahProducerTarget();

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(ProselytizationHitCount("Failure"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyProselytizationSifrahProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static void InvokeOwnerMethod(DummyProselytizationSifrahProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, new object[] { new DummyGameObject() });
    }

    private static int ProselytizationHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ProselytizationSifrahTranslationPatch), detail);
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

    private static string RepositoryDictionaryDirectory()
    {
        return Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries");
    }
}
