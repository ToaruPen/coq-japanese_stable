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
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", Utf8WithoutBom);
        DummyTradeUiPopupTarget.Reset();
        DummyPopupShow.Reset();
        DummyPopupTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
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
            Is.EqualTo("商人は、あなたが彼に借りている5ドラムの{{B|真水}}を支払うまで取引してくれない。"));
    }

    [Test]
    public void Prefix_TranslatesShowYesNo_ForTradeQuestion()
    {
        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.ShowYesNo));

        _ = DummyTradeUiPopupTarget.ShowYesNo("You'll have to pony up 10 drams of fresh water to even up the trade. Agreed?");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowYesNoMessage,
            Is.EqualTo("取引を釣り合わせるには10ドラムの真水を支払う必要がある。承諾する？"));
    }

    [Test]
    public void Prefix_TranslatesShowMessage_WithGenericReceiveItemPattern()
    {
        WritePatternDictionary(("^You receive (.+?)[.!]?$", "{t0}を受け取った"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("You receive 奇妙な小物!");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("奇妙な小物を受け取った"));
    }

    [Test]
    public void Prefix_UsesOwnerTemplateForTradeQuestion_IgnoresDictionaryEntriesAndPreservesColorTags()
    {
        WriteDictionary(
            ("You'll have to pony up 10 drams of fresh water to even up the trade. Agreed?", "dictionary exact fallback should not be used"),
            ("You'll have to pony up {0} to even up the trade. Agreed?", "dictionary template should not be used: {0}"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.ShowYesNo));

        _ = DummyTradeUiPopupTarget.ShowYesNo(
            "{{R|You'll have to pony up {{C|10}} drams of fresh water to even up the trade. Agreed?}}");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowYesNoMessage,
            Is.EqualTo("{{R|取引を釣り合わせるには{{C|10}}ドラムの真水を支払う必要がある。承諾する？}}"));
    }

    [Test]
    public void Prefix_TranslatesShowMessage_ForForceCompleteTradeWaterMessages()
    {
        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("You pony up 1 dram of fresh water to even up the trade.");
        var playerPays = DummyTradeUiPopupTarget.LastShowMessage;

        DummyTradeUiPopupTarget.Show("The 商人 ponies up 12 drams of fresh water to even up the trade.");
        var traderPays = DummyTradeUiPopupTarget.LastShowMessage;

        DummyTradeUiPopupTarget.Show("The 商人 doesn't have 1 dram of fresh water to even up the trade!");
        var traderCannotPay = DummyTradeUiPopupTarget.LastShowMessage;

        Assert.Multiple(() =>
        {
            Assert.That(playerPays, Is.EqualTo("あなたは取引を釣り合わせるために1ドラムの真水を支払った。"));
            Assert.That(traderPays, Is.EqualTo("商人は取引を釣り合わせるためにあなたへ12ドラムの真水を支払った。"));
            Assert.That(traderCannotPay, Is.EqualTo("商人には取引を釣り合わせるための1ドラムの真水がない！"));
        });
    }

    [Test]
    public void Prefix_PreservesInlineSubjectColors_ForTradeOwnerTemplates()
    {
        WriteDictionary(
            ("{0} will not trade with you until you pay {1} the {2} you owe {3}.", "{0}は、あなたが{1}に借りている{2}を支払うまで取引してくれない。"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("The {{G|商人}} ponies up 12 drams of fresh water to even up the trade.");
        var traderPays = DummyTradeUiPopupTarget.LastShowMessage;

        DummyTradeUiPopupTarget.Show("\u0002have\u001F16\u001F20\u001F\u0003The {{G|ウォーターヴァイン農家}} has nothing to trade.");
        var hasNothing = DummyTradeUiPopupTarget.LastShowMessage;

        DummyTradeUiPopupTarget.Show("The {{G|商人}} will not trade with you until you pay 彼 the 5 drams of fresh water you owe 彼.");
        var waterDebt = DummyTradeUiPopupTarget.LastShowMessage;

        Assert.Multiple(() =>
        {
            Assert.That(traderPays, Is.EqualTo("{{G|商人}}は取引を釣り合わせるためにあなたへ12ドラムの真水を支払った。"));
            Assert.That(hasNothing, Is.EqualTo("{{G|ウォーターヴァイン農家}}には取引するものがない"));
            Assert.That(waterDebt, Is.EqualTo("{{G|商人}}は、あなたが彼に借りている5ドラムの{{B|真水}}を支払うまで取引してくれない。"));
        });
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
        WriteDictionary(("10th", "第10"), ("Iyur Ut", "イユル・ウト"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show(
            "On the 10th of Iyur Ut, you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert.\n\n"
            + "All around you, moisture farmers tend to groves of viridian watervine. There are huts wrought from rock salt and brinestalk.\n\n"
            + "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo(
                "イユル・ウトの第10日、あなたは大塩砂漠モグラヤイの遥かな縁にあるオアシス集落ジョッパに到着した。\n\n"
                + "あたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n"
                + "地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。"));
    }

    [Test]
    public void Prefix_UsesPopupProducerFallback_ForQuestReceivedPopup()
    {
        WriteDictionary(("You have received a new quest, {0}!", "新しいクエスト「{0}」を受けた！"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("You have received a new quest, {{W|O Glorious Shekhinah!}}!");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("新しいクエスト「{{W|O Glorious Shekhinah!}}」を受けた！"));
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
            Is.EqualTo("{{R|それらを修理するには{{C|8}}ドラムの真水が必要だ。}}"));
    }

    [Test]
    public void Prefix_TranslatesDoVendorRepairBrokenMessages_WhenPatched()
    {
        WriteDictionary(("{0} isn't broken!", "{0}は壊れていない！"));

        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("{{R|That item isn't broken!}}");
        var singular = DummyTradeUiPopupTarget.LastShowMessage;

        DummyTradeUiPopupTarget.Show("Those items aren't broken!");
        var plural = DummyTradeUiPopupTarget.LastShowMessage;

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.EqualTo("{{R|その品は壊れていない！}}"));
            Assert.That(plural, Is.EqualTo("それらの品は壊れていない！"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairBroken"), Is.EqualTo(2));
        });
    }

    [Test]
    public void Prefix_RecordsTryRemoveAndRepairOwnerTemplateRoutes_WhenPatched()
    {
        WriteDictionary(
            ("Trade could not be completed, {0} couldn't drop object: {1}", "取引を完了できなかった。{0}は{1}を落とせなかった。"),
            ("{0} are too complex for {1} to repair.", "{0}は{1}には複雑すぎて修理できない。"),
            ("You need {0} to repair {1}.", "{1}を修理するには{0}が必要だ。"),
            ("You may repair {0} for {1}.", "{0}を{1}で修理できる。"),
            ("{0} isn't broken!", "{0}は壊れていない！"));

        using var showPatch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));
        using var showBlockPatch = PatchMethod(nameof(DummyTradeUiPopupTarget.ShowBlock));
        using var showYesNoPatch = PatchMethod(nameof(DummyTradeUiPopupTarget.ShowYesNo));

        _ = DummyTradeUiPopupTarget.ShowBlock("Trade could not be completed, you couldn't drop object: {{Y|laser rifle}}");
        DummyTradeUiPopupTarget.Show("These items are too complex for {{G|商人}} to repair.");
        DummyTradeUiPopupTarget.Show("You need {{C|8}} drams of fresh water to repair those.");
        _ = DummyTradeUiPopupTarget.ShowYesNo("You may repair this for {{C|8}} drams of fresh water.");
        DummyTradeUiPopupTarget.Show("That item isn't broken!");

        Assert.Multiple(() =>
        {
            Assert.That(DummyTradeUiPopupTarget.LastShowBlockMessage, Is.EqualTo("取引を完了できなかった。あなたは{{Y|laser rifle}}を落とせなかった。"));
            Assert.That(DummyTradeUiPopupTarget.LastShowYesNoMessage, Is.EqualTo("これを{{C|8}}ドラムの真水で修理できる。"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.TryRemove"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairTooComplex"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairNeed"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairQuestion"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairBroken"), Is.EqualTo(1));
        });
    }

    [Test]
    public void VendorOwnerPatch_TranslatesTryRemoveShowBlock_WhenOwnerPatched()
    {
        WriteDictionary(
            ("Trade could not be completed, {0} couldn't drop object: {1}", "取引を完了できなかった。{0}は{1}を落とせなかった。"));

        using var popupPatch = PatchPopupTranslationShowBlock();
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.TryRemove));
        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = "Trade could not be completed, you couldn't drop object: {{Y|laser rifle}}",
        };

        target.TryRemove();

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("取引を完了できなかった。あなたは{{Y|laser rifle}}を落とせなかった。"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.TryRemove"), Is.EqualTo(1));
        });
    }

    [Test]
    public void VendorOwnerPatch_DoesNotTranslateTryRemoveShowBlock_WhenOwnerAbsent()
    {
        WriteDictionary(
            ("Trade could not be completed, {0} couldn't drop object: {1}", "取引を完了できなかった。{0}は{1}を落とせなかった。"));

        using var popupPatch = PatchPopupTranslationShowBlock();
        const string source = "Trade could not be completed, you couldn't drop object: {{Y|laser rifle}}";

        DummyPopupTarget.ShowBlock(source);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(source));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.TryRemove"), Is.Zero);
        });
    }

    [Test]
    public void VendorOwnerPatch_TranslatesRepairShowAndConfirmationPopups_WhenOwnerPatched()
    {
        WriteDictionary(
            ("{0} are too complex for {1} to repair.", "{0}は{1}には複雑すぎて修理できない。"),
            ("You need {0} to repair {1}.", "{1}を修理するには{0}が必要だ。"),
            ("You may repair {0} for {1}.", "{0}を{1}で修理できる。"));

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var showYesNoPatch = PatchPopupShow(nameof(DummyPopupShow.ShowYesNo));
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRepair));

        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = "These items are too complex for {{G|商人}} to repair.",
        };
        target.DoVendorRepair();
        var tooComplex = DummyPopupShow.LastShowMessage;

        target.PopupMessageToShow = "You need {{C|8}} drams of fresh water to repair those.";
        target.DoVendorRepair();

        target.PopupMessageToShow = "You may repair this for {{C|8}} drams of fresh water.";
        target.UseConfirmationPopup = true;
        target.DoVendorRepair();

        Assert.Multiple(() =>
        {
            Assert.That(tooComplex, Is.EqualTo("これらの品は{{G|商人}}には複雑すぎて修理できない。"));
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("それらを修理するには{{C|8}}ドラムの真水が必要だ。"));
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("これを{{C|8}}ドラムの真水で修理できる。"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairTooComplex"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairNeed"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairQuestion"), Is.EqualTo(1));
        });
    }

    [Test]
    public void VendorOwnerPatch_TranslatesRepairBrokenPopups_WhenOwnerPatched()
    {
        WriteDictionary(("{0} isn't broken!", "{0}は壊れていない！"));

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRepair));
        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = "{{R|That item isn't broken!}}",
        };

        target.DoVendorRepair();
        var singular = DummyPopupShow.LastShowMessage;

        target.PopupMessageToShow = "Those items aren't broken!";
        target.DoVendorRepair();

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.EqualTo("{{R|その品は壊れていない！}}"));
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("それらの品は壊れていない！"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairBroken"), Is.EqualTo(2));
        });
    }

    [Test]
    public void VendorOwnerPatch_DoesNotRetranslateDirectMarkedRepairPopup_WhenOwnerPatched()
    {
        WriteDictionary(("{0} isn't broken!", "{0}は壊れていない！"));

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRepair));
        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("That item isn't broken!"),
        };

        target.DoVendorRepair();

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("That item isn't broken!"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairBroken"), Is.Zero);
        });
    }

    [Test]
    public void VendorOwnerPatch_LeavesEmptyRepairPopupUnchanged_WhenOwnerPatched()
    {
        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRepair));
        var target = new DummyTradeUiVendorPopupProducerTarget();

        target.DoVendorRepair();

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RepairBroken"), Is.Zero);
        });
    }

    [Test]
    public void VendorOwnerPatch_TranslatesShowTradeScreenOwnerPopups_WhenOwnerPatched()
    {
        WriteDictionary(
            ("{0} cannot carry things.", "{0}は物を運べない。"),
            ("{0} will not trade with you until you pay {1} the {2} you owe {3}. Do you want to give it to {4} now?", "{0}は、あなたが{3}に借りている{2}を{1}に支払うまで取引してくれない。今すぐそれを{4}に渡しますか？"),
            ("{0} does not have the skill to {1}.", "{0}には{1}技能がない。"));

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var showYesNoPatch = PatchPopupShow(nameof(DummyPopupShow.ShowYesNo));
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.ShowTradeScreen));

        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = "商人 cannot carry things.",
            UseShowFailPopup = true,
        };
        target.ShowTradeScreen();
        var cannotCarry = DummyPopupShow.LastShowMessage;

        target.UseShowFailPopup = false;
        target.PopupMessageToShow = "\u0002have\u001F16\u001F20\u001F\u0003The {{G|ウォーターヴァイン農家}} has nothing to trade.";
        target.ShowTradeScreen();
        var hasNothing = DummyPopupShow.LastShowMessage;

        target.UseConfirmationPopup = true;
        target.PopupMessageToShow = "The {{G|商人}} will not trade with you until you pay 彼 the {{C|5}} drams of {{B|fresh water}} you owe 彼. Do you want to give it to 彼 now?";
        target.ShowTradeScreen();
        var waterDebt = DummyPopupShow.LastShowYesNoMessage;

        target.UseConfirmationPopup = false;
        target.PopupMessageToShow = "商人 does not have the skill to repair items.";
        target.ShowTradeScreen();
        var repairSkill = DummyPopupShow.LastShowMessage;

        target.PopupMessageToShow = "商人 does not have the skill to recharge items.";
        target.ShowTradeScreen();
        var rechargeSkill = DummyPopupShow.LastShowMessage;

        Assert.Multiple(() =>
        {
            Assert.That(cannotCarry, Is.EqualTo("商人は物を運べない。"));
            Assert.That(hasNothing, Is.EqualTo("{{G|ウォーターヴァイン農家}}には取引するものがない"));
            Assert.That(waterDebt, Is.EqualTo("{{G|商人}}は、あなたが彼に借りている{{C|5}}ドラムの{{B|真水}}を彼に支払うまで取引してくれない。今すぐそれを彼に渡しますか？"));
            Assert.That(repairSkill, Is.EqualTo("商人にはアイテムを修理する技能がない。"));
            Assert.That(rechargeSkill, Is.EqualTo("商人にはアイテムを充電する技能がない。"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.CannotCarry"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.HasNothingToTrade"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.WaterDebtGiveIt"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.MissingSkill"), Is.EqualTo(2));
        });
    }

    [Test]
    public void VendorOwnerPatch_TranslatesDoVendorExamineOwnerPopups_WhenOwnerPatched()
    {
        UseRepositoryDictionaries();

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var showYesNoPatch = PatchPopupShow(nameof(DummyPopupShow.ShowYesNo));
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorExamine));

        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = "You can't understand 商人の explanation.",
            UseShowFailPopup = true,
        };
        target.DoVendorExamine();
        var explanation = DummyPopupShow.LastShowMessage;

        target.PopupMessageToShow = "This item is too complex for 商人 to identify.";
        target.DoVendorExamine();
        var tooComplex = DummyPopupShow.LastShowMessage;

        target.PopupMessageToShow = "You do not have the required {{C|7}} drams to identify this item.";
        target.DoVendorExamine();
        var requiredDrams = DummyPopupShow.LastShowMessage;

        target.UseShowFailPopup = false;
        target.UseConfirmationPopup = true;
        target.PopupMessageToShow = "You may identify this for 7 drams of fresh water.";
        target.DoVendorExamine();
        var question = DummyPopupShow.LastShowYesNoMessage;

        target.UseConfirmationPopup = false;
        target.PopupMessageToShow = "商人 identifies {{Y|laser pistol}} as レーザーピストル.";
        target.DoVendorExamine();
        var result = DummyPopupShow.LastShowMessage;

        target.PopupMessageToShow = "商人 doesn't have the skill to identify artifacts.";
        target.DoVendorExamine();
        var missingSkill = DummyPopupShow.LastShowMessage;

        Assert.Multiple(() =>
        {
            Assert.That(explanation, Is.EqualTo("商人の説明は理解できない。"));
            Assert.That(tooComplex, Is.EqualTo("この品は商人には複雑すぎて鑑定できない。"));
            Assert.That(requiredDrams, Is.EqualTo("この品を鑑定するのに必要な{{C|7}}ドラムが足りない。"));
            Assert.That(question, Is.EqualTo("これを7ドラムの真水で鑑定できる。"));
            Assert.That(result, Is.EqualTo("商人は{{Y|laser pistol}}をレーザーピストルだと鑑定した。"));
            Assert.That(missingSkill, Is.EqualTo("商人にはアーティファクトを鑑定する技能がない。"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.Explanation"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.IdentifyTooComplex"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.IdentifyRequiredDrams"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.IdentifyQuestion"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.IdentifyResult"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.MissingSkill"), Is.EqualTo(1));
        });
    }

    [Test]
    public void VendorOwnerPatch_TranslatesDoVendorRechargeOwnerPopups_WhenOwnerPatched()
    {
        UseRepositoryDictionaries();
        UseRepositoryVerbDictionary();

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var showYesNoPatch = PatchPopupShow(nameof(DummyPopupShow.ShowYesNo));
        using var ownerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRecharge));

        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = "You need {{C|4}} drams of fresh water to charge one of those.",
        };
        _ = target.DoVendorRecharge();
        var need = DummyPopupShow.LastShowMessage;

        target.UseConfirmationPopup = true;
        target.PopupMessageToShow = "You may recharge {{Y|変圧器}} for {{C|4}} drams of fresh water.";
        _ = target.DoVendorRecharge();
        var question = DummyPopupShow.LastShowYesNoMessage;

        target.UseConfirmationPopup = false;
        const string subject = "The 変圧器";
        target.PopupMessageToShow = DoesVerbRouteTranslator.MarkDoesFragment(
            subject + " are",
            "are",
            subject.Length,
            null) + " fully charged!";
        _ = target.DoVendorRecharge();
        var fullyCharged = DummyPopupShow.LastShowMessage;

        Assert.Multiple(() =>
        {
            Assert.That(need, Is.EqualTo("そのうちの1つを充電するには{{C|4}}ドラムの真水が必要だ。"));
            Assert.That(question, Is.EqualTo("{{Y|変圧器}}を{{C|4}}ドラムの真水で充電できる。"));
            Assert.That(fullyCharged, Is.EqualTo("変圧器は完全に充電された！"));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RechargeNeed"), Is.EqualTo(1));
            Assert.That(TradeUiPopupHitCount("TradeUiPopup.RechargeQuestion"), Is.EqualTo(1));
        });
    }

    [Test]
    public void VendorOwnerPatch_TranslatesFixedVendorPopupFallbacks_WithRepositoryDictionaries()
    {
        UseRepositoryDictionaries();

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var examineOwnerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorExamine));
        using var rechargeOwnerPatch = PatchVendorOwner(nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRecharge));

        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = "You already understand this item.",
            UseShowFailPopup = true,
        };
        target.DoVendorExamine();
        var alreadyUnderstand = DummyPopupShow.LastShowMessage;

        target.UseShowFailPopup = false;
        target.PopupMessageToShow = "That item has no cell or rechargeable capacitor in it.";
        _ = target.DoVendorRecharge();
        var noCell = DummyPopupShow.LastShowMessage;

        target.PopupMessageToShow = "That item cannot be recharged this way.";
        _ = target.DoVendorRecharge();
        var cannotRecharge = DummyPopupShow.LastShowMessage;

        Assert.Multiple(() =>
        {
            Assert.That(alreadyUnderstand, Is.EqualTo("このアイテムはすでに理解している。"));
            Assert.That(noCell, Is.EqualTo("そのアイテムには電池も充電可能なコンデンサもない。"));
            Assert.That(cannotRecharge, Is.EqualTo("そのアイテムはこの方法では再充電できない。"));
        });
    }

    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorExamine),
        "TradeUiPopup.IdentifyTooComplex")]
    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRecharge),
        "TradeUiPopup.RechargeNeed")]
    public void VendorOwnerPatch_LeavesEmptyNewVendorOwnerPopupUnchanged_WhenOwnerPatched(
        string methodName,
        string detail)
    {
        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var ownerPatch = PatchVendorOwner(methodName);
        var target = new DummyTradeUiVendorPopupProducerTarget();

        InvokeVendorMethod(target, methodName);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            Assert.That(TradeUiPopupHitCount(detail), Is.Zero);
        });
    }

    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.ShowTradeScreen),
        "商人 cannot carry things.",
        "TradeUiPopup.CannotCarry")]
    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorExamine),
        "This item is too complex for 商人 to identify.",
        "TradeUiPopup.IdentifyTooComplex")]
    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRecharge),
        "You need {{C|4}} drams of fresh water to charge one of those.",
        "TradeUiPopup.RechargeNeed")]
    public void VendorOwnerPatch_DoesNotTranslateNewVendorOwnerPopups_WhenOwnerAbsent(
        string methodName,
        string source,
        string detail)
    {
        UseRepositoryVerbDictionary();
        WriteDictionary(
            ("{0} cannot carry things.", "{0}は物を運べない。"),
            ("This item is too complex for {0} to identify.", "この品は{0}には複雑すぎて鑑定できない。"),
            ("You need {0} to charge {1}.", "{1}を充電するには{0}が必要だ。"));

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = source,
            UseShowFailPopup = true,
        };

        InvokeVendorMethod(target, methodName);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(TradeUiPopupHitCount(detail), Is.Zero);
        });
    }

    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.ShowTradeScreen),
        "商人 cannot carry things.",
        "TradeUiPopup.CannotCarry")]
    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorExamine),
        "This item is too complex for 商人 to identify.",
        "TradeUiPopup.IdentifyTooComplex")]
    [TestCase(
        nameof(DummyTradeUiVendorPopupProducerTarget.DoVendorRecharge),
        "You need {{C|4}} drams of fresh water to charge one of those.",
        "TradeUiPopup.RechargeNeed")]
    public void VendorOwnerPatch_DoesNotRetranslateDirectMarkedNewVendorOwnerPopups_WhenOwnerPatched(
        string methodName,
        string unmarked,
        string detail)
    {
        UseRepositoryVerbDictionary();
        WriteDictionary(
            ("{0} cannot carry things.", "{0}は物を運べない。"),
            ("This item is too complex for {0} to identify.", "この品は{0}には複雑すぎて鑑定できない。"),
            ("You need {0} to charge {1}.", "{1}を充電するには{0}が必要だ。"));

        using var showPatch = PatchPopupShow(nameof(DummyPopupShow.Show));
        using var ownerPatch = PatchVendorOwner(methodName);
        var target = new DummyTradeUiVendorPopupProducerTarget
        {
            PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(unmarked),
            UseShowFailPopup = true,
        };

        InvokeVendorMethod(target, methodName);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
            Assert.That(TradeUiPopupHitCount(detail), Is.Zero);
        });
    }

    [Test]
    public void Prefix_StripsRuntimeControlHeader_ForHasNothingToTrade()
    {
        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("\u0002have\u001F16\u001F20\u001F\u0003The 濡れた グロウフィッシュ has nothing to trade.");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("濡れた グロウフィッシュには取引するものがない"));
    }

    [Test]
    public void Prefix_PreservesOuterColorAndStripsRuntimeControlHeader_ForHasNothingToTrade()
    {
        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("{{y|\u0002have\u001F16\u001F20\u001F\u0003The 巨大トンボ has nothing to trade.}}");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("{{y|巨大トンボには取引するものがない}}"));
    }

    [Test]
    public void Prefix_StripsRuntimeControlHeader_ForSnapjawWarlordHasNothingToTrade()
    {
        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show("\u0002have\u001F14\u001F18\u001F\u0003The スナップジョーの軍主 has nothing to trade.");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("スナップジョーの軍主には取引するものがない"));
    }

    [Test]
    public void Prefix_TranslatesMarkedDoesVerbHookahWaterMessage_WhenPatched()
    {
        UseRepositoryVerbDictionary();
        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));
        var source = DoesVerbRouteTranslator.MarkDoesFragment(
            "The 宙吊りのシーシャ（配管付き） needs",
            "need",
            "The 宙吊りのシーシャ（配管付き）".Length,
            null) + " water in it.";

        DummyTradeUiPopupTarget.Show(source);

        Assert.Multiple(() =>
        {
            Assert.That(DummyTradeUiPopupTarget.LastShowMessage, Is.EqualTo("宙吊りのシーシャ（配管付き）には水が必要だ"));
            Assert.That(DummyTradeUiPopupTarget.LastShowMessage.IndexOf('\u0002'), Is.EqualTo(-1));
            Assert.That(DummyTradeUiPopupTarget.LastShowMessage.IndexOf('\u001f'), Is.EqualTo(-1));
            Assert.That(DummyTradeUiPopupTarget.LastShowMessage.IndexOf('\u0003'), Is.EqualTo(-1));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(TradeUiPopupTranslationPatch),
                    "Popup.ProducerText.DoesVerb"),
                Is.EqualTo(1));
        });
    }

    [Test]
    public void Prefix_TranslatesStoneStatuePrayerPattern_WhenPatched()
    {
        WritePatternDictionary((
            "^You voice a short prayer beneath the (.+?) stone statue of (?:the |a |an )?(.+?)\\.$",
            "あなたは{1}の{0}石像の下で短い祈りを唱えた。"));
        using var patch = PatchMethod(nameof(DummyTradeUiPopupTarget.Show));

        DummyTradeUiPopupTarget.Show(
            "You voice a short prayer beneath the 冒涜された stone statue of a 山羊人の種播き.");

        Assert.That(
            DummyTradeUiPopupTarget.LastShowMessage,
            Is.EqualTo("あなたは山羊人の種播きの冒涜された石像の下で短い祈りを唱えた。"));
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

    private static IDisposable PatchPopupShow(string methodName)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static IDisposable PatchPopupTranslationShowBlock()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static IDisposable PatchVendorOwner(string methodName)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyTradeUiVendorPopupProducerTarget), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(TradeUiVendorPopupTranslationPatch), nameof(TradeUiVendorPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(TradeUiVendorPopupTranslationPatch), nameof(TradeUiVendorPopupTranslationPatch.Finalizer))));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void InvokeVendorMethod(DummyTradeUiVendorPopupProducerTarget target, string methodName)
    {
        _ = RequireMethod(typeof(DummyTradeUiVendorPopupProducerTarget), methodName).Invoke(target, []);
    }

    private static int TradeUiPopupHitCount(string family)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(TradeUiPopupTranslationPatch),
            family);
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
    }

    private static void UseRepositoryVerbDictionary()
    {
        MessageFrameTranslator.SetDictionaryPathForTests(
            Path.Combine(GetLocalizationRoot(), "MessageFrames", "verbs.ja.json"));
    }

    private static void UseRepositoryDictionaries()
    {
        Translator.SetDictionaryDirectoryForTests(Path.Combine(GetLocalizationRoot(), "Dictionaries"));
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
