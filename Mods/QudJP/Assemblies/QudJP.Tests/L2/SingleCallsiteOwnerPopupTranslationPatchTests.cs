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
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleAnimatorSpray),
        "It's a {{Y|spray-a-brain}}!",
        "{{Y|spray-a-brain}}だ！",
        "AnimatorSprayIdentified",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleAnimatorSpray),
        "You imbue {{Y|chair}} with life.",
        "{{Y|chair}}に命を吹き込んだ。",
        "AnimatorSprayImbueLife",
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
        Assert.That(TryTranslateForOwner(methodName, source, out var translated), Is.True);
        ShowPopup(translated, popupMethod);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(GetLastPopupMessage(popupMethod), Is.EqualTo(expected));
            Assert.That(HitCount(detail), Is.EqualTo(1));
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
        "It's a {{Y|spray-a-brain}}!",
        "AnimatorSprayIdentified",
        PopupMethod.Show)]
    [TestCase(
        "You imbue {{Y|chair}} with life.",
        "AnimatorSprayImbueLife",
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
        "It's a {{Y|spray-a-brain}}!",
        "AnimatorSprayIdentified")]
    [TestCase(
        "You imbue {{Y|chair}} with life.",
        "AnimatorSprayImbueLife")]
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
        Assert.That(
            TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), source, out var translated),
            Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount(detail), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish), marked, out var translated), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("BaetylRewardWish"), Is.Zero);
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), string.Empty, out var translated), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(string.Empty));
            Assert.That(HitCount("DecoyHologramOutOfRange"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotClaimFoodSicknessPopup_WhenFoodOwnerPatched()
    {
        const string source = "Ugh, you feel sick.";

        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood), source, out var translated), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("FoodConsumptionFrame"), Is.Zero);
        });
    }

    [TestCase("You are covered in sticky goop!")]
    [TestCase("It's a {{Y|fix-it spray foam}}!")]
    public void Patch_DoesNotClaimFixitSprayFixedOrRuntimePopups_WhenFixitSprayOwnerPatched(string source)
    {
        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray), source, out var translated), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("FixitSprayPhasePassThrough"), Is.Zero);
            Assert.That(HitCount("FixitSprayLiquidMix"), Is.Zero);
            Assert.That(HitCount("FixitSprayCovered"), Is.Zero);
        });
    }

    [TestCase("The sprayer head won't move.")]
    [TestCase("There's nothing viable to animate here.")]
    [TestCase("You can't animate an object that already has a brain.")]
    public void Patch_DoesNotClaimAnimatorSprayFixedPopups_WhenAnimatorSprayOwnerPatched(string source)
    {
        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.HandleAnimatorSpray), source, out var translated), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("AnimatorSprayIdentified"), Is.Zero);
            Assert.That(HitCount("AnimatorSprayImbueLife"), Is.Zero);
        });
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

    private static bool TryTranslateForOwner(string methodName, string source, out string translated)
    {
        return SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
            source,
            OwnerKeyForMethod(methodName),
            nameof(PopupShowTranslationPatch),
            "SingleCallsiteOwnerPopup",
            out translated);
    }

    private static string OwnerKeyForMethod(string methodName)
    {
        return methodName switch
        {
            nameof(DummySingleCallsiteOwnerPopupTarget.BarathrumStartConversation) =>
                "XRL.World.Quests.AscensionSystem|BarathrumStartConversation",
            nameof(DummySingleCallsiteOwnerPopupTarget.DisplaySurfaceDistribution) =>
                "XRL.World.Biomes.BiomeManager|DisplaySurfaceDistribution",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleBootEvent) =>
                "XRL.CharacterBuilds.Qud.QudSpecificCharacterInitModule|handleBootEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms) =>
                "XRL.World.Parts.DecoyHologramEmitter|CreateHolograms",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleBaetylRewardWish) =>
                "XRL.World.Parts.RandomAltarBaetyl|HandleBaetylRewardWish",
            nameof(DummySingleCallsiteOwnerPopupTarget.CastForceSuccess) =>
                "XRL.World.Parts.Skill.Axe_Dismember|CastForceSuccess",
            nameof(DummySingleCallsiteOwnerPopupTarget.Cast) =>
                "XRL.World.Parts.Skill.Cudgel_Slam|Cast",
            nameof(DummySingleCallsiteOwnerPopupTarget.AwardDynamicQuestRewardGameObject) =>
                "XRL.World.DynamicQuestRewardElement_GameObject|award",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleFactionEncounterWish) =>
                "XRL.World.ZoneBuilders.FactionEncounters|HandleFactionEncounterWish",
            nameof(DummySingleCallsiteOwnerPopupTarget.AttemptProselytization) =>
                "XRL.World.Parts.Skill.Persuasion_Proselytize|AttemptProselytization",
            nameof(DummySingleCallsiteOwnerPopupTarget.LearnNewRecipe) =>
                "XRL.World.Parts.Skill.Tinkering|LearnNewRecipe",
            nameof(DummySingleCallsiteOwnerPopupTarget.OnCreated) =>
                "XRL.World.Parts.GameUnique|OnCreated",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGenocideCurio) =>
                "XRL.World.Parts.GenocideCurio|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal) =>
                "XRL.World.Parts.GritGateMainframeTerminal|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc) =>
                "XRL.World.Parts.HindrenMysteryCriticalNPC|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.ReturnKindrishAward) =>
                "XRL.World.Parts.KindrishProperties|ReturnAward",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant) =>
                "XRL.World.Parts.LiquidFueledPowerPlant|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.ShowLooker) =>
                "XRL.UI.Look|ShowLooker",
            nameof(DummySingleCallsiteOwnerPopupTarget.MakeFuss) =>
                "XRL.World.Parts.MakeFussOnTaken|MakeFuss",
            nameof(DummySingleCallsiteOwnerPopupTarget.FireMutationPointsOnEat) =>
                "XRL.World.Parts.MutationPointsOnEat|FireEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.FireEngulfingDescends) =>
                "XRL.World.Parts.EngulfingDescends|FireEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.SetFactionRank) =>
                "XRL.World.Reputation|SetFactionRank",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleMarkovBook) =>
                "XRL.World.Parts.MarkovBook|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.FireMumblesInfection) =>
                "XRL.World.Parts.MumblesInfection|FireEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleToolboxBonus) =>
                "XRL.World.Parts.Toolbox|HandleBonus",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleRecoilOnDeath) =>
                "XRL.World.Parts.RecoilOnDeath|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpraybottle) =>
                "XRL.World.Parts.Spraybottle|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleFixitSpray) =>
                "XRL.World.Parts.FixitSpray|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleAnimatorSpray) =>
                "XRL.World.Parts.AnimatorSpray|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleSummoningCurio) =>
                "XRL.World.Parts.SummoningCurio|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood) =>
                "XRL.World.Parts.Food|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.ApplySpaceTimeVortex) =>
                "XRL.World.Parts.SpaceTimeVortex|ApplyVortex",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord) =>
                "XRL.World.Parts.WaterRitualRecord|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax) =>
                "XRL.World.QuestManagers.SpreadPax|Finish",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook) =>
                "XRL.World.Parts.TrainingBook|HandleEvent",
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unexpected owner method."),
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
