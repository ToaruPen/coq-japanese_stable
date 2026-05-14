using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationScriptPopupTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-conversation-script-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json"));
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
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "You can't seem to make out what {{Y|the snapjaw}} is saying.",
        "{{Y|snapjaw}}が何と言っているのか聞き取れない。",
        "MakeOutSpeech")]
    [TestCase(
        "The merchant is utterly unresponsive.",
        "merchantはまったく反応しない",
        "UtterlyUnresponsive")]
    [TestCase(
        "The merchant refuses to speak to you.",
        "merchantはあなたと話そうとしない。",
        "RefuseToSpeak")]
    [TestCase(
        "The merchant is engaged in hand-to-hand combat and is too busy to have a conversation with you.",
        "merchantは格闘戦闘中で会話どころではない",
        "TooBusyCombat")]
    [TestCase(
        "The merchant is on fire and is too busy to have a conversation with you.",
        "merchantは燃えていてそれどころではない",
        "TooBusyOnFire")]
    [TestCase(
        "You cannot seem to engage the merchant in conversation.",
        "merchantと会話を始められない。",
        "EngageConversation")]
    public void PhysicalConversation_TranslatesOwnerPopupShapes_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(
            nameof(DummyConversationScriptPopupProducerTarget.IsPhysicalConversationPossible),
            source,
            expected,
            detail);
    }

    [TestCase(
        "You can sense nothing from the merchant.",
        "merchantからは何も感じ取れない。",
        "SenseNothing")]
    [TestCase(
        "You sense only hostility from {{R|the snapjaw}}.",
        "{{R|snapjaw}}からは敵意しか感じない。",
        "SenseHostility")]
    [TestCase(
        "You cannot seem to make contact with the merchant.",
        "merchantとうまく交信できない。",
        "MakeContact")]
    public void MentalConversation_TranslatesOwnerPopupShapes_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(
            nameof(DummyConversationScriptPopupProducerTarget.IsMentalConversationPossible),
            source,
            expected,
            detail);
    }

    [TestCase(
        "You can't seem to make out what the merchant is saying.",
        "MakeOutSpeech")]
    [TestCase(
        "You can sense nothing from the merchant.",
        "SenseNothing")]
    public void TryTranslatePopupMessage_DoesNotClaimConversationPopup_WhenOwnerAbsent(
        string source,
        string detail)
    {
        var claimed = ConversationScriptPopupTranslationPatch.TryTranslatePopupMessage(
            source,
            nameof(PopupShowTranslationPatch),
            nameof(ConversationScriptPopupTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(claimed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    [TestCase(nameof(DummyConversationScriptPopupProducerTarget.IsPhysicalConversationPossible), "MakeOutSpeech")]
    [TestCase(nameof(DummyConversationScriptPopupProducerTarget.IsMentalConversationPossible), "SenseNothing")]
    public void ConversationScriptPopup_DoesNotRetranslateDirectMarkedShowFail_WhenOwnerPatched(
        string methodName,
        string detail)
    {
        const string source = "You can't seem to make out what the merchant is saying.";

        AssertOwnerPopup(
            methodName,
            MessageFrameTranslator.MarkDirectTranslation(source),
            source,
            detail,
            expectedHits: 0);
    }

    [Test]
    public void ConversationScriptPopup_DirectMarkerPassThroughDoesNotLeakToNextPopup_WhenOwnerPatched()
    {
        const string directSource = "You can't seem to make out what the merchant is saying.";
        const string nextSource = "The merchant refuses to speak to you.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ConversationScriptPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummyConversationScriptPopupProducerTarget.IsPhysicalConversationPossible)),
            () =>
            {
                var target = new DummyConversationScriptPopupProducerTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(directSource),
                    SecondPopupMessageToShow = nextSource,
                };

                _ = target.IsPhysicalConversationPossible();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("merchantはあなたと話そうとしない。"));
                    Assert.That(HitCount("MakeOutSpeech"), Is.Zero);
                    Assert.That(HitCount("RefuseToSpeak"), Is.EqualTo(1));
                });
            });
    }

    [TestCase(nameof(DummyConversationScriptPopupProducerTarget.IsPhysicalConversationPossible), "MakeOutSpeech")]
    [TestCase(nameof(DummyConversationScriptPopupProducerTarget.IsMentalConversationPossible), "SenseNothing")]
    public void ConversationScriptPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched(string methodName, string detail)
    {
        AssertOwnerPopup(methodName, string.Empty, string.Empty, detail, expectedHits: 0);
    }

    [Test]
    public void PhysicalConversation_LeavesFixedCandidatePopupUnclaimed_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            nameof(DummyConversationScriptPopupProducerTarget.IsPhysicalConversationPossible),
            "You are in no shape to start a conversation.",
            "You are in no shape to start a conversation.",
            "MakeOutSpeech",
            expectedHits: 0);
    }

    [Test]
    public void PhysicalConversation_LeavesRuntimeFailurePopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            nameof(DummyConversationScriptPopupProducerTarget.IsPhysicalConversationPossible),
            "The speaker sends a custom refusal from an event.",
            "The speaker sends a custom refusal from an event.",
            "EngageConversation",
            expectedHits: 0);
    }

    [Test]
    public void PhysicalConversation_PreservesWholeSourceColorWrapper_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            nameof(DummyConversationScriptPopupProducerTarget.IsPhysicalConversationPossible),
            "{{W|The merchant refuses to speak to you.}}",
            "{{W|merchantはあなたと話そうとしない。}}",
            "RefuseToSpeak");
    }

    private static void AssertOwnerPopup(
        string methodName,
        string source,
        string expected,
        string detail,
        int expectedHits = 1)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ConversationScriptPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                var target = new DummyConversationScriptPopupProducerTarget { PopupMessageToShow = source };

                _ = RequireOwnerMethod(methodName).Invoke(target, []);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyConversationScriptPopupProducerTarget), methodName);
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ConversationScriptPopupTranslationPatch), detail);
    }
}
