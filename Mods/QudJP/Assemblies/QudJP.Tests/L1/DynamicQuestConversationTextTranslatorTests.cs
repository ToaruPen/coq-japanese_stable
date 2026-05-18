using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class DynamicQuestConversationTextTranslatorTests
{
    [TestCase("I'm looking for work.", "仕事を探している。")]
    [TestCase("Do you have work that needs doing?", "何かやるべき仕事はあるか？")]
    [TestCase("My services are available if you have work to offer.", "仕事があるなら、力を貸せる。")]
    [TestCase("Is there work around here?", "この辺りに仕事はあるか？")]
    [TestCase("Speak to ", "話す相手：")]
    [TestCase("Talk to ", "話す相手：")]
    [TestCase("Find ", "探す相手：")]
    [TestCase("話す相手：{{Y|Mehmet}}, to the north.", "北にいる{{Y|Mehmet}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}, to the north or {{Y|Ashe}}, to the south.", "北にいる{{Y|Mehmet}}、または南にいる{{Y|Ashe}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}, to the north, or {{Y|Ashe}}, to the south.", "北にいる{{Y|Mehmet}}、または南にいる{{Y|Ashe}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}, to the north, {{Y|Ashe}}, also to the north, or {{Y|Elder}}, to the southeast.", "北にいる{{Y|Mehmet}}、または北にいる{{Y|Ashe}}、または南東にいる{{Y|Elder}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}, here.", "ここにいる{{Y|Mehmet}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}, here, or {{Y|Ashe}}, also here.", "ここにいる{{Y|Mehmet}}、またはここにいる{{Y|Ashe}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}, somewhere, or {{Y|Ashe}}, above.", "どこかにいる{{Y|Mehmet}}、または上方にいる{{Y|Ashe}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}.", "{{Y|Mehmet}}と話す。")]
    [TestCase("話す相手：{{Y|Mehmet}}, {{Y|Ashe}}, to the south.", "{{Y|Mehmet}}、または南にいる{{Y|Ashe}}と話す。")]
    [TestCase("探す相手：{{Y|Mehmet}}, to the north.", "北にいる{{Y|Mehmet}}を探す。")]
    [TestCase("探す相手：{{Y|Mehmet}}, here.", "ここにいる{{Y|Mehmet}}を探す。")]
    [TestCase("探す相手：{{Y|Mehmet}}.", "{{Y|Mehmet}}を探す。")]
    [TestCase("探す相手：{{Y|Mehmet}}, to the north, or {{Y|Ashe}}, to the south.", "北にいる{{Y|Mehmet}}、または南にいる{{Y|Ashe}}を探す。")]
    [TestCase(
        "Our thanks, adventurer. Our village owes you a debt. For now, please choose a reward from our stockpile as payment for your service.",
        "冒険者よ、感謝する。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。")]
    [TestCase(
        "Thank you for your service, traveler. Our village owes you a debt. For now, please choose a reward from our stockpile as payment for your service.",
        "奉仕に感謝する、旅人。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。")]
    [TestCase(
        "Friend, you have our thanks. Our village owes you a debt. For now, please choose a reward from our stockpile as payment for your service.",
        "友よ、感謝する。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。")]
    [TestCase(
        "Our thanks, nomad. You've proven =player.reflexive= a friend to our village. Take this recoiler and return whenever your throat is dry.",
        "遊牧民よ、感謝する。あなたは=player.reflexive=をわれらの村の友だと示した。このリコイラーを受け取り、喉が渇いたときはいつでも戻ってきてほしい。")]
    [TestCase(
        "Thank you for your service, wanderer. You've proven =player.reflexive= a friend to our village. Take this recoiler and return whenever your throat is dry.",
        "奉仕に感謝する、放浪者。あなたは=player.reflexive=をわれらの村の友だと示した。このリコイラーを受け取り、喉が渇いたときはいつでも戻ってきてほしい。")]
    [TestCase(
        "Drifter, you have our thanks. You've proven =player.reflexive= a friend to our village. Take this recoiler and return whenever your throat is dry.",
        "漂泊者よ、感謝する。あなたは=player.reflexive=をわれらの村の友だと示した。このリコイラーを受け取り、喉が渇いたときはいつでも戻ってきてほしい。")]
    public void TryTranslate_TranslatesOwnedFrames(string source, string expected)
    {
        var translated = DynamicQuestConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesTravelerMarkup()
    {
        var source = "Our thanks, {{Y|adventurer}}. Our village owes you a debt. For now, please choose a reward from our stockpile as payment for your service.";

        var translated = DynamicQuestConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y|冒険者}}よ、感謝する。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var source = MessageFrameTranslator.DirectTranslationMarker + "I'm looking for work.";

        var translated = DynamicQuestConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("I'm looking for work."));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("Tell me about your village.")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = DynamicQuestConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
