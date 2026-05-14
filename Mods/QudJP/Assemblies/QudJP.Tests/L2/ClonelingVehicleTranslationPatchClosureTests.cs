using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ClonelingVehicleTranslationPatchClosureTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        SinkObservation.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You do not have 1 dram of cloning draught.",
        "cloning draughtを1ドラム持っていない。")]
    [TestCase(
        "You do not have 1 dram of {{Y|cloning draught}}.",
        "{{Y|cloning draught}}を1ドラム持っていない。")]
    public void ClonelingHandleEvent_TranslatesOneDramPopupFailure_WhenOwnerPatched(
        string source,
        string expected)
    {
        AssertOwnerPopup(source, expected);
    }

    [Test]
    public void ClonelingHandleEvent_DoesNotTranslateOneDramPopupFailure_WhenOwnerAbsent()
    {
        const string source = "You do not have 1 dram of cloning draught.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowFail(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        });
    }

    [Test]
    public void ClonelingHandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You do not have 1 dram of cloning draught.";

        AssertOwnerPopup(MessageFrameTranslator.MarkDirectTranslation(source), source);
    }

    [Test]
    public void ClonelingHandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty);
    }

    [Test]
    public void ClonelingHandleEvent_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "You do not have enough cloning draught.";

        AssertOwnerPopup(source, source);
    }

    private static void AssertOwnerPopup(string source, string expected)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ClonelingVehicleTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                var target = new DummyClonelingProducerTarget { PopupMessageToShow = source };

                target.HandleEvent(new DummyInventoryActionEvent());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyClonelingProducerTarget),
            nameof(DummyClonelingProducerTarget.HandleEvent),
            typeof(DummyInventoryActionEvent));
    }

}
