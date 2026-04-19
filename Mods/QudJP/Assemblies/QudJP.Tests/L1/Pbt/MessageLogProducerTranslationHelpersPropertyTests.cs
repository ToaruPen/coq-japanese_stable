using FsCheck.Fluent;
using QudJP.Patches;

using FsCheckProperty = FsCheck.Property;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class MessageLogProducerTranslationHelpersPropertyTests
{
    private const string ReplaySeed = "246813579,97531";

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-message-log-producer-pbt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);

        var root = TestProjectPaths.GetRepositoryRoot();
        var repositoryPatternFile = Path.Combine(root, "Mods", "QudJP", "Localization", "Dictionaries", "messages.ja.json");
        File.Copy(repositoryPatternFile, patternFilePath, overwrite: true);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(MessageLogProducerTranslationHelpersArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryPreparePatternMessage_StripsControlHeaderAndPreservesNestedWrappers(CombatPatternCase sample)
    {
        var source = sample.Source;

        var translated = MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
            ref source,
            nameof(GameObjectEmitMessageTranslationPatch),
            "EmitMessage",
            markJapaneseAsDirect: true);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(source, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(MessageLogProducerTranslationHelpersArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryPreparePatternMessage_LeavesDirectMarkedMessagesUntouched(DirectMarkedCase sample)
    {
        var source = sample.Source;

        var translated = MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
            ref source,
            nameof(GameObjectEmitMessageTranslationPatch),
            "EmitMessage",
            markJapaneseAsDirect: true);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(source, Is.EqualTo(sample.Source));
        });

        return true.ToProperty();
    }
}
