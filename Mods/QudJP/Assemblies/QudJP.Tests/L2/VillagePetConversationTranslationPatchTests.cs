using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VillagePetConversationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Prefix_TranslatesPetQuestionAndOriginStoryAnswer()
    {
        var question = "Why are there glowfish here?";
        var answer = "They just showed up one day and started singing.";

        VillagePetConversationTranslationPatch.Prefix(ref question, ref answer);

        Assert.Multiple(() =>
        {
            Assert.That(question, Is.EqualTo("なぜここにグロウフィッシュがいるのだ？"));
            Assert.That(answer, Is.EqualTo("ある日ふらりと現れ、歌い始めたんだ。"));
            Assert.That(HitCount("petQuestion"), Is.EqualTo(1));
            Assert.That(HitCount("originStory"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Prefix_DoesNotRecordHit_ForDirectMarker()
    {
        var question = MessageFrameTranslator.DirectTranslationMarker + "Why are there glowfish here?";
        var answer = MessageFrameTranslator.DirectTranslationMarker + "Ask them yourself.";

        VillagePetConversationTranslationPatch.Prefix(ref question, ref answer);

        Assert.Multiple(() =>
        {
            Assert.That(question, Is.EqualTo("Why are there glowfish here?"));
            Assert.That(answer, Is.EqualTo("Ask them yourself."));
            Assert.That(HitCount("petQuestion"), Is.Zero);
            Assert.That(HitCount("originStory"), Is.Zero);
        });
    }

    private static int HitCount(string route) =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(VillagePetConversationTranslationPatch),
            nameof(VillagePetConversationTranslationPatch) + "." + route);
}
