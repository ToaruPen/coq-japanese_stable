using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class BedChairFragmentTranslatorTests
{
    [TestCase("You cannot sleep on ベッド.", "ベッドの上で眠れない。")]
    [TestCase("上で眠れない: ベッド.", "ベッドの上で眠れない。")]
    [TestCase("You cannot reach ベッド.", "ベッドに手が届かない。")]
    [TestCase("手が届かない: ベッド.", "ベッドに手が届かない。")]
    [TestCase("You are out of phase with ベッド.", "ベッドと位相がずれている。")]
    [TestCase("あなたは と位相がずれている。ベッド.", "ベッドと位相がずれている。")]
    [TestCase("{{r|上で眠れない: ベッド.}}", "{{r|ベッドの上で眠れない。}}")]
    [TestCase("You think you broke ベッド...", "ベッドを壊してしまった気がする。")]
    [TestCase(" を壊してしまった気がする。ベッド...", "ベッドを壊してしまった気がする。")]
    [TestCase("You cannot reach {{r|ベッド}}.", "{{r|ベッドに手が届かない。}}")]
    public void TryTranslateBedMessage_TranslatesProducerFragments(string input, string expected)
    {
        var ok = BedChairFragmentTranslator.TryTranslateBedMessage(
            input,
            nameof(BedChairFragmentTranslatorTests),
            "Bed",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("This is a random message.")]
    [TestCase("Some other text.")]
    [TestCase("You cannot sleep")]
    [TestCase("\x01")]
    [TestCase("test\x01bed")]
    [TestCase("{{r|completely unrelated message}}")]
    public void TryTranslateBedMessage_ReturnsFalseForNonMatching(string input)
    {
        var ok = BedChairFragmentTranslator.TryTranslateBedMessage(
            input,
            nameof(BedChairFragmentTranslatorTests),
            "Bed",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(input));
        });
    }

    [TestCase("You cannot sit on 椅子.", "椅子に座れない。")]
    [TestCase("座れない: 椅子.", "椅子に座れない。")]
    [TestCase("You cannot reach 椅子.", "椅子に手が届かない。")]
    [TestCase("手が届かない: 椅子.", "椅子に手が届かない。")]
    [TestCase("You are out of phase with 椅子.", "椅子と位相がずれている。")]
    [TestCase("あなたは と位相がずれている。椅子.", "椅子と位相がずれている。")]
    [TestCase("You cannot unequip 椅子.", "椅子を外せない。")]
    [TestCase(" を外せない。椅子.", "椅子を外せない。")]
    [TestCase("You cannot set 椅子 down!", "椅子を置けない！")]
    [TestCase(" を設定できない。椅子 down!", "椅子を置けない！")]
    [TestCase("You think you broke 椅子...", "椅子を壊してしまった気がする。")]
    [TestCase(" を壊してしまった気がする。椅子...", "椅子を壊してしまった気がする。")]
    [TestCase("You cannot set {{r|椅子}} down!", "{{r|椅子を置けない！}}")]
    public void TryTranslateChairMessage_TranslatesProducerFragments(string input, string expected)
    {
        var ok = BedChairFragmentTranslator.TryTranslateChairMessage(
            input,
            nameof(BedChairFragmentTranslatorTests),
            "Chair",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("This is a random message.")]
    [TestCase("Some other text.")]
    [TestCase("You cannot sit")]
    [TestCase("\x01")]
    [TestCase("test\x01chair")]
    [TestCase("{{r|completely unrelated message}}")]
    public void TryTranslateChairMessage_ReturnsFalseForNonMatching(string input)
    {
        var ok = BedChairFragmentTranslator.TryTranslateChairMessage(
            input,
            nameof(BedChairFragmentTranslatorTests),
            "Chair",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(input));
        });
    }
}