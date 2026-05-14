using System.Reflection;
using System.Text.RegularExpressions;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SingleCallsiteOwnerPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json"));
        DummyPopupShow.Reset();
        DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.BarathrumStartConversation),
        "Barathrum has left your party.",
        "Barathrumはパーティーを離れた",
        "AscensionBarathrumLeftParty",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.DisplaySurfaceDistribution),
        "No biome by name 'fungal' found.",
        "'fungal'という名前のバイオームは見つからない。",
        "BiomeNotFound",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleBootEvent),
        "Error creating player body. Unknown blueprint \"OddBody\"",
        "プレイヤーの体を作成できない。不明なブループリント「OddBody」。",
        "CharacterInitUnknownBlueprint",
        PopupMethod.ShowAsync)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms),
        "That is out of range (3 squares)",
        "範囲外だ（3マス）。",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms),
        "That is out of range (1 square)",
        "範囲外だ（1マス）。",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish),
        "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}",
        "{{C|oil}}の報酬として{{Y|folded carbide axe}}を生成した。",
        "BaetylRewardWish",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess),
        "Are you sure you want to dismember yourself?",
        "yourselfを切断してもよいか？",
        "AxeDismemberSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "Are you sure you want to slam yourself?",
        "yourselfを叩きつけてもよいか？",
        "CudgelSlamSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.AwardDynamicQuestRewardGameObject),
        "You receive {{Y|the copper nugget}}.",
        "{{Y|the copper nugget}}を受け取った。",
        "ReceiveObject",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleFactionEncounterWish),
        "No members found for 'snapjaws'.",
        "'snapjaws'のメンバーは見つからない。",
        "FactionEncounterNoMembers",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization),
        "Argyve is already your follower. Do you want to proselytize him anyway?",
        "Argyveはすでにあなたの仲間だ。それでも勧誘するか？",
        "ProselytizeFollowerConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe),
        "You have a flash of insight and scribe a {{Y|laser pistol schematic}}.",
        "ひらめきを得て{{Y|laser pistol schematic}}を記した。",
        "TinkeringLearnRecipe",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.OnCreated),
        "Argyve (NPC_Argyve) is considered unique, are you sure you want to create another?",
        "Argyve（NPC_Argyve）は一意とみなされています。もう1つ作成しますか？",
        "GameUniqueWishConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleGenocideCurio),
        "You activate {{Y|the chrome idol}} and toss it into the air.",
        "{{Y|the chrome idol}}を起動して空中に放り投げた。",
        "GenocideCurioActivation",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal),
        "The mainframe is unresponsive.",
        "The mainframeは反応しない。",
        "GritGateMainframeUnresponsive",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc),
        "The death of Kesehind means that the investigation can go no further.",
        "Kesehindの死により、調査はこれ以上進められなくなった。",
        "HindrenMysteryCriticalNpcDeath",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ReturnKindrishAward),
        "You receive {{Y|a force bracelet}}.",
        "{{Y|a force bracelet}}を受け取った。",
        "ReceiveObject",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ShowLooker),
        "12, 34: 56",
        "12, 34: ナビゲーション重み 56",
        "LookNavigationWeight",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant),
        "Your flamethrower has consumed all of its oil.",
        "あなたのflamethrowerはits oilをすべて消費した。",
        "LiquidFueledPowerPlantEmpty",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.MakeFuss),
        "You have found {{Y|Kindrish}}!",
        "{{Y|Kindrish}}を見つけた！",
        "MakeFussOnTaken",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat),
        "Your genome destabilizes and you gain 1 mutation point.",
        "ゲノムが不安定化し、変異ポイントを1得た。",
        "MutationPointsOnEat",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat),
        "Your genome destabilizes and you gain 3 mutation points.",
        "ゲノムが不安定化し、変異ポイントを3得た。",
        "MutationPointsOnEat",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FireEngulfingDescends),
        "the {{r|gelatinous wedge}}&y engulfing you melts through the floor! You fall to the level below.",
        "{{r|gelatinous wedge}}があなたを飲み込んだまま床を溶かして下っていった！ あなたは下の階層へ落ちた。",
        "EngulfingDescendsPassengerFall",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.SetFactionRank),
        "You are promoted to the Warden of the Barathrumites.",
        "あなたはthe BarathrumitesのWardenに昇進した。",
        "ReputationRankPromotion",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleMarkovBook),
        "You read one of the few legible excerpts from {{Y|Chrome Dreams}}:\n\n\"The chrome road remembers you.\"",
        "{{Y|Chrome Dreams}}から判読できる数少ない抜粋の1つを読んだ:\n\n「The chrome road remembers you.」",
        "MarkovBookExcerpt",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FireMumblesInfection),
        "The mouths on your skin begin to mumble coherently, revealing the wisdom of a trillion microbes:\n\nThe location of Red Rock",
        "肌の口がはっきりとつぶやき始め、一兆の微生物の叡智を明かした:\n\nThe location of Red Rock",
        "MumblesSecret",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleToolboxBonus),
        "Your toolbox is unpowered. Do you want to continue without its benefits?",
        "Your toolboxは電力が供給されていない。利点なしで続けますか？",
        "ToolboxInoperativeConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleToolboxBonus),
        "Your toolbox is still starting up. Do you want to continue without its full benefits?",
        "Your toolboxはまだ起動中だ。完全な利点なしで続けますか？",
        "ToolboxInoperativeConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleToolboxBonus),
        "Your toolbox is unpowered. Do you want to continue, using it without power?",
        "Your toolboxは電力が供給されていない。電力なしで使用して続けますか？",
        "ToolboxInoperativeConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant),
        "Your fuel cells have consumed all of their blood.",
        "あなたのfuel cellsはtheir bloodをすべて消費した。",
        "LiquidFueledPowerPlantEmpty",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath),
        "Just before your demise, you are transported to safety! {{Y|recoiler}} disintegrates.",
        "死の直前、あなたは安全な場所へ転送された！ {{Y|recoiler}}は崩壊した。",
        "RecoilOnDeathTransport",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath),
        "Just before your demise, you are transported to safety! {{Y|recoilers}} disintegrate.",
        "死の直前、あなたは安全な場所へ転送された！ {{Y|recoilers}}は崩壊した。",
        "RecoilOnDeathTransport",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle),
        "you are covered in slime!",
        "youはslimeに覆われた！",
        "SpraybottleCovered",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle),
        "{{Y|the snapjaw}} is covered in oil!",
        "{{Y|the snapjaw}}はoilに覆われた！",
        "SpraybottleCovered",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray),
        "Some sticky goop passes through {{Y|phase spider}}.",
        "ねばつく粘液が{{Y|phase spider}}を通り抜けた。",
        "FixitSprayPhasePassThrough",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray),
        "Some sticky goop mixes in with {{Y|oil pool}}.",
        "ねばつく粘液が{{Y|oil pool}}に混ざった。",
        "FixitSprayLiquidMix",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray),
        "{{Y|broken chair}} is covered in sticky goop!",
        "{{Y|broken chair}}はべとべとの粘液に覆われた！",
        "FixitSprayCovered",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSummoningCurio),
        "You activate the curio and toss it on the ground. It erupts into a throng of tiny polygons, which amalgamate into a fully formed {{Y|polygonal snapjaw}}.",
        "キュリオを起動して地面に投げた。小さなポリゴンの群れが噴出し、完全な形をした{{Y|polygonal snapjaw}}へと融合した。",
        "SummoningCurioActivation",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood),
        "You eat the {{Y|jerky}}.\n塩辛い。\nYou are now {{|{{g|Sated}}}} and {{|{{g|Quenched}}}}.",
        "{{Y|jerky}}を食べた。\n塩辛い。\n現在、{{|{{g|満腹}}}}、{{|{{g|潤っている}}}}だ。",
        "FoodConsumptionFrame",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood),
        "You eat {{Y|raw meat}}.\nYou are now {{|{{W|Hungry}}}} and {{|{{Y|Thirsty}}}}.",
        "{{Y|raw meat}}を食べた。\n現在、{{|{{W|空腹}}}}、{{|{{Y|喉が渇いた}}}}だ。",
        "FoodConsumptionFrame",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ApplySpaceTimeVortex),
        "Your companion, {{G|Q Girl}},has been sucked into the space-time vortex to the east!",
        "あなたの仲間である{{G|Q Girl}}は東側のspace-time vortexに吸い込まれた！",
        "SpaceTimeVortexCompanionSucked",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord),
        "You bothered {{G|Yurl}} again.",
        "{{G|Yurl}}にまた迷惑をかけた。",
        "WaterRitualRecordBothered",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax),
        "The infected crust of skin on your third arm loosens and breaks away.",
        "あなたのthird armの感染した皮殻が緩み、剥がれ落ちた。",
        "SpreadPaxCure",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook),
        "Your Strength is increased by {{G|1}}!",
        "あなたのStrengthが{{G|1}}上昇した！",
        "TrainingBookAttributeIncrease",
        PopupMethod.Show)]
    public void Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail,
        PopupMethod popupMethod)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(methodName),
            () =>
            {
                InvokeOwnerMethod(methodName, source);

                Assert.Multiple(() =>
                {
                    Assert.That(GetLastPopupMessage(popupMethod), Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [TestCase(
        "Barathrum has left your party.",
        "AscensionBarathrumLeftParty",
        PopupMethod.Show)]
    [TestCase(
        "No biome by name 'fungal' found.",
        "BiomeNotFound",
        PopupMethod.Show)]
    [TestCase(
        "Error creating player body. Unknown blueprint \"OddBody\"",
        "CharacterInitUnknownBlueprint",
        PopupMethod.ShowAsync)]
    [TestCase(
        "That is out of range (3 squares)",
        "DecoyHologramOutOfRange",
        PopupMethod.Show)]
    [TestCase(
        "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}",
        "BaetylRewardWish",
        PopupMethod.Show)]
    [TestCase(
        "Are you sure you want to dismember yourself?",
        "AxeDismemberSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "Are you sure you want to slam yourself?",
        "CudgelSlamSelfConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "You receive {{Y|the copper nugget}}.",
        "ReceiveObject",
        PopupMethod.Show)]
    [TestCase(
        "No members found for 'snapjaws'.",
        "FactionEncounterNoMembers",
        PopupMethod.Show)]
    [TestCase(
        "Argyve is already your follower. Do you want to proselytize him anyway?",
        "ProselytizeFollowerConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "You have a flash of insight and scribe a {{Y|laser pistol schematic}}.",
        "TinkeringLearnRecipe",
        PopupMethod.Show)]
    [TestCase(
        "Argyve (NPC_Argyve) is considered unique, are you sure you want to create another?",
        "GameUniqueWishConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "You activate {{Y|the chrome idol}} and toss it into the air.",
        "GenocideCurioActivation",
        PopupMethod.Show)]
    [TestCase(
        "The mainframe is unresponsive.",
        "GritGateMainframeUnresponsive",
        PopupMethod.Show)]
    [TestCase(
        "The death of Kesehind means that the investigation can go no further.",
        "HindrenMysteryCriticalNpcDeath",
        PopupMethod.Show)]
    [TestCase(
        "You receive {{Y|a force bracelet}}.",
        "ReceiveObject",
        PopupMethod.Show)]
    [TestCase(
        "12, 34: 56",
        "LookNavigationWeight",
        PopupMethod.Show)]
    [TestCase(
        "Your flamethrower has consumed all of its oil.",
        "LiquidFueledPowerPlantEmpty",
        PopupMethod.Show)]
    [TestCase(
        "You have found {{Y|Kindrish}}!",
        "MakeFussOnTaken",
        PopupMethod.Show)]
    [TestCase(
        "Your genome destabilizes and you gain 3 mutation points.",
        "MutationPointsOnEat",
        PopupMethod.Show)]
    [TestCase(
        "the {{r|gelatinous wedge}}&y engulfing you melts through the floor! You fall to the level below.",
        "EngulfingDescendsPassengerFall",
        PopupMethod.Show)]
    [TestCase(
        "You are promoted to the Warden of the Barathrumites.",
        "ReputationRankPromotion",
        PopupMethod.Show)]
    [TestCase(
        "You read one of the few legible excerpts from {{Y|Chrome Dreams}}:\n\n\"The chrome road remembers you.\"",
        "MarkovBookExcerpt",
        PopupMethod.Show)]
    [TestCase(
        "The mouths on your skin begin to mumble coherently, revealing the wisdom of a trillion microbes:\n\nThe location of Red Rock",
        "MumblesSecret",
        PopupMethod.Show)]
    [TestCase(
        "Your toolbox is unpowered. Do you want to continue without its benefits?",
        "ToolboxInoperativeConfirmation",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "Just before your demise, you are transported to safety! {{Y|recoiler}} disintegrates.",
        "RecoilOnDeathTransport",
        PopupMethod.Show)]
    [TestCase(
        "you are covered in slime!",
        "SpraybottleCovered",
        PopupMethod.Show)]
    [TestCase(
        "Some sticky goop passes through {{Y|phase spider}}.",
        "FixitSprayPhasePassThrough",
        PopupMethod.Show)]
    [TestCase(
        "Some sticky goop mixes in with {{Y|oil pool}}.",
        "FixitSprayLiquidMix",
        PopupMethod.Show)]
    [TestCase(
        "{{Y|broken chair}} is covered in sticky goop!",
        "FixitSprayCovered",
        PopupMethod.Show)]
    [TestCase(
        "You activate the curio and toss it on the ground. It erupts into a throng of tiny polygons, which amalgamate into a fully formed {{Y|polygonal snapjaw}}.",
        "SummoningCurioActivation",
        PopupMethod.Show)]
    [TestCase(
        "You eat the {{Y|jerky}}.\n塩辛い。\nYou are now {{|{{g|Sated}}}} and {{|{{g|Quenched}}}}.",
        "FoodConsumptionFrame",
        PopupMethod.Show)]
    [TestCase(
        "Your companion, {{G|Q Girl}},has been sucked into the space-time vortex to the east!",
        "SpaceTimeVortexCompanionSucked",
        PopupMethod.Show)]
    [TestCase(
        "You bothered {{G|Yurl}} again.",
        "WaterRitualRecordBothered",
        PopupMethod.Show)]
    [TestCase(
        "The infected crust of skin on your third arm loosens and breaks away.",
        "SpreadPaxCure",
        PopupMethod.Show)]
    [TestCase(
        "Your Strength is increased by {{G|1}}!",
        "TrainingBookAttributeIncrease",
        PopupMethod.Show)]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent(
        string source,
        string detail,
        PopupMethod popupMethod)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => ShowPopup(source, popupMethod));

        Assert.Multiple(() =>
        {
            Assert.That(GetLastPopupMessage(popupMethod), Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    [TestCase(
        "Barathrum has left your party.",
        "AscensionBarathrumLeftParty")]
    [TestCase(
        "No biome by name 'fungal' found.",
        "BiomeNotFound")]
    [TestCase(
        "Error creating player body. Unknown blueprint \"OddBody\"",
        "CharacterInitUnknownBlueprint")]
    [TestCase(
        "Argyve (NPC_Argyve) is considered unique, are you sure you want to create another?",
        "GameUniqueWishConfirmation")]
    [TestCase(
        "You activate {{Y|the chrome idol}} and toss it into the air.",
        "GenocideCurioActivation")]
    [TestCase(
        "The mainframe is unresponsive.",
        "GritGateMainframeUnresponsive")]
    [TestCase(
        "The death of Kesehind means that the investigation can go no further.",
        "HindrenMysteryCriticalNpcDeath")]
    [TestCase(
        "You receive {{Y|a force bracelet}}.",
        "ReceiveObject")]
    [TestCase(
        "No members found for 'snapjaws'.",
        "FactionEncounterNoMembers")]
    [TestCase(
        "12, 34: 56",
        "LookNavigationWeight")]
    [TestCase(
        "Your flamethrower has consumed all of its oil.",
        "LiquidFueledPowerPlantEmpty")]
    [TestCase(
        "You have found {{Y|Kindrish}}!",
        "MakeFussOnTaken")]
    [TestCase(
        "Your genome destabilizes and you gain 3 mutation points.",
        "MutationPointsOnEat")]
    [TestCase(
        "the {{r|gelatinous wedge}}&y engulfing you melts through the floor! You fall to the level below.",
        "EngulfingDescendsPassengerFall")]
    [TestCase(
        "You are promoted to the Warden of the Barathrumites.",
        "ReputationRankPromotion")]
    [TestCase(
        "You read one of the few legible excerpts from {{Y|Chrome Dreams}}:\n\n\"The chrome road remembers you.\"",
        "MarkovBookExcerpt")]
    [TestCase(
        "The mouths on your skin begin to mumble coherently, revealing the wisdom of a trillion microbes:\n\nThe location of Red Rock",
        "MumblesSecret")]
    [TestCase(
        "Your toolbox is unpowered. Do you want to continue without its benefits?",
        "ToolboxInoperativeConfirmation")]
    [TestCase(
        "Just before your demise, you are transported to safety! {{Y|recoiler}} disintegrates.",
        "RecoilOnDeathTransport")]
    [TestCase(
        "you are covered in slime!",
        "SpraybottleCovered")]
    [TestCase(
        "Some sticky goop passes through {{Y|phase spider}}.",
        "FixitSprayPhasePassThrough")]
    [TestCase(
        "Some sticky goop mixes in with {{Y|oil pool}}.",
        "FixitSprayLiquidMix")]
    [TestCase(
        "{{Y|broken chair}} is covered in sticky goop!",
        "FixitSprayCovered")]
    [TestCase(
        "You activate the curio and toss it on the ground. It erupts into a throng of tiny polygons, which amalgamate into a fully formed {{Y|polygonal snapjaw}}.",
        "SummoningCurioActivation")]
    [TestCase(
        "You eat the {{Y|jerky}}.\n塩辛い。\nYou are now {{|{{g|Sated}}}} and {{|{{g|Quenched}}}}.",
        "FoodConsumptionFrame")]
    [TestCase(
        "Your companion, {{G|Q Girl}},has been sucked into the space-time vortex to the east!",
        "SpaceTimeVortexCompanionSucked")]
    [TestCase(
        "You bothered {{G|Yurl}} again.",
        "WaterRitualRecordBothered")]
    [TestCase(
        "The infected crust of skin on your third arm loosens and breaks away.",
        "SpreadPaxCure")]
    [TestCase(
        "Your Strength is increased by {{G|1}}!",
        "TrainingBookAttributeIncrease")]
    public void Patch_DoesNotTranslatePopupUnderWrongSingleCallsiteOwner(
        string source,
        string detail)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount(detail), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish), marked);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("BaetylRewardWish"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), string.Empty);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                    Assert.That(HitCount("DecoyHologramOutOfRange"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_DoesNotClaimFoodSicknessPopup_WhenFoodOwnerPatched()
    {
        const string source = "Ugh, you feel sick.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood), source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("FoodConsumptionFrame"), Is.Zero);
                });
            });
    }

    [TestCase("You are covered in sticky goop!")]
    [TestCase("It's a {{Y|fix-it spray foam}}!")]
    public void Patch_DoesNotClaimFixitSprayFixedOrRuntimePopups_WhenFixitSprayOwnerPatched(string source)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SingleCallsiteOwnerPopupTranslationPatch),
            RequireOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray)),
            () =>
            {
                InvokeOwnerMethod(nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray), source);

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("FixitSprayPhasePassThrough"), Is.Zero);
                    Assert.That(HitCount("FixitSprayLiquidMix"), Is.Zero);
                    Assert.That(HitCount("FixitSprayCovered"), Is.Zero);
                });
            });
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return methodName switch
        {
            nameof(DummySingleCallsiteOwnerPopupTarget.BarathrumStartConversation) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.DisplaySurfaceDistribution) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleBootEvent) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string),
                    typeof(DummyXrlGame),
                    typeof(DummyEmbarkInfo),
                    typeof(object)),
            nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(DummyAxeDismember),
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.Cast) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(DummyCudgelSlam),
                    typeof(string),
                    typeof(DummyGameObject),
                    typeof(bool),
                    typeof(int),
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.AwardDynamicQuestRewardGameObject) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleFactionEncounterWish) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(Match)),
            nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName),
            nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject),
                    typeof(int),
                    typeof(int)),
            nameof(DummySingleCallsiteOwnerPopupTarget.OnCreated) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGenocideCurio) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleSummoningCurio) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood) or
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyInventoryActionEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyBeforeDeathRemovalEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.ReturnKindrishAward) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyEndTurnEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.MakeFuss) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.FireEngulfingDescends) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.ApplySpaceTimeVortex) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGameObject)),
            nameof(DummySingleCallsiteOwnerPopupTarget.SetFactionRank) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(bool)),
            nameof(DummySingleCallsiteOwnerPopupTarget.ShowLooker) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(int),
                    typeof(int),
                    typeof(int)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyBeforeDieEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleMarkovBook) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyInventoryActionEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.FireMumblesInfection) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleToolboxBonus) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyGetTinkeringBonusEvent),
                    typeof(int),
                    typeof(int)),
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName,
                    typeof(DummyBeginConversationEvent)),
            nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax) =>
                OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySingleCallsiteOwnerPopupTarget),
                    methodName),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method."),
        };
    }

    private static void InvokeOwnerMethod(string methodName, string message)
    {
        switch (methodName)
        {
            case nameof(DummySingleCallsiteOwnerPopupTarget.BarathrumStartConversation):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.BarathrumStartConversation(new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.DisplaySurfaceDistribution):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                DummySingleCallsiteOwnerPopupTarget.DisplaySurfaceDistribution("fungal");
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleBootEvent):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleBootEvent("BeforeBoot", new DummyXrlGame(), new DummyEmbarkInfo());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.CreateHolograms(new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish("@Melee Weapons {tier}R");
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.CastForceSuccess(
                    new DummyGameObject(),
                    new DummyAxeDismember(),
                    new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.Cast):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.Cast(
                    new DummyGameObject(),
                    new DummyCudgelSlam(),
                    null,
                    new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.AwardDynamicQuestRewardGameObject):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.AwardDynamicQuestRewardGameObject();
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleFactionEncounterWish):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.HandleFactionEncounterWish(
                    Regex.Match("factionencounter:snapjaws", "^factionencounter:(.*)$"));
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.AttemptProselytization();
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe(new DummyGameObject(), 1, 4);
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.OnCreated):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.OnCreated("Wish");
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleGenocideCurio):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleGenocideCurio(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleGritGateMainframeTerminal(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleHindrenMysteryCriticalNpc(new DummyBeforeDeathRemovalEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.ReturnKindrishAward):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.ReturnKindrishAward();
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleLiquidFueledPowerPlant(new DummyEndTurnEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.MakeFuss):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.MakeFuss(new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.FireMutationPointsOnEat(new DummyEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.FireEngulfingDescends):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.FireEngulfingDescends(new DummyEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.SetFactionRank):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.SetFactionRank("Barathrumites", "Warden", message: true);
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.ShowLooker):
                DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = message;
                _ = DummySingleCallsiteOwnerPopupTarget.ShowLooker(80, 12, 34);
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleRecoilOnDeath(new DummyBeforeDieEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleMarkovBook):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleMarkovBook(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.FireMumblesInfection):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.FireMumblesInfection(new DummyEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleSpraybottle(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleFixitSpray(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleSummoningCurio):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleSummoningCurio(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleFood(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.ApplySpaceTimeVortex):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.ApplySpaceTimeVortex(new DummyGameObject());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleTrainingBook(new DummyInventoryActionEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleToolboxBonus):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleToolboxBonus(new DummyGetTinkeringBonusEvent(), 2, 0);
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.HandleWaterRitualRecord(new DummyBeginConversationEvent());
                break;
            case nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax):
                new DummySingleCallsiteOwnerPopupTarget
                {
                    PopupMessageToShow = message,
                }.FinishSpreadPax();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method.");
        }
    }

    private static void ShowPopup(string source, PopupMethod popupMethod)
    {
        if (popupMethod == PopupMethod.ShowAsync)
        {
            DummyPopupShow.ShowAsync(source).GetAwaiter().GetResult();
            return;
        }

        if (popupMethod == PopupMethod.ShowYesNo)
        {
            _ = DummyPopupShow.ShowYesNo(source);
            return;
        }

        DummyPopupShow.Show(source);
    }

    private static string? GetLastPopupMessage(PopupMethod popupMethod)
    {
        return popupMethod switch
        {
            PopupMethod.ShowAsync => DummyPopupShow.LastShowAsyncMessage,
            PopupMethod.ShowYesNo => DummyPopupShow.LastShowYesNoMessage,
            _ => DummyPopupShow.LastShowMessage,
        };
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(SingleCallsiteOwnerPopupTranslationPatch), detail);
    }

    public enum PopupMethod
    {
        Show,
        ShowAsync,
        ShowYesNo,
    }
}
