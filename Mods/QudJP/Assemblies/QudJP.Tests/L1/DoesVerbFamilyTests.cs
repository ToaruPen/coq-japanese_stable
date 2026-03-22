namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DoesVerbFamilyTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-does-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");
        dictionaryDirectory = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));

        File.Copy(
            Path.Combine(dictionaryDirectory, "messages.ja.json"),
            patternFilePath);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        MessagePatternTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    // --- Status Predicate Family ---

    // Plain text (runtime-like already-localized display names)
    [TestCase("The 熊 is exhausted!", "熊は疲弊した！")]
    [TestCase("The スナップジョー is stunned!", "スナップジョーは気絶した！")]
    [TestCase("The 熊 is stuck.", "熊は動けなくなった。")]
    [TestCase("The グロウパッド is sealed.", "グロウパッドは封印された。")]
    [TestCase("You are exhausted!", "あなたは疲弊した！")]
    // Color-wrapped (AddPlayerMessage wraps entire message in {{color|...}})
    [TestCase("{{g|The 熊 is exhausted!}}", "{{g|熊は疲弊した！}}")]
    [TestCase("{{R|The スナップジョー is stunned!}}", "{{R|スナップジョーは気絶した！}}")]
    public void Translate_StatusPredicateFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Negation/Lack Family ---

    // Plain text
    [TestCase("The タレット can't hear you!", "タレットにはあなたの声が聞こえない！")]
    [TestCase("The 熊 doesn't have a consciousness to appeal to.", "熊には訴えるべき意識がない。")]
    [TestCase("You don't penetrate the スナップジョー's armor!", "スナップジョーの防具を貫通できなかった！")]
    [TestCase("You can't see!", "視界がない！")]
    [TestCase("The タレット doesn't have enough charge to fire.", "タレットはfireするのに十分なchargeがない。")]
    // Color-wrapped (ConsequentialColor wraps full message)
    [TestCase("{{r|You don't penetrate the スナップジョー's armor!}}", "{{r|スナップジョーの防具を貫通できなかった！}}")]
    public void Translate_NegationLackFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Combat Damage Family (third-person actor) ---

    // Plain text
    [TestCase("The 熊 hits the スナップジョー for 5 damage!", "熊はスナップジョーに5ダメージを与えた！")]
    [TestCase("The 熊 misses the スナップジョー!", "熊はスナップジョーへの攻撃を外した！")]
    [TestCase("The 熊 hits the スナップジョー with a 青銅の短剣 for 3 damage.", "熊は青銅の短剣でスナップジョーに3ダメージを与えた。")]
    [TestCase("The 熊 misses the スナップジョー with a 青銅の短剣! [8 vs 12]", "熊は青銅の短剣でスナップジョーへの攻撃を外した！ [8 vs 12]")]
    // Penetration color (Stat.GetResultColor wraps full message in ampersand format)
    // AddPlayerMessage converts ampersand to brace: &W → {{W|...}}
    [TestCase("{{W|The 熊 hits the スナップジョー for 5 damage!}}", "{{W|熊はスナップジョーに5ダメージを与えた！}}")]
    [TestCase("{{r|The 熊 misses the スナップジョー!}}", "{{r|熊はスナップジョーへの攻撃を外した！}}")]
    public void Translate_CombatDamageThirdPerson(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Possession/Lack Family ---

    // Plain text
    [TestCase("The 水筒 has no room for more water.", "水筒にはこれ以上の水を入れる余地がない。")]
    [TestCase("You have no more ammo!", "弾薬が尽きた！")]
    [TestCase("The 熊 has nothing to say.", "熊は何も言うことがない。")]
    [TestCase("You have left your party.", "あなたはパーティーを離れた。")]
    // Color-wrapped
    [TestCase("{{y|You have no more ammo!}}", "{{y|弾薬が尽きた！}}")]
    public void Translate_PossessionLackFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Motion/Direction Family ---

    // Plain text
    [TestCase("The 熊 falls to the ground.", "熊は地面に倒れた。")]
    [TestCase("You fall to the ground.", "あなたは地面に倒れた。")]
    [TestCase("The スナップジョー falls asleep.", "スナップジョーは眠りに落ちた。")]
    // Color-wrapped (ConsequentialColor)
    [TestCase("{{g|The 熊 falls to the ground.}}", "{{g|熊は地面に倒れた。}}")]
    public void Translate_MotionDirectionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Edge cases (required for all families) ---

    [Test]
    public void Translate_ReturnsOriginal_WhenNoPatternMatches()
    {
        // Unrecognized message should pass through unchanged
        var input = "The bear performs an unknowable act.";
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(input));
    }

    [Test]
    public void Translate_ReturnsEmpty_WhenInputIsEmpty()
    {
        var result = MessagePatternTranslator.Translate(string.Empty);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_ReturnsEmpty_WhenInputIsNull()
    {
        var result = MessagePatternTranslator.Translate(null);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_SkipsTranslation_WhenDirectTranslationMarkerPresent()
    {
        // \x01 marker means already translated — should pass through unchanged
        var input = "\u0001熊は疲弊した！";
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(input));
    }

    private static void AssertTranslated(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(expected));
    }
}
