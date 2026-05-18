using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DynamicQuestExplicitConversationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void IntroChoicePrefix_TranslatesAndRecordsIntroChoice()
    {
        var text = "Yes. I will find the rusted relic as you ask.";

        DynamicQuestIntroChoiceTranslationPatch.Prefix(ref text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("はい。頼まれたとおり錆びた遺物を探す。"));
            Assert.That(IntroHitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void IntroChoicePrefix_StripsDirectMarkerWithoutObservabilityHit()
    {
        var text = MessageFrameTranslator.DirectTranslationMarker + "No, I will not.";

        DynamicQuestIntroChoiceTranslationPatch.Prefix(ref text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("No, I will not."));
            Assert.That(IntroHitCount(), Is.Zero);
        });
    }

    [Test]
    public void CompletionPrefix_TranslatesCompletionAndIncompleteChoices()
    {
        var complete = "I've found the rusted relic.";
        var incomplete = "I don't have the rusted relic yet.";

        DynamicQuestConversationTranslationPatch.Prefix(ref complete, ref incomplete);

        Assert.Multiple(() =>
        {
            Assert.That(complete, Is.EqualTo("錆びた遺物を見つけた。"));
            Assert.That(incomplete, Is.EqualTo("まだ錆びた遺物を持っていない。"));
            Assert.That(ConversationHitCount("CompletionChoice"), Is.EqualTo(1));
            Assert.That(ConversationHitCount("IncompleteChoice"), Is.EqualTo(1));
        });
    }

    private static int IntroHitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(DynamicQuestIntroChoiceTranslationPatch),
            nameof(DynamicQuestIntroChoiceTranslationPatch) + ".IntroChoice");

    private static int ConversationHitCount(string route) =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(DynamicQuestConversationTranslationPatch),
            nameof(DynamicQuestConversationTranslationPatch) + "." + route);
}
