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
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        MessageFrameTranslator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
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
    public void Prefix_ControlHeaderHasNothingToTrade_UsesRepositoryPatternAndStripsHeader()
    {
        UseRepositoryDictionary();
        var message = "\u0002have\u001F14\u001F18\u001F\u0003The スナップジョーの軍主 has nothing to trade.";

        var result = MessageLogPatch.Prefix(ref message);

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.EqualTo("スナップジョーの軍主には取引するものがない"));
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

    [Test]
    public void Prefix_TranslatesWaterRitualStartAndPreservesColors()
    {
        var message = "&yYou share your &Bwater&y with 監視官イラメ and begin the water ritual.";

        var result = MessageLogPatch.Prefix(ref message);

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.EqualTo("&y監視官イラメと&B水&yを分かち合い、水の儀式を始めた。"));
            Assert.That(result, Is.True);
        });
    }

    [TestCase(
        "&yYour reputation with &Cthe 監視官同胞団&y increased by &G100&y to &C-50&y.",
        "&y&C監視官同胞団&yとの評判が&G100&y増加し、&C-50&yになった。")]
    [TestCase(
        "&yBecause they admire 監視官イラメ, your reputation with the ジョッパの村人たち increased by &G100&y to &C-40&y.",
        "&y監視官イラメを尊敬しているため、ジョッパの村人たちとの評判が&G100&y増加し、&C-40&yになった。")]
    [TestCase(
        "&yBecause they dislike 監視官イラメ, your reputation with the villagers of テガニプ decreased by &R50&y to &C-50&y.",
        "&y監視官イラメをよく思っていないため、テガニプの村人たちとの評判が&R50&y減少し、&C-50&yになった。")]
    public void Prefix_TranslatesWaterRitualReputationMessagesAndPreservesColors(string source, string expected)
    {
        var message = source;

        var result = MessageLogPatch.Prefix(ref message);

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.EqualTo(expected));
            Assert.That(result, Is.True);
        });
    }

    [Test]
    public void Prefix_TranslatesCampfirePreserveMessageLogFrameAndPreservesColors()
    {
        Translator.SetDictionaryDirectoryForTests(
            Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries"));
        const string source =
            "&yYou preserved:\n\n"
            + "Some &r生の猪肉&y into 3 serving of 猪肉ジャーキー.\n"
            + "Some &r生のワーム肉&y into 3 serving of ワームジャーキー.";
        var message = source;

        var result = MessageLogPatch.Prefix(ref message);

        Assert.Multiple(() =>
        {
            Assert.That(
                message,
                Is.EqualTo(
                    "&y保存した:\n\n"
                    + "&r生の猪肉少々&yを3食分の猪肉ジャーキーに保存した。\n"
                    + "&r生のワーム肉少々&yを3食分のワームジャーキーに保存した。"));
            Assert.That(result, Is.True);
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(MessageLogPatch),
                    "MessageLog.CampfirePreserve.CampfirePreserveTranslationPatch"),
                Is.GreaterThan(0));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(MessageLogPatch),
                    nameof(MessageLogPatch),
                    SinkObservation.ObservationOnlyDetail,
                    source,
                    "You preserved:\n\n"
                    + "Some 生の猪肉 into 3 serving of 猪肉ジャーキー.\n"
                    + "Some 生のワーム肉 into 3 serving of ワームジャーキー."),
                Is.EqualTo(0));
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
