using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class DynamicQuestExplicitConversationTextTranslatorTests
{
    [TestCase("Yes. I will find the rusted relic as you ask.", "はい。頼まれたとおり錆びた遺物を探す。")]
    [TestCase("Yes. I will locate {{|the hidden archive}}&G as you ask.", "はい。頼まれたとおり{{|隠された文書庫}}を特定する。")]
    [TestCase("Yes. I will pray at {{|the salt shrine}} as you ask.", "はい。頼まれたとおり{{|塩の祠}}で祈る。")]
    [TestCase("No, I will not.", "いや、断る。")]
    [TestCase("I already know where {{|the hidden archive}} is.", "{{|隠された文書庫}}がどこにあるか既に知っている。")]
    [TestCase("I've found the rusted relic.", "錆びた遺物を見つけた。")]
    [TestCase("I don't have the rusted relic yet.", "まだ錆びた遺物を持っていない。")]
    [TestCase("I've located {{|the hidden archive}}.", "{{|隠された文書庫}}を特定した。")]
    [TestCase("I haven't located {{|the hidden archive}} yet.", "まだ{{|隠された文書庫}}を特定していない。")]
    [TestCase("I've desecrated {{|sacred vessel}}.", "{{|聖なる器}}を冒涜した。")]
    [TestCase("I haven't prayed at {{|the salt shrine}} yet.", "まだ{{|塩の祠}}で祈っていない。")]
    public void TryTranslate_TranslatesExplicitDynamicQuestConversationLines(string source, string expected)
    {
        var translated = DynamicQuestExplicitConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = DynamicQuestExplicitConversationTextTranslator.TryTranslate(
            "{{G|No, I will not.}}",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{G|いや、断る。}}"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = DynamicQuestExplicitConversationTextTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "No, I will not.",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("No, I will not."));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("I will do something unrelated.")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = DynamicQuestExplicitConversationTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
