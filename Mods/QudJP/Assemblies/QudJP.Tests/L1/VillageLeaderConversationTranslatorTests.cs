using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class VillageLeaderConversationTranslatorTests
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

    [TestCase("Watch yourself, adventurer.", "冒険者よ、身の振り方には気をつけろ。")]
    [TestCase("Live and drink, friend.", "生きて飲め、友。")]
    [TestCase("Stay out of trouble, wanderer.", "面倒は起こすな、放浪者。")]
    [TestCase("I'm watching you, nomad.", "見張っているぞ、遊牧民。")]
    public void TryTranslate_TranslatesWardenFrames(string source, string expected)
    {
        var translated = VillageLeaderConversationTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_TranslatesMayorTreasureWelcome()
    {
        const string source =
            "Welcome to the village of Kyakukya, traveler. Here you will find shade and vittle, along with other provisions to help you better scour the rust-caves for treasure. Above all else, you may drink of our freshwater and quench your thirst.";

        var translated = VillageLeaderConversationTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("旅人よ、Kyakukyaの村へようこそ。ここには日陰と食べ物があり、錆の洞窟で宝を探す助けになる備えもある。何よりも、われらの真水を飲み、渇きを癒してよい。"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesMayorSacredProfaneWelcome()
    {
        const string source =
            "friend, welcome to the village of Joppa. We are a clan who cherish the wheel and abhor chrome. Per our custom, you may drink of our freshwater and quench your thirst.";

        var translated = VillageLeaderConversationTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("友よ、Joppaの村へようこそ。われらは車輪を大切にし、クロムを忌む氏族だ。われらの習わしにより、われらの真水を飲み、渇きを癒してよい。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesMarkupOnMayorSacredProfaneCaptures()
    {
        const string source =
            "friend, welcome to the village of Joppa. We are a clan who cherish {{C|the wheel}} and abhor {{R|chrome}}. Per our custom, you may drink of our freshwater and quench your thirst.";

        var translated = VillageLeaderConversationTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("友よ、Joppaの村へようこそ。われらは{{C|車輪}}を大切にし、{{R|クロム}}を忌む氏族だ。われらの習わしにより、われらの真水を飲み、渇きを癒してよい。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = VillageLeaderConversationTranslator.TryTranslate("{{G|Live and drink, friend.}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{G|生きて飲め、友。}}"));
        });
    }

    [Test]
    public void TryTranslate_PassesThroughUnknownAndDirectMarker()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VillageLeaderConversationTranslator.TryTranslate(string.Empty, out var empty), Is.False);
            Assert.That(empty, Is.Empty);

            Assert.That(VillageLeaderConversationTranslator.TryTranslate("Welcome, friend.", out var unknown), Is.False);
            Assert.That(unknown, Is.EqualTo("Welcome, friend."));

            Assert.That(
                VillageLeaderConversationTranslator.TryTranslate(
                    MessageFrameTranslator.DirectTranslationMarker + "Live and drink, friend.",
                    out var direct),
                Is.False);
            Assert.That(direct, Is.EqualTo("Live and drink, friend."));
        });
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");
}
