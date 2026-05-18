using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class VillagePetConversationTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase("Why are there glowfish here?", "なぜここにグロウフィッシュがいるのだ？")]
    [TestCase("Why is there a glowfish here?", "なぜここにグロウフィッシュがいるのだ？")]
    [TestCase("Why's there an albino ape here?", "なぜここにアルビノ類人猿がいるのだ？")]
    public void TryTranslateQuestion_TranslatesPetQuestion(string source, string expected)
    {
        var translated = VillagePetConversationTranslator.TryTranslateQuestion(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateQuestion_PassesThroughUnknownEmptyColorTagsAndDirectMarker()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VillagePetConversationTranslator.TryTranslateQuestion(string.Empty, out var empty), Is.False);
            Assert.That(empty, Is.Empty);

            Assert.That(VillagePetConversationTranslator.TryTranslateQuestion("Why is this impossible?", out var unknown), Is.False);
            Assert.That(unknown, Is.EqualTo("Why is this impossible?"));

            Assert.That(VillagePetConversationTranslator.TryTranslateQuestion("{{Y|Why is this impossible?}}", out var colorTagged), Is.False);
            Assert.That(colorTagged, Is.EqualTo("{{Y|Why is this impossible?}}"));

            Assert.That(
                VillagePetConversationTranslator.TryTranslateQuestion(
                    MessageFrameTranslator.DirectTranslationMarker + "Why are there glowfish here?",
                    out var direct),
                Is.False);
            Assert.That(direct, Is.EqualTo("Why are there glowfish here?"));
        });
    }

    [TestCase("Nib just showed up one day and started singing.", "Nibはある日ふらりと現れ、歌い始めたんだ。")]
    [TestCase("They just showed up one day and started guarding the gate.", "ある日ふらりと現れ、門を守り始めたんだ。")]
    [TestCase("They've been here for as long as I remember, breaking bread.", "私が覚えている限りずっとここにいて、パンを分け合っている。")]
    [TestCase("Nib has been here for as long as I remember, barking.", "Nibは私が覚えている限りずっとここにいて、吠えている。")]
    [TestCase("Nib? Who knows.", "Nib？ 誰にわかるものか。")]
    [TestCase("Ask them yourself.", "直接聞いてみなさい。")]
    [TestCase("Perhaps they thought they could find gold here.", "おそらく、ここで金を見つけられると思ったのだろう。")]
    [TestCase("Oh, Nib? I assume because of their love of the wheel.", "ああ、Nibか。車輪への愛ゆえだと思う。")]
    [TestCase("Oh, {{Y|Nib}}? I assume because of their love of {{W|chrome}}.", "ああ、{{Y|Nib}}か。{{W|クロム}}への愛ゆえだと思う。")]
    public void TryTranslateAnswer_TranslatesOriginStoryFrames(string source, string expected)
    {
        var translated = VillagePetConversationTranslator.TryTranslateAnswer(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateAnswer_RestoresWholeSourceColorWrapper()
    {
        var translated = VillagePetConversationTranslator.TryTranslateAnswer("{{Y|Ask them yourself.}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y|直接聞いてみなさい。}}"));
        });
    }

    [Test]
    public void TryTranslateAnswer_PassesThroughUnknownAndDirectMarker()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VillagePetConversationTranslator.TryTranslateAnswer("They are complicated.", out var unknown), Is.False);
            Assert.That(unknown, Is.EqualTo("They are complicated."));

            Assert.That(VillagePetConversationTranslator.TryTranslateAnswer("They just showed up one day and started weaving baskets.", out var unknownActivity), Is.False);
            Assert.That(unknownActivity, Is.EqualTo("They just showed up one day and started weaving baskets."));

            Assert.That(
                VillagePetConversationTranslator.TryTranslateAnswer(
                    MessageFrameTranslator.DirectTranslationMarker + "Ask them yourself.",
                    out var direct),
                Is.False);
            Assert.That(direct, Is.EqualTo("Ask them yourself."));
        });
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");
}
