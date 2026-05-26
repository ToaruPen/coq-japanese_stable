using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WaterRitualPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        JournalPatternTranslator.ResetForTests();
        Translator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        JournalPatternTranslator.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBeginHandleEvent),
        nameof(DummyPopupShow.ShowYesNoCancel),
        "Do you want to play a game of Sifrah to perform the formal water ritual with {{G|Tam}}? The formal ritual can be much more impactful. If you do not play the game of Sifrah, the informal water ritual will consume 1 dram of {{B|fresh water}}.",
        "{{G|Tam}}と正式な水の儀式を行うためにシフラーのゲームをプレイしますか？正式な儀式はより大きな影響をもたらすことがあります。シフラーをプレイしない場合、非正式な水の儀式は{{B|fresh water}}を1ドラム消費します。",
        "FormalRitualPrompt")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBeginHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "You don't have enough {{B|fresh water}} to begin the ritual.",
        "儀式を始めるには{{B|fresh water}}が足りない。",
        "NotEnoughLiquid")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
        nameof(DummyPopupShow.Show),
        "Talking to {{Y|the warden}} rouses in you an inert truth. You once wore the frock of a child. You poured salt through the cracks of your fingers, and you watched worlds form. Can it be all so simple still?",
        "{{Y|the warden}}との会話が、あなたの内に眠る真実を呼び覚ました。あなたはかつて子供の上着をまとっていた。指の隙間から塩を注ぎ、世界が形作られるのを見ていた。今もなお、それほど単純でありうるのだろうか？",
        "SkillPointIntro")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
        nameof(DummyPopupShow.Show),
        "You gained {{C|50}} skill points!",
        "{{C|50}}スキルポイントを得た！",
        "SkillPointGain")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Hortensa}} teaches you to craft the item modification {{W|sturdy}}.",
        "{{G|Hortensa}}がアイテム改造{{W|sturdy}}の作り方を教えてくれた。",
        "TinkeringMod")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Hortensa}} teaches you to craft {{W|spring-loaded boots}}.",
        "{{G|Hortensa}}が{{W|spring-loaded boots}}の作り方を教えてくれた。",
        "TinkeringRecipe")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "{{G|Tam}} has no more secrets to share.",
        "{{G|Tam}}にはもう共有できる秘密がない。",
        "BuySecretNoMoreSecrets")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares a recipe with you.",
        "{{G|Tam}}がレシピを共有してくれた。",
        "BuySecretRecipe")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares the location of {{Y|the Rust Wells}}.",
        "{{G|Tam}}が{{Y|the Rust Wells}}の場所を教えてくれた。",
        "BuySecretLocation")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares an event from the life of a sultan with you.\n\n\"In 100 AR, a sultan found a chrome idol.\"",
        "{{G|Tam}}がスルタンの生涯の出来事を共有してくれた。\n\n\"In 100 AR, a sultan found a chrome idol.\"",
        "BuySecretSultanEvent")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.IWaterRitualPartUseReputation),
        nameof(DummyPopupShow.Show),
        "You don't have a high enough reputation with {{Y|the Farmers' Guild}}.",
        "{{Y|Farmers' Guild}}との評判が十分に高くない。",
        "ReputationTooLow")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualPerformRitual),
        nameof(DummyPopupShow.Show),
        "You share your {{B|fresh water}} with {{G|Tam}} and begin the water ritual.",
        "{{G|Tam}}と{{B|真水}}を分かち合い、水の儀式を始めた。",
        "PerformRitual")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuyItemHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} gifts you {{Y|the electrobow}}!",
        "{{G|Tam}}が{{Y|the electrobow}}を贈ってくれた！",
        "BuyItemGift")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualGainMutationHandleEvent),
        nameof(DummyPopupShow.Show),
        "Despite your genetic limitations, {{G|Tam}} teaches you to improvise {{M|Wings}}!",
        "遺伝的な制限にもかかわらず、{{G|Tam}}が{{M|Wings}}を即興で扱う方法を教えてくれた！",
        "GainMutation")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualRandomMutationHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "You can't be mutated.",
        "変異できない。",
        "RandomMutationNonMutant")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualRandomMutationHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "{{R|You can't be mutated.}}",
        "{{R|変異できない。}}",
        "RandomMutationNonMutant")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualRandomMutationHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "You can't gain physical mutations.",
        "肉体変異は得られない。",
        "RandomMutationIncompatible")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualRandomMutationHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "You can't gain mental mutations.",
        "精神変異は得られない。",
        "RandomMutationIncompatible")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualJoinPartyHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} joins you!",
        "{{G|Tam}}が仲間に加わった！",
        "JoinParty")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualNephilimPacifyTryGiveCircle),
        nameof(DummyPopupShow.Show),
        "You receive {{Y|an amulet}}!",
        "{{Y|an amulet}}を受け取った！",
        "NephilimCircle")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSellSecretHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "{{G|Tam}} can't grant you any more reputation.",
        "{{G|Tam}}はこれ以上評判を与えられない。",
        "SellSecretNoMoreReputation")]
    public void Patch_TranslatesWaterRitualOwnerPopups_WhenOwnerPatched(
        string methodName,
        string popupMethod,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwnerAndPopup(methodName, popupMethod, () =>
        {
            var target = new DummyWaterRitualPopupProducerTarget
            {
                PopupMethod = popupMethod,
                PopupMessageToShow = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBeginHandleEvent),
        nameof(DummyPopupShow.ShowYesNoCancel),
        "Do you want to play a game of Sifrah to perform the formal water ritual with {{G|Tam}}? The formal ritual can be much more impactful. If you do not play the game of Sifrah, the informal water ritual will consume 1 dram of {{B|fresh water}}.",
        "FormalRitualPrompt")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
        nameof(DummyPopupShow.Show),
        "You gained {{C|50}} skill points!",
        "SkillPointGain")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Hortensa}} teaches you to craft {{W|spring-loaded boots}}.",
        "TinkeringRecipe")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "{{G|Tam}} has no more secrets to share.",
        "BuySecretNoMoreSecrets")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares a recipe with you.",
        "BuySecretRecipe")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "{{G|Tam}} has no more secrets to share.",
        "BuySecretNoMoreSecrets")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares the location of {{Y|the Rust Wells}}.",
        "BuySecretLocation")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares an event from the life of a sultan with you.\n\n\"In 100 AR, a sultan found a chrome idol.\"",
        "BuySecretSultanEvent")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.IWaterRitualPartUseReputation),
        nameof(DummyPopupShow.Show),
        "You don't have a high enough reputation with {{Y|the Farmers' Guild}}.",
        "ReputationTooLow")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuyItemHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} gifts you {{Y|the electrobow}}!",
        "BuyItemGift")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuyItemHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} gifts you {{Y|the electrobow}}!",
        "BuyItemGift")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualGainMutationHandleEvent),
        nameof(DummyPopupShow.Show),
        "Despite your genetic limitations, {{G|Tam}} teaches you to improvise {{M|Wings}}!",
        "GainMutation")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualRandomMutationHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "You can't gain physical mutations.",
        "RandomMutationIncompatible")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualJoinPartyHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} joins you!",
        "JoinParty")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualNephilimPacifyTryGiveCircle),
        nameof(DummyPopupShow.Show),
        "You receive {{Y|an amulet}}!",
        "NephilimCircle")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSellSecretHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "{{G|Tam}} can't grant you any more reputation.",
        "SellSecretNoMoreReputation")]
    public void Patch_DoesNotTranslateWaterRitualPopup_WhenOwnerAbsent(
        string methodName,
        string popupMethod,
        string source,
        string detail)
    {
        _ = methodName;
        WithPatchedPopupOnly(popupMethod, () =>
        {
            InvokePopup(popupMethod, source);

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(source));
                Assert.That(HitCount(detail), Is.Zero);
            });
            });
    }

    [Test]
    public void Patch_TranslatesWaterRitualPerformRitualPopup_WhenOwnerAbsent()
    {
        const string source = "You share your {{B|fresh water}} with {{G|Tam}} and begin the water ritual.";

        WithPatchedPopupOnly(
            nameof(DummyPopupShow.Show),
            () =>
            {
                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowMessage,
                        Is.EqualTo("{{G|Tam}}と{{B|真水}}を分かち合い、水の儀式を始めた。"));
                    Assert.That(
                        DynamicTextObservability.GetRouteFamilyHitCountForTests(
                            nameof(PopupShowTranslationPatch),
                            "Popup.ProducerText.WaterRitual.PerformRitual"),
                        Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "&yYour reputation with &Cthe 監視官同胞団&y increased by &G100&y to &C-50&y.",
        "&y&C監視官同胞団&yとの評判が&G100&y増加し、&C-50&yになった。",
        "Reputation")]
    [TestCase(
        "&yBecause they admire 監視官イラメ, your reputation with the ジョッパの村人たち increased by &G100&y to &C-40&y.",
        "&y監視官イラメを尊敬しているため、ジョッパの村人たちとの評判が&G100&y増加し、&C-40&yになった。",
        "ReputationBecause")]
    public void Patch_TranslatesWaterRitualReputationPopup_WhenOwnerAbsent(
        string source,
        string expected,
        string detail)
    {
        WithPatchedPopupOnly(
            nameof(DummyPopupShow.Show),
            () =>
            {
                DummyPopupShow.Show(source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(
                        DynamicTextObservability.GetRouteFamilyHitCountForTests(
                            nameof(PopupShowTranslationPatch),
                            "Popup.ProducerText.WaterRitualReputation." + detail),
                        Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "Your reputation with {{C|the 監視官同胞団}} increased by {{G|100}} to {{g|600}}.\n\nYou are now {{g|favored}} by {{C|the 監視官同胞団}}.",
        "{{C|監視官同胞団}}との評判が{{G|100}}増加し、{{g|600}}になった。\n\n{{C|監視官同胞団}}から{{g|好意的}}と見なされるようになった。")]
    [TestCase(
        "Because they dislike 監視官イラメ, your reputation with the villagers of テガニプ decreased by {{R|100}} to {{r|-600}}.\n\nThe villagers of テガニプ are now {{r|despised}} to you.",
        "監視官イラメをよく思っていないため、テガニプの村人たちとの評判が{{R|100}}減少し、{{r|-600}}になった。\n\nテガニプの村人たちはあなたを{{r|憎悪されている}}と見なすようになった。")]
    public void Patch_TranslatesWaterRitualReputationPopup_WithStandingParagraph(
        string source,
        string expected)
    {
        WithPatchedPopupOnly(
            nameof(DummyPopupShow.Show),
            () =>
            {
                DummyPopupShow.Show(source);

                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        const string unmarked = "You gained {{C|50}} skill points!";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                target.WaterRitualSkillPointHandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                    Assert.That(HitCount("SkillPointGain"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedBuySecretRevealPopup_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        const string unmarked = "{{G|Tam}} shares a recipe with you.";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry));

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                    Assert.That(HitCount("BuySecretRecipe"), Is.Zero);
                });
            });
    }

    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares the location of {{Y|the Rust Wells}}.",
        "BuySecretLocation")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} shares an event from the life of a sultan with you.\n\n\"In 100 AR, a sultan found a chrome idol.\"",
        "BuySecretSultanEvent")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.IWaterRitualPartUseReputation),
        nameof(DummyPopupShow.Show),
        "You don't have a high enough reputation with {{Y|the Farmers' Guild}}.",
        "ReputationTooLow")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualPerformRitual),
        nameof(DummyPopupShow.Show),
        "You share your {{B|fresh water}} with {{G|Tam}} and begin the water ritual.",
        "PerformRitual")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualGainMutationHandleEvent),
        nameof(DummyPopupShow.Show),
        "Despite your genetic limitations, {{G|Tam}} teaches you to improvise {{M|Wings}}!",
        "GainMutation")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualRandomMutationHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "You can't gain physical mutations.",
        "RandomMutationIncompatible")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualJoinPartyHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Tam}} joins you!",
        "JoinParty")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualNephilimPacifyTryGiveCircle),
        nameof(DummyPopupShow.Show),
        "You receive {{Y|an amulet}}!",
        "NephilimCircle")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSellSecretHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "{{G|Tam}} can't grant you any more reputation.",
        "SellSecretNoMoreReputation")]
    public void Patch_DoesNotRetranslateDirectMarkedNewFamilyPopups_WhenOwnerPatched(
        string methodName,
        string popupMethod,
        string unmarked,
        string detail)
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        WithPatchedOwnerAndPopup(methodName, popupMethod, () =>
        {
            var target = new DummyWaterRitualPopupProducerTarget
            {
                PopupMethod = popupMethod,
                PopupMessageToShow = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(unmarked));
                Assert.That(HitCount(detail), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesUnknownEnglishPopupUnchanged_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        const string source = "{{G|Tam}} shares an unknown water ritual secret.";

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                target.WaterRitualSkillPointHandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("SkillPointIntro"), Is.Zero);
                    Assert.That(HitCount("SkillPointGain"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_TranslatesRuntimeBuySecretGossipPopup_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        var source =
            "{{G|Tam}} shares some gossip with you.\n\n\"I heard that some organization repeatedly beat some party at dice.\"";

        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        JournalPatternTranslator.ResetForTests();
        try
        {
            WithPatchedOwnerAndPopup(
                nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
                popupMethod,
                () =>
                {
                    var target = new DummyWaterRitualPopupProducerTarget
                    {
                        PopupMethod = popupMethod,
                        PopupMessageToShow = source,
                    };

                    InvokeOwnerMethod(target, nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry));

                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            DummyPopupShow.LastShowMessage,
                            Is.EqualTo("{{G|Tam}}が噂を共有してくれた。\n\n\"聞いたところでは、ある組織はある一団を何度も賽子で打ち負かした。\""));
                        Assert.That(HitCount("BuySecretGossip"), Is.EqualTo(1));
                        Assert.That(HitCount("BuySecretRecipe"), Is.Zero);
                        Assert.That(HitCount("BuySecretLocation"), Is.Zero);
                        Assert.That(HitCount("BuySecretSultanEvent"), Is.Zero);
                    });
                });
        }
        finally
        {
            JournalPatternTranslator.ResetForTests();
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
        }
    }

    [Test]
    public void Patch_StripsDirectMarkedBuySecretGossipLeadInWithoutRetranslatingBody_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        var source =
            "{{G|Tam}} shares some gossip with you.\n\n\""
            + MessageFrameTranslator.DirectTranslationMarker
            + "I heard that some organization repeatedly beat some party at dice.\"";

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry));

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowMessage,
                        Is.EqualTo("{{G|Tam}}が噂を共有してくれた。\n\n\"聞いたところでは、some organization repeatedly beat some party at dice.\""));
                    Assert.That(HitCount("BuySecretGossip"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_TranslatesWrappedBuySecretGossipLeadIn_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        var source =
            "{{G|Tam}} shares some gossip with you.\n\n\"{{Y|I heard that some organization repeatedly beat some party at dice.}}\"";

        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        JournalPatternTranslator.ResetForTests();
        try
        {
            WithPatchedOwnerAndPopup(
                nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
                popupMethod,
                () =>
                {
                    var target = new DummyWaterRitualPopupProducerTarget
                    {
                        PopupMethod = popupMethod,
                        PopupMessageToShow = source,
                    };

                    InvokeOwnerMethod(target, nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry));

                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            DummyPopupShow.LastShowMessage,
                            Is.EqualTo("{{G|Tam}}が噂を共有してくれた。\n\n\"{{Y|聞いたところでは、ある組織はある一団を何度も賽子で打ち負かした。}}\""));
                        Assert.That(HitCount("BuySecretGossip"), Is.EqualTo(1));
                    });
                });
        }
        finally
        {
            JournalPatternTranslator.ResetForTests();
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
        }
    }

    [Test]
    public void Patch_StripsDirectMarkedUnknownBuySecretGossipWithoutRetranslatingBody_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        var source =
            "{{G|Tam}} shares some gossip with you.\n\n\""
            + MessageFrameTranslator.DirectTranslationMarker
            + "Listen well. A ruin lies beneath the dunes.\"";

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry));

                Assert.Multiple(() =>
                {
                    Assert.That(
                        DummyPopupShow.LastShowMessage,
                        Is.EqualTo("{{G|Tam}}が噂を共有してくれた。\n\n\"Listen well. A ruin lies beneath the dunes.\""));
                    Assert.That(HitCount("BuySecretGossip"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_LeavesUnknownRuntimeBuySecretGossipPopupUnchanged_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        const string source = "{{G|Tam}} shares some gossip with you.\n\n\"Listen well. A ruin lies beneath the dunes.\"";

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                InvokeOwnerMethod(target, nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretRevealEntry));

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("BuySecretGossip"), Is.Zero);
                });
            });
    }

    [TestCase("{{G|Tam}} shares an unfamiliar mutation lesson with you.", nameof(DummyPopupShow.Show))]
    public void Patch_LeavesRandomMutationNonOwnerMessagesUnchanged_WhenOwnerPatched(string source, string popupMethod)
    {
        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualRandomMutationHandleEvent),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                target.WaterRitualRandomMutationHandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(source));
                    Assert.That(HitCount("RandomMutationIncompatible"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = string.Empty,
                };

                target.WaterRitualSkillPointHandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("SkillPointIntro"), Is.Zero);
                    Assert.That(HitCount("SkillPointGain"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);

        WithPatchedOwnerAndPopup(
            [
                nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
                nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent),
            ],
            popupMethod,
            () =>
            {
                var innerTarget = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = "{{G|Hortensa}} teaches you to craft {{W|spring-loaded boots}}.",
                };
                var outerTarget = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = "You gained {{C|50}} skill points!",
                    BeforePopup = () =>
                    {
                        innerTarget.WaterRitualTinkeringRecipeHandleEvent();

                        Assert.Multiple(() =>
                        {
                            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{G|Hortensa}}が{{W|spring-loaded boots}}の作り方を教えてくれた。"));
                            Assert.That(HitCount("TinkeringRecipe"), Is.EqualTo(1));
                            Assert.That(HitCount("SkillPointGain"), Is.Zero);
                        });
                    },
                };

                outerTarget.WaterRitualSkillPointHandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{C|50}}スキルポイントを得た！"));
                    Assert.That(HitCount("TinkeringRecipe"), Is.EqualTo(1));
                    Assert.That(HitCount("SkillPointGain"), Is.EqualTo(1));
                });
            });
    }

    private static void WithPatchedOwnerAndPopup(string methodName, string popupMethod, Action action)
    {
        WithPatchedOwnerAndPopup([methodName], popupMethod, action);
    }

    private static void WithPatchedOwnerAndPopup(string[] methodNames, string popupMethod, Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony, popupMethod);
            foreach (var methodName in methodNames)
            {
                harmony.Patch(
                    original: RequireOwnerMethod(methodName),
                    prefix: new HarmonyMethod(RequireMethod(typeof(WaterRitualPopupTranslationPatch), nameof(WaterRitualPopupTranslationPatch.Prefix))),
                    finalizer: new HarmonyMethod(RequireMethod(typeof(WaterRitualPopupTranslationPatch), nameof(WaterRitualPopupTranslationPatch.Finalizer))));
            }

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(string popupMethod, Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony, popupMethod);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopup(Harmony harmony, string popupMethod)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), popupMethod),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void InvokeOwnerMethod(DummyWaterRitualPopupProducerTarget target, string methodName)
    {
        var method = RequireOwnerMethod(methodName);
        var arguments = method.GetParameters().Length == 0 ? null : new object[] { "WaterRitualUse" };
        _ = method.Invoke(target, arguments);
    }

    private static void InvokePopup(string popupMethod, string source)
    {
        if (popupMethod == nameof(DummyPopupShow.ShowYesNoCancel))
        {
            _ = DummyPopupShow.ShowYesNoCancel(source);
            return;
        }

        if (popupMethod == nameof(DummyPopupShow.ShowFail))
        {
            DummyPopupShow.ShowFail(source);
            return;
        }

        DummyPopupShow.Show(source);
    }

    private static string? LastPopupMessage(string popupMethod)
    {
        return popupMethod == nameof(DummyPopupShow.ShowYesNoCancel)
            ? DummyPopupShow.LastShowYesNoCancelMessage
            : DummyPopupShow.LastShowMessage;
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(WaterRitualPopupTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyWaterRitualPopupProducerTarget), methodName);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string GetLocalizationRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Localization directory not found from test directory: {TestContext.CurrentContext.TestDirectory}");
    }

    private sealed class DummyWaterRitualPopupProducerTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

        public Action? BeforePopup { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualBeginHandleEvent()
        {
            EmitPopup(nameof(WaterRitualBeginHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualSkillPointHandleEvent()
        {
            EmitPopup(nameof(WaterRitualSkillPointHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualTinkeringRecipeHandleEvent()
        {
            EmitPopup(nameof(WaterRitualTinkeringRecipeHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualBuySecretHandleEvent()
        {
            EmitPopup(nameof(WaterRitualBuySecretHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WaterRitualBuySecretRevealEntry()
        {
            EmitPopup(nameof(WaterRitualBuySecretRevealEntry));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IWaterRitualPartUseReputation(string type = "WaterRitualUse")
        {
            _ = type;
            EmitPopup(nameof(IWaterRitualPartUseReputation));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WaterRitualPerformRitual()
        {
            EmitPopup(nameof(WaterRitualPerformRitual));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualBuyItemHandleEvent()
        {
            EmitPopup(nameof(WaterRitualBuyItemHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualGainMutationHandleEvent()
        {
            EmitPopup(nameof(WaterRitualGainMutationHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualRandomMutationHandleEvent()
        {
            EmitPopup(nameof(WaterRitualRandomMutationHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualJoinPartyHandleEvent()
        {
            EmitPopup(nameof(WaterRitualJoinPartyHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualNephilimPacifyTryGiveCircle()
        {
            EmitPopup(nameof(WaterRitualNephilimPacifyTryGiveCircle));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualSellSecretHandleEvent()
        {
            EmitPopup(nameof(WaterRitualSellSecretHandleEvent));
            return true;
        }

        private void EmitPopup(string route)
        {
            _ = route;
            BeforePopup?.Invoke();
            WaterRitualPopupTranslationPatchTests.InvokePopup(PopupMethod, PopupMessageToShow);
        }
    }
}
