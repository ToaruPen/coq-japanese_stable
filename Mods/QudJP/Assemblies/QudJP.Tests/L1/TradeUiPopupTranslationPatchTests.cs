using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class TradeUiPopupTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-trade-ui-popup-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        DynamicTextObservability.ResetForTests();

        WritePatternDictionary();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "You cannot carry things.",
        "{0} cannot carry things.",
        "{0}は物を運べない。",
        "あなたは物を運べない。")]
    [TestCase(
        "商人 cannot carry things.",
        "{0} cannot carry things.",
        "{0}は物を運べない。",
        "商人は物を運べない。")]
    [TestCase(
        "商人 is engaged in melee combat and is too busy to trade with you.",
        "{0} engaged in melee combat and is too busy to trade with you.",
        "{0}は近接戦闘中で、あなたと取引している暇がない。",
        "商人は近接戦闘中で、あなたと取引している暇がない。")]
    [TestCase(
        "商人 is on fire and is too busy to trade with you.",
        "{0} on fire and is too busy to trade with you.",
        "{0}は燃えていて、あなたと取引している暇がない。",
        "商人は燃えていて、あなたと取引している暇がない。")]
    [TestCase(
        "商人 will not trade with you until you pay 彼 the 5 drams of fresh water you owe 彼.",
        "{0} will not trade with you until you pay {1} the {2} you owe {3}.",
        "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。",
        "商人は、あなたが彼に借りている5ドラムの{{B|清水}}を支払うまで取引してくれない。")]
    [TestCase(
        "商人 will not trade with you until you pay 彼 the 5 drams of fresh water you owe 彼. Do you want to give 彼 your 3 drams now?",
        "{0} will not trade with you until you pay {1} the {2} you owe {3}. Do you want to give {4} your {5} now?",
        "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。今すぐあなたの{5}を{4}に渡しますか？",
        "商人は、あなたが彼に借りている5ドラムの{{B|清水}}を支払うまで取引してくれない。今すぐあなたの3ドラムを彼に渡しますか？")]
    [TestCase(
        "商人 will not trade with you until you pay 彼 the 5 drams of fresh water you owe 彼. Do you want to give it to 彼 now?",
        "{0} will not trade with you until you pay {1} the {2} you owe {3}. Do you want to give it to {4} now?",
        "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。今すぐそれを{4}に渡しますか？",
        "商人は、あなたが彼に借りている5ドラムの{{B|清水}}を支払うまで取引してくれない。今すぐそれを彼に渡しますか？")]
    [TestCase(
        "You can't understand 商人の explanation.",
        "You can't understand {0} explanation.",
        "{0}説明は理解できない。",
        "商人の説明は理解できない。")]
    [TestCase(
        "This item is too complex for 商人 to identify.",
        "This item is too complex for {0} to identify.",
        "この品は{0}には複雑すぎて鑑定できない。",
        "この品は商人には複雑すぎて鑑定できない。")]
    [TestCase(
        "You do not have the required 7 drams to identify this item.",
        "You do not have the required {0} to identify this item.",
        "この品を鑑定するのに必要な{0}が足りない。",
        "この品を鑑定するのに必要な7ドラムが足りない。")]
    [TestCase(
        "You may identify this for 7 drams of fresh water.",
        "You may identify this for {0}.",
        "これを{0}で鑑定できる。",
        "これを7ドラムの清水で鑑定できる。")]
    [TestCase(
        "merchant identifies laser pistol as a laser pistol.",
        "{0} identifies {1} as {2}.",
        "{0}は{1}を{2}だと鑑定した。",
        "merchantはlaser pistolをa laser pistolだと鑑定した。")]
    [TestCase(
        "These items are too complex for 商人 to repair.",
        "{0} are too complex for {1} to repair.",
        "{0}は{1}には複雑すぎて修理できない。",
        "これらの品は商人には複雑すぎて修理できない。")]
    [TestCase(
        "You need 8 drams of fresh water to repair those.",
        "You need {0} to repair {1}.",
        "{1}を修理するには{0}が必要だ。",
        "それらを修理するには8ドラムの清水が必要だ。")]
    [TestCase(
        "You may repair this for 8 drams of fresh water.",
        "You may repair {0} for {1}.",
        "{0}を{1}で修理できる。",
        "これを8ドラムの清水で修理できる。")]
    [TestCase(
        "You'll have to pony up 10 drams of fresh water to even up the trade. Agreed?",
        "You'll have to pony up {0} to even up the trade. Agreed?",
        "取引を釣り合わせるには{0}を支払う必要がある。承諾する？",
        "取引を釣り合わせるには10ドラムの清水を支払う必要がある。承諾する？")]
    [TestCase(
        "You don't have 10 drams of fresh water to even up the trade!",
        "You don't have {0} to even up the trade!",
        "取引を釣り合わせるための{0}が足りない！",
        "取引を釣り合わせるための10ドラムの清水が足りない！")]
    [TestCase(
        "You pony up 3 drams of fresh water, and now owe 商人 7 drams.",
        "You pony up {0}, and now owe {1} {2}.",
        "あなたは{0}を支払い、今や{1}に{2}借りている。",
        "あなたは3ドラムの清水を支払い、今や商人に7ドラム借りている。")]
    [TestCase(
        "You now owe 商人 7 drams of fresh water.",
        "You now owe {0} {1}.",
        "今や{0}に{1}借りている。",
        "今や商人に7ドラムの清水借りている。")]
    [TestCase(
        "商人 will have to pony up 12 drams of fresh water to even up the trade. Agreed?",
        "{0} will have to pony up {1} to even up the trade. Agreed?",
        "{0}は取引を釣り合わせるために{1}を支払う必要がある。承諾する？",
        "商人は取引を釣り合わせるために12ドラムの清水を支払う必要がある。承諾する？")]
    [TestCase(
        "商人 don't have 12 drams of fresh water to even up the trade! Do you want to complete the trade anyway?",
        "{0} don't have {1} to even up the trade! Do you want to complete the trade anyway?",
        "{0}には取引を釣り合わせるための{1}がない！ それでも取引を成立させる？",
        "商人には取引を釣り合わせるための12ドラムの清水がない！ それでも取引を成立させる？")]
    [TestCase(
        "You don't have enough water containers to carry that many drams! You can store 6 drams.",
        "You don't have enough water containers to carry that many drams! You can store {0}.",
        "そんな量のドラムを運ぶだけの水容器が足りない！ 保管できるのは{0}までだ。",
        "そんな量のドラムを運ぶだけの水容器が足りない！ 保管できるのは6ドラムまでだ。")]
    [TestCase(
        "You don't have enough water containers to carry that many drams! Do you want to complete the trade for the 6 drams you can store?",
        "You don't have enough water containers to carry that many drams! Do you want to complete the trade for the {0} you can store?",
        "そんな量のドラムを運ぶだけの水容器が足りない！ 保管できる{0}分だけで取引を成立させる？",
        "そんな量のドラムを運ぶだけの水容器が足りない！ 保管できる6ドラム分だけで取引を成立させる？")]
    [TestCase(
        "You need 4 drams of fresh water to charge one of those.",
        "You need {0} to charge {1}.",
        "{1}を充電するには{0}が必要だ。",
        "そのうちの1つを充電するには4ドラムの清水が必要だ。")]
    [TestCase(
        "You may recharge 変圧器 for 4 drams of fresh water.",
        "You may recharge {0} for {1}.",
        "{0}を{1}で充電できる。",
        "変圧器を4ドラムの清水で充電できる。")]
    [TestCase(
        "Trade could not be completed, you couldn't drop object: laser rifle",
        "Trade could not be completed, {0} couldn't drop object: {1}",
        "取引を完了できなかった。{0}は{1}を落とせなかった。",
        "取引を完了できなかった。あなたはlaser rifleを落とせなかった。")]
    [TestCase(
        "As a result, the trade costs you 5 drams rather than 7.",
        "As a result, the trade costs you {0} rather than {1}.",
        "その結果、取引費用は{1}ではなく{0}になった。",
        "その結果、取引費用は7ドラムではなく5ドラムになった。")]
    [TestCase(
        "As a result, the trade is worth 5 drams rather than 7.",
        "As a result, the trade is worth {0} rather than {1}.",
        "その結果、その取引の価値は{1}ではなく{0}になった。",
        "その結果、その取引の価値は7ドラムではなく5ドラムになった。")]
    [TestCase(
        "As a result, the trade goes from costing you 7 drams to being worth 5.",
        "As a result, the trade goes from costing you {0} to being worth {1}.",
        "その結果、その取引は{0}の支払いから{1}の得になる形へ変わった。",
        "その結果、その取引は7ドラムの支払いから5ドラムの得になる形へ変わった。")]
    [TestCase(
        "As a result, the trade goes from being worth 7 drams to being worth 5.",
        "As a result, the trade goes from being worth {0} to being worth {1}.",
        "その結果、その取引は{0}の得から{1}の得へ変わった。",
        "その結果、その取引は7ドラムの得から5ドラムの得へ変わった。")]
    public void TranslatePopupText_TranslatesTradeUiTemplate(string source, string key, string text, string expected)
    {
        WriteDictionary((key, text));

        var translated = TradeUiPopupTranslationPatch.TranslatePopupText(source);

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePopupText_PreservesColorTagsInsideAndOutsideTemplate()
    {
        WriteDictionary(
            ("You need {0} to repair {1}.", "{1}を修理するには{0}が必要だ。"));

        var translated = TradeUiPopupTranslationPatch.TranslatePopupText(
            "{{R|You need {{C|8}} drams of fresh water to repair those.}}");

        Assert.That(translated, Is.EqualTo("{{R|それらを修理するには{{C|8}}ドラムの清水が必要だ。}}"));
    }

    [Test]
    public void TranslatePopupText_UsesPopupExactFallback_ForStaticTradeLine()
    {
        WriteDictionary(
            ("In the end, though, it makes no difference.", "結局のところ、何も変わらなかった。"));

        var translated = TradeUiPopupTranslationPatch.TranslatePopupText(
            "In the end, though, it makes no difference.");

        Assert.That(translated, Is.EqualTo("結局のところ、何も変わらなかった。"));
    }

    [Test]
    public void TranslatePopupText_UsesMessagePatternFallback_ForSharedVerbFamily()
    {
        WritePatternDictionary(
            ("^(?:The |the |[Aa]n? )?(.+?) (?:is|are) fully charged!$", "{0}は完全に充電された！"));

        var translated = TradeUiPopupTranslationPatch.TranslatePopupText("The 変圧器 is fully charged!");

        Assert.That(translated, Is.EqualTo("変圧器は完全に充電された！"));
    }

    [Test]
    public void TranslatePopupText_LeavesUnknownTextUnchanged()
    {
        const string source = "This popup does not belong to trade UI.";

        var translated = TradeUiPopupTranslationPatch.TranslatePopupText(source);

        Assert.That(translated, Is.EqualTo(source));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");

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

        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "trade-ui-popup-tests.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"patterns\":[");

        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
