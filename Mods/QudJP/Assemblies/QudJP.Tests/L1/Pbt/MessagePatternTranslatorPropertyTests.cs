using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class MessagePatternTranslatorPropertyTests
{
    private const string ReplaySeed = "975318642,24681";

    [SetUp]
    public void SetUp()
    {
        MessagePatternTranslator.ResetForTests();
        UseRepositoryPatternDictionary();
    }

    [TearDown]
    public void TearDown()
    {
        MessagePatternTranslator.ResetForTests();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(MessagePatternTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty Translate_PreservesHitWithRollWrappers(HitWithRollPatternCase sample)
    {
        var translated = MessagePatternTranslator.Translate(sample.Source);

        Assert.That(translated, Is.EqualTo(sample.ExpectedTranslated));

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(MessagePatternTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty Translate_PreservesWeaponMissWrappers(WeaponMissPatternCase sample)
    {
        var translated = MessagePatternTranslator.Translate(sample.Source);

        Assert.That(translated, Is.EqualTo(sample.ExpectedTranslated));

        return true.ToProperty();
    }

    private static void UseRepositoryPatternDictionary()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var repositoryPatternFile = Path.Combine(root, "Mods", "QudJP", "Localization", "Dictionaries", "messages.ja.json");
        MessagePatternTranslator.SetPatternFileForTests(repositoryPatternFile);
    }
}
