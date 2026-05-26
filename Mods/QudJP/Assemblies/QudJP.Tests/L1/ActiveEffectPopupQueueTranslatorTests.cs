using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ActiveEffectPopupQueueTranslatorTests
{
    [TestCase(
        "your rind softens while you recrystallize!",
        "あなたの外皮が柔らかくなり、あなたは再結晶化した！",
        "IrisdualCallowRindSoftens")]
    [TestCase(
        "the snapjaw's rind softens while it recrystallizes!",
        "snapjawの外皮が柔らかくなり、それは再結晶化した！",
        "IrisdualCallowRindSoftens")]
    [TestCase(
        "A trio of tongues vegetate from your face!",
        "3本の舌があなたの顔から生え出た！",
        "ThreeTonguesVegetate")]
    [TestCase(
        "A trio of tongues vegetate from the snapjaw's face!",
        "3本の舌がsnapjawの顔から生え出た！",
        "ThreeTonguesVegetate")]
    [TestCase(
        "you are no longer poisoned!",
        "あなたはもう毒を受けていない！",
        "NoLongerPoisonedFireEvent")]
    public void TryTranslateQueuedMessage_TranslatesCoveredActiveEffectQueueText(
        string source,
        string expected,
        string expectedDetail)
    {
        var translated = ActiveEffectPopupQueueTranslator.TryTranslateQueuedMessage(source, out var result, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(detail, Is.EqualTo(expectedDetail));
        });
    }

    [TestCase(
        "You cannot do that on the world map.",
        "ワールドマップではそれはできない。",
        "ShadeOilWorldMap")]
    [TestCase(
        "Shade oil has been applied by your injector. Do you wish to phase out immediately?",
        "シェードオイルがあなたのinjectorによって適用された。すぐに位相をずらす？",
        "ShadeOilPhasePrompt")]
    [TestCase(
        "You shake the water from your addled brain, but someone else's thoughts have already taken root.",
        "混乱した脳から水を振り払ったが、すでに誰か別の思考が根を下ろしている。",
        "BrainBrineCurseRootedThoughts")]
    [TestCase(
        "The clouds part in your mind and a ray of clarity strikes through.",
        "心の中で雲が割れ、明晰さの光が差し込む。",
        "SphynxSaltClarity")]
    public void TryTranslatePopupMessage_TranslatesCoveredActiveEffectPopupText(
        string source,
        string expected,
        string expectedDetail)
    {
        var translated = ActiveEffectPopupQueueTranslator.TryTranslatePopupMessage(source, out var result, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(detail, Is.EqualTo(expectedDetail));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_PreservesWholeSourceColorWrapper()
    {
        var translated = ActiveEffectPopupQueueTranslator.TryTranslatePopupMessage(
            "{{y|The clouds part in your mind and a ray of clarity strikes through.}}",
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{y|心の中で雲が割れ、明晰さの光が差し込む。}}"));
            Assert.That(detail, Is.EqualTo("SphynxSaltClarity"));
        });
    }

    [TestCase("")]
    [TestCase("unknown active effect popup")]
    [TestCase("\u0001You cannot do that on the world map.")]
    public void TryTranslatePopupMessage_LeavesUnsupportedAndMarkedTextUnchanged(string source)
    {
        var translated = ActiveEffectPopupQueueTranslator.TryTranslatePopupMessage(source, out var result, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
            Assert.That(detail, Is.Empty);
        });
    }

    [TestCase("")]
    [TestCase("unknown queued message")]
    [TestCase("\u0001your rind softens while you recrystallize!")]
    public void TryTranslateQueuedMessage_LeavesUnsupportedAndMarkedTextUnchanged(string source)
    {
        var translated = ActiveEffectPopupQueueTranslator.TryTranslateQueuedMessage(source, out var result, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
            Assert.That(detail, Is.Empty);
        });
    }

    [Test]
    public void TryTranslateQueuedMessage_ColorWrappedKnownTextIsTranslated()
    {
        var translated = ActiveEffectPopupQueueTranslator.TryTranslateQueuedMessage(
            "{{W|you are no longer poisoned!}}",
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{W|あなたはもう毒を受けていない！}}"));
            Assert.That(detail, Is.EqualTo("NoLongerPoisonedFireEvent"));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_MarkerPrefixedKnownTextReturnsFalseWithUnchangedSource()
    {
        const string source = "\u0001You shake the water from your addled brain, but someone else's thoughts have already taken root.";
        var translated = ActiveEffectPopupQueueTranslator.TryTranslatePopupMessage(source, out var result, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
            Assert.That(detail, Is.Empty);
        });
    }
}
