using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ExaminerTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultSuccess),
        "You now understand {{C|奇妙な装置}}.",
        "{{C|奇妙な装置}}を理解した。",
        "Understand")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultExceptionalSuccess),
        "You discover something about {{Y|古びた箱}} that was hidden!",
        "{{Y|古びた箱}}について隠されていたことを発見した！",
        "DiscoverHidden")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultFailure),
        "You are puzzled by {{R|ひび割れた銃}}.",
        "{{R|ひび割れた銃}}のことがわからない。",
        "Puzzled")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultFakeConfusionFailure),
        "You think you broke {{G|謎の装置}}...",
        "{{G|謎の装置}}を壊してしまった気がする。",
        "Broke")]
    [TestCase(
        nameof(DummyExaminerProducerTarget.ResultCriticalFailure),
        "You are puzzled by {{R|ひび割れた銃}}.",
        "{{R|ひび割れた銃}}のことがわからない。",
        "Puzzled")]
    public void Patch_TranslatesExaminerResultPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, methodName);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(ExaminerHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        nameof(DummyPopupShow.ShowFail),
        "Whatever it is, it's broken...",
        "それが何であれ、壊れている...",
        "Broken")]
    [TestCase(
        nameof(DummyPopupShow.ShowYesNoCancel),
        "{{Y|奇妙な装置}} is not owned by you, and examining it risks damaging it. Are you sure you want to do so?",
        "{{Y|奇妙な装置}}はあなたのものではない。調べるとそれを傷つけるおそれがある。それでもそうしますか？",
        "OwnedExamine")]
    [TestCase(
        nameof(DummyPopupShow.ShowYesNoCancel),
        "{{Y|箱}} is not owned by you, and examining a {{C|奇妙な装置}} inside it risks causing damage. Are you sure you want to do so?",
        "{{Y|箱}}はあなたのものではない。それの中にある{{C|奇妙な装置}}を調べると損傷を引き起こすおそれがある。それでもそうしますか？",
        "ContainerOwnedExamine")]
    public void Patch_TranslatesExaminerHandleEventPopups_WhenOwnerPatched(
        string popupMethod,
        string source,
        string expected,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.HandleEvent)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                    PopupMethod = popupMethod,
                };

                _ = target.HandleEvent(new DummyInventoryActionEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(expected));
                    Assert.That(ExaminerHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslateHandleEventPopup_WhenOwnerAbsent()
    {
        const string source =
            "{{Y|奇妙な装置}} is not owned by you, and examining it risks damaging it. Are you sure you want to do so?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                _ = DummyPopupShow.ShowYesNoCancel(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(source));
                    Assert.That(ExaminerHitCount("OwnedExamine"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedHandleEventPopup_WhenOwnerPatched()
    {
        const string unmarked =
            "{{Y|奇妙な装置}} is not owned by you, and examining it risks damaging it. Are you sure you want to do so?";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.HandleEvent)),
            () =>
            {
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                    PopupMethod = nameof(DummyPopupShow.ShowYesNoCancel),
                };

                _ = target.HandleEvent(new DummyInventoryActionEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowYesNoCancelMessage, Is.EqualTo(unmarked));
                    Assert.That(ExaminerHitCount("OwnedExamine"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotTranslateExaminerPopup_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () =>
            {
                const string source = "You now understand {{C|奇妙な装置}}.";
                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(ExaminerHitCount("Understand"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                var source = MessageFrameTranslator.MarkDirectTranslation("You are puzzled by {{R|ひび割れた銃}}.");
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You are puzzled by {{R|ひび割れた銃}}."));
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesDirectMarkedNonMatchingEnglishPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                var source = MessageFrameTranslator.MarkDirectTranslation("You inspect {{C|奇妙な装置}} carefully.");
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You inspect {{C|奇妙な装置}} carefully."));
                    Assert.That(ExaminerHitCount("Understand"), Is.Zero);
                    Assert.That(ExaminerHitCount("DiscoverHidden"), Is.Zero);
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                    Assert.That(ExaminerHitCount("Broke"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesNonMatchingEnglishPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                const string source = "You inspect {{C|奇妙な装置}} carefully.";
                var target = new DummyExaminerProducerTarget
                {
                    PopupMessageToShow = source,
                };

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(ExaminerHitCount("Understand"), Is.Zero);
                    Assert.That(ExaminerHitCount("DiscoverHidden"), Is.Zero);
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                    Assert.That(ExaminerHitCount("Broke"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ExaminerTranslationPatch),
            RequireOwnerMethod(nameof(DummyExaminerProducerTarget.ResultFailure)),
            () =>
            {
                var target = new DummyExaminerProducerTarget();

                target.ResultFailure(new DummyGameObject());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(ExaminerHitCount("Puzzled"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        if (methodName == nameof(DummyExaminerProducerTarget.HandleEvent))
        {
            return OwnerPopupRouteTestHarness.RequireMethod(
                typeof(DummyExaminerProducerTarget),
                methodName,
                typeof(DummyInventoryActionEvent));
        }

        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyExaminerProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static void InvokeOwnerMethod(DummyExaminerProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, new object[] { new DummyGameObject() });
    }

    private static int ExaminerHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ExaminerTranslationPatch), detail);
    }

    private static string? LastPopupMessage(string popupMethod)
    {
        return popupMethod == nameof(DummyPopupShow.ShowYesNoCancel)
            ? DummyPopupShow.LastShowYesNoCancelMessage
            : DummyPopupShow.LastShowMessage;
    }
}
