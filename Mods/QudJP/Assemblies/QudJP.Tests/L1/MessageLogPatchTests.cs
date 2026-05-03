using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MessageLogPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        MessageFrameTranslator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        Translator.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [Test]
    public void Prefix_ObservationOnly_DoesNotTranslateMessage()
    {
        var message = "You hit the bear.";
        MessageLogPatch.Prefix(ref message);
        Assert.That(message, Is.EqualTo("You hit the bear."));
    }

    [Test]
    public void Prefix_ObservationOnly_LogsUnclaimed()
    {
        var message = "You hit the bear.";
        var originalMessage = message;
        MessageLogPatch.Prefix(ref message);
        var hitCount = SinkObservation.GetHitCountForTests(
            nameof(MessageLogPatch), nameof(MessageLogPatch), SinkObservation.ObservationOnlyDetail, originalMessage, originalMessage);
        Assert.That(hitCount, Is.GreaterThan(0));
    }

    [Test]
    public void Prefix_DirectMarker_StillStripped()
    {
        var message = "\u0001すでに翻訳済みテキスト";
        var result = MessageLogPatch.Prefix(ref message);
        Assert.That(message, Is.EqualTo("すでに翻訳済みテキスト"));
        Assert.That(result, Is.True);
    }

    [Test]
    public void Prefix_DoesVerbMarker_TranslatesAndStripsHeader()
    {
        UseRepositoryDictionary();
        var fragment = "The 巨大トンボ begins";
        var subjectLength = "The 巨大トンボ".Length;
        var message = DoesVerbRouteTranslator.MarkDoesFragment(fragment, "begin", subjectLength, null) + " flying.";

        var result = MessageLogPatch.Prefix(ref message);

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.EqualTo("巨大トンボが飛翔し始めた。"));
            Assert.That(result, Is.True);
        });
    }

    [Test]
    public void Prefix_JournalNotification_TranslatesAndPreservesSourceColors()
    {
        UseSultanHistoryJournalPattern();
        var message = "&yYou note this piece of information in the &WSultan Histories > クホマスプ II&y section of your journal.";

        var result = MessageLogPatch.Prefix(ref message);

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.EqualTo("&yこの情報をジャーナルの「&Wスルタン史 > クホマスプ II&y」欄に記録した。"));
            Assert.That(result, Is.True);
        });
    }

    [Test]
    public void Prefix_JournalNotification_FallsBackToEnglish_WhenPatternMissing()
    {
        UseRepositoryJournalPatterns();
        var message = "&yYou note this piece of information in the &WUnregistered Lore > Missing&y section of your journal.";
        var original = message;

        var result = MessageLogPatch.Prefix(ref message);

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.EqualTo(original));
            Assert.That(result, Is.True);
        });
    }

    private static void UseRepositoryDictionary()
    {
        var repositoryDictionaryPath = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));

        MessageFrameTranslator.SetDictionaryPathForTests(repositoryDictionaryPath);

        var repositoryPatternPath = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries",
                "messages.ja.json"));

        MessagePatternTranslator.SetPatternFileForTests(repositoryPatternPath);
    }

    private static void UseRepositoryJournalPatterns()
    {
        var repositoryPatternPath = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries",
                "journal-patterns.ja.json"));

        JournalPatternTranslator.SetPatternFileForTests(repositoryPatternPath);
    }

    private static void UseSultanHistoryJournalPattern()
    {
        var patternPath = Path.Combine(Path.GetTempPath(), $"qudjp-journal-patterns-{Guid.NewGuid():N}.ja.json");
        File.WriteAllText(
            patternPath,
            """
            {
              "patterns": [
                {
                  "pattern": "^You note this piece of information in the Sultan Histories > (.+?) section of your journal\\.[.!]?$",
                  "template": "この情報をジャーナルの「スルタン史 > {0}」欄に記録した。"
                }
              ]
            }
            """);
        JournalPatternTranslator.SetPatternFileForTests(patternPath);
    }
}
