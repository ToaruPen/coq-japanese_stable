using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationCheckLostPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));

        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json"));

        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyConversationCheckLostTarget.PopupMessageToShow = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
        DummyConversationCheckLostTarget.PopupMessageToShow = string.Empty;
    }

    [TestCase(
        "You ask about your location and are no longer lost.",
        "場所を尋ね、もう迷子ではなくなる。",
        "ListenerNoLongerLost")]
    [TestCase(
        "Argyve asks about his location and is no longer lost.",
        "Argyveは自分の居場所について尋ね、もう迷っていない",
        "SpeakerNoLongerLost")]
    public void CheckLost_TranslatesLostRecoveryPopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(source, expected, detail);
    }

    [Test]
    public void CheckLost_TranslatesMarkedSpeakerLostRecoveryPopup_WhenOwnerPatched()
    {
        var source = DoesVerbRouteTranslator.MarkDoesFragment(
            "The warden asks",
            "ask",
            "The warden".Length,
            null)
            + " about its location and is no longer lost.";

        AssertOwnerPopup(
            source,
            "wardenは自分の居場所について尋ね、もう迷っていない",
            "SpeakerNoLongerLost");
    }

    [Test]
    public void CheckLost_TranslatesMarkedPluralSpeakerLostRecoveryPopup_WhenOwnerPatched()
    {
        var source = DoesVerbRouteTranslator.MarkDoesFragment(
            "The villagers ask",
            "ask",
            "The villagers".Length,
            null)
            + " about their location and are no longer lost.";

        AssertOwnerPopup(
            source,
            "villagersは自分の居場所について尋ね、もう迷っていない",
            "SpeakerNoLongerLost");
    }

    [Test]
    public void CheckLost_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            MessageFrameTranslator.MarkDirectTranslation("You ask about your location and are no longer lost."),
            "You ask about your location and are no longer lost.",
            "ListenerNoLongerLost",
            expectedHits: 0);
    }

    [Test]
    public void CheckLost_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(string.Empty, string.Empty, "ListenerNoLongerLost", expectedHits: 0);
    }

    [Test]
    public void CheckLost_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup("The conversation continues.", "The conversation continues.", "ListenerNoLongerLost", expectedHits: 0);
    }

    [TestCase(
        "You ask about your location and are no longer lost.",
        "ListenerNoLongerLost")]
    [TestCase(
        "Argyve asks about his location and is no longer lost.",
        "SpeakerNoLongerLost")]
    public void CheckLost_DoesNotClaimLostRecoveryPopup_WhenOwnerAbsent(string source, string detail)
    {
        var claimed = ConversationCheckLostPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            nameof(PopupShowTranslationPatch),
            nameof(ConversationCheckLostPopupTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(claimed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    private static void AssertOwnerPopup(
        string source,
        string expected,
        string detail,
        int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ConversationCheckLostPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                DummyConversationCheckLostTarget.PopupMessageToShow = source;

                DummyConversationCheckLostTarget.CheckLost();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummyConversationCheckLostTarget),
            nameof(DummyConversationCheckLostTarget.CheckLost));
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ConversationCheckLostPopupTranslationPatch), detail);
    }
}
