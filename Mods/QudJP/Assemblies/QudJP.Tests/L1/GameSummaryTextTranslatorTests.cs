using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GameSummaryTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-game-summary-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateCause_UsesExistingExactEndingDictionaryEntry()
    {
        WriteDictionary(("You abandoned all hope.", "あなたはすべての希望を捨てた。"));

        var translated = GameSummaryTextTranslator.TranslateCause("You abandoned all hope.");

        Assert.That(translated, Is.EqualTo("あなたはすべての希望を捨てた。"));
    }

    [Test]
    public void TranslateDetails_TranslatesRuntimeSummaryTemplates()
    {
        WriteDictionary(
            ("Game summary for {0}", "{0}のゲームサマリー"),
            ("This game ended on {0} at {1}.", "このゲームは{0} {1}に終了した。"),
            ("You were level {0}.", "レベルは{0}だった。"),
            ("You scored {0} points.", "{0}点を獲得した。"),
            ("You survived for {0} turns.", "{0}ターン生存した。"),
            ("You found {0} lairs.", "{0}個の巣を発見した。"),
            ("You named {0} items.", "{0}個のアイテムに名前を付けた。"),
            ("You generated {0} storied items.", "{0}個の伝説的アイテムを生成した。"),
            ("The most advanced artifact in your possession was {0}.", "所持品の中でもっとも高度なアーティファクトは{0}だった。"),
            ("This game was played in {0} mode.", "このゲームは{0}モードでプレイされた。"),
            ("Chronology for {0}", "{0}の年代記"),
            ("Final messages for {0}", "{0}の最終メッセージ"),
            ("Classic", "クラシック"),
            ("phase cannon", "位相砲"));
        var source = string.Join(
            "\n",
            "{{C|*}} Game summary for {{W|Qudman}} {{C|*}}",
            "",
            "This game ended on Monday, 04 May 2026 at 00:55:34.",
            "You were level {{C|1}}.",
            "You scored {{C|10539}} points.",
            "You survived for {{C|477}} turns.",
            "You found {{C|2}} lairs.",
            "You named {{C|3}} items.",
            "You generated {{C|4}} storied items.",
            "The most advanced artifact in your possession was {{Y|phase cannon}}.",
            "This game was played in Classic mode.",
            "",
            "{{C|*}} Chronology for {{W|Qudman}} {{C|*}}",
            "",
            "{{C|*}} Final messages for {{W|Qudman}} {{C|*}}");

        var translated = GameSummaryTextTranslator.TranslateDetails(source);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("{{W|Qudman}}のゲームサマリー"));
            Assert.That(translated, Does.Contain("このゲームはMonday, 04 May 2026 00:55:34に終了した。"));
            Assert.That(translated, Does.Contain("レベルは{{C|1}}だった。"));
            Assert.That(translated, Does.Contain("{{C|10539}}点を獲得した。"));
            Assert.That(translated, Does.Contain("{{C|477}}ターン生存した。"));
            Assert.That(translated, Does.Contain("{{C|2}}個の巣を発見した。"));
            Assert.That(translated, Does.Contain("{{C|3}}個のアイテムに名前を付けた。"));
            Assert.That(translated, Does.Contain("{{C|4}}個の伝説的アイテムを生成した。"));
            Assert.That(translated, Does.Contain("所持品の中でもっとも高度なアーティファクトは{{Y|位相砲}}だった。"));
            Assert.That(translated, Does.Contain("このゲームはクラシックモードでプレイされた。"));
            Assert.That(translated, Does.Contain("{{W|Qudman}}の年代記"));
            Assert.That(translated, Does.Contain("{{W|Qudman}}の最終メッセージ"));
        });
    }

    [Test]
    public void TranslateDetails_TranslatesNegativeScoreLine()
    {
        WriteDictionary(("You scored {0} points.", "{0}点を獲得した。"));

        var translated = GameSummaryTextTranslator.TranslateDetails("You scored {{C|-352}} points.");

        Assert.That(translated, Is.EqualTo("{{C|-352}}点を獲得した。"));
    }

    [Test]
    public void TranslateDetails_PreservesTemplateLine_WhenDictionaryEntryIsMissing()
    {
        WriteDictionary(("Game summary for {0}", "{0}のゲームサマリー"));

        var translated = GameSummaryTextTranslator.TranslateDetails(
            "{{C|*}} Game summary for {{W|Qudman}} {{C|*}}\nYou scored {{C|10539}} points.");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("{{W|Qudman}}のゲームサマリー"));
            Assert.That(translated, Does.Contain("You scored {{C|10539}} points."));
        });
    }

    [Test]
    public void TranslateCauseAndDetails_PreserveEmptyAndMarkerPrefixedInputs()
    {
        WriteDictionary(("You abandoned all hope.", "あなたはすべての希望を捨てた。"));

        Assert.Multiple(() =>
        {
            Assert.That(GameSummaryTextTranslator.TranslateCause(null), Is.EqualTo(string.Empty));
            Assert.That(GameSummaryTextTranslator.TranslateCause(string.Empty), Is.EqualTo(string.Empty));
            Assert.That(GameSummaryTextTranslator.TranslateDetails(null), Is.EqualTo(string.Empty));
            Assert.That(
                GameSummaryTextTranslator.TranslateCause("\u0001You abandoned all hope."),
                Is.EqualTo("\u0001You abandoned all hope."));
            Assert.That(GameSummaryTextTranslator.TranslateDetails(string.Empty), Is.EqualTo(string.Empty));
            Assert.That(
                GameSummaryTextTranslator.TranslateDetails("\u0001You scored {{C|10539}} points."),
                Is.EqualTo("\u0001You scored {{C|10539}} points."));
        });
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(Path.Combine(tempDirectory, "game-summary-l1.ja.json"), builder.ToString(), Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
