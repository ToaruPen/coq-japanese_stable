using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
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
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-trade-ui-popup-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        DynamicTextObservability.ResetForTests();
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", Utf8WithoutBom);
        DummyTradeUiPopupTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyTradeUiPopupTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesShowMessage_ForWaterDebt()
    {
        WriteDictionary(
            ("{0} will not trade with you until you pay {1} the {2} you owe {3}.", "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("商人 will not trade with you until you pay 彼 the 5 drams of fresh water you owe 彼.");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("商人は、あなたが彼に借りている5ドラムの{{B|清水}}を支払うまで取引してくれない。"));
    }

    [Test]
    public void Prefix_TranslatesShowYesNo_ForTradeQuestion()
    {
        WriteDictionary(
            ("You'll have to pony up {0} to even up the trade. Agreed?", "取引を釣り合わせるには{0}を支払う必要がある。承諾する？"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.ShowYesNo));

        _ = DummyTradeUiPopupTarget.ShowYesNo("You'll have to pony up 10 drams of fresh water to even up the trade. Agreed?");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowYesNoMessage,
            Is.EqualTo("取引を釣り合わせるには10ドラムの清水を支払う必要がある。承諾する？"));
    }

    [Test]
    public void Prefix_TranslatesShowBlock_ForDropFailure()
    {
        WriteDictionary(
            ("Trade could not be completed, {0} couldn't drop object: {1}", "取引を完了できなかった。{0}は{1}を落とせなかった。"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.ShowBlock));

        _ = DummyTradeUiPopupTarget.ShowBlock("Trade could not be completed, you couldn't drop object: laser rifle");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowBlockMessage,
            Is.EqualTo("取引を完了できなかった。あなたはlaser rifleを落とせなかった。"));
    }

    [Test]
    public void Prefix_UsesPopupExactFallback_ForStaticTradePopup()
    {
        WriteDictionary(
            ("In the end, though, it makes no difference.", "結局のところ、何も変わらなかった。"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("In the end, though, it makes no difference.");

        Assert.That(DummyTradeUiPopupTarget.LastShowMessage, Is.EqualTo("結局のところ、何も変わらなかった。"));
    }

    [Test]
    public void Prefix_UsesMessagePatternFallback_ForSharedVerbFamily()
    {
        WritePatternDictionary(
            ("^(?:The |the |[Aa]n? )?(.+?) (?:is|are) fully charged!$", "{0}は完全に充電された！"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("The 変圧器 is fully charged!");

        Assert.That(DummyTradeUiPopupTarget.LastShowMessage, Is.EqualTo("変圧器は完全に充電された！"));
    }

    [Test]
    public void Prefix_UsesMessagePatternFallback_ForStartupJoppaIntro()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        MessagePatternTranslator.SetPatternFileForTests(null);

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show(
            "On the 10th of Iyur Ut, you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert.\n\n"
            + "All around you, moisture farmers tend to groves of viridian watervine. There are huts wrought from rock salt and brinestalk.\n\n"
            + "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo(
                "Iyur Utの10th日、あなたは大塩砂漠モグラヤイの遥かな縁にあるオアシス集落ジョッパに到着した。\n\n"
                + "あたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n"
                + "地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。"));
    }

    [Test]
    public void Prefix_PreservesColorTags_ForCustomTradeTemplate()
    {
        WriteDictionary(
            ("You need {0} to repair {1}.", "{1}を修理するには{0}が必要だ。"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("{{R|You need {{C|8}} drams of fresh water to repair those.}}");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("{{R|それらを修理するには{{C|8}}ドラムの清水が必要だ。}}"));
    }

    [Test]
    public void Prefix_StripsRuntimeControlHeader_ForHasNothingToTrade()
    {
        WriteDictionary(
            ("{0} has nothing to trade.", "{0}には取引するものがない"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("\u0002have\u001F16\u001F20\u001F\u0003The 濡れた グロウフィッシュ has nothing to trade.");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("濡れた グロウフィッシュには取引するものがない"));
    }

    [Test]
    public void Prefix_PreservesOuterColorAndStripsRuntimeControlHeader_ForHasNothingToTrade()
    {
        WriteDictionary(
            ("{0} has nothing to trade.", "{0}には取引するものがない"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("{{y|\u0002have\u001F16\u001F20\u001F\u0003The 巨大トンボ has nothing to trade.}}");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("{{y|巨大トンボには取引するものがない}}"));
    }

    private static IDisposable PatchMethod(string methodName)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyTradeUiPopupTarget), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(TradeUiPopupTranslationPatch), nameof(TradeUiPopupTranslationPatch.Prefix))));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
    }

    // To-do: consolidate these JSON test helpers once the shared usage reaches 3+ files.
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

    private sealed class HarmonyPatchScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyPatchScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
