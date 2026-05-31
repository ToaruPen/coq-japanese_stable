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
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(GetLocalizationRoot(), "Dictionaries"));
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(Path.Combine(
            GetLocalizationRoot(),
            "MessageFrames",
            "verbs.ja.json"));
        DummyPopupShow.Reset();
        DummySingleCallsiteOwnerPopupTarget.StaticPopupMessageToShow = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
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
        nameof(DummySingleCallsiteOwnerPopupTarget.CastDismember),
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
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "You cannot slam {{Y|the phase spider}}.",
        "{{Y|phase spider}}を叩きつけられない。",
        "CudgelSlamCannotSlam",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "You cannot reach {{Y|the phase spider}} to slam it.",
        "{{Y|phase spider}}に手が届かず、叩きつけられない。",
        "CudgelSlamCannotReach",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "There's nothing there to slam.",
        "そこには叩きつけるものがない。",
        "CudgelSlamNothingThere",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "You have no weapon!",
        "武器を持っていない！",
        "CudgelSlamNoWeapon",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "You aren't strong enough to slam through {{Y|the granite wall}}.",
        "{{Y|granite wall}}を叩き壊すには力が足りない。",
        "CudgelSlamNotStrongEnough",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Cast),
        "{{Y|The door}} are open.",
        "{{Y|door}}は開いている。",
        "CudgelSlamTargetOpen",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSubmersionCommand),
        "{{B|the brackish pool}} is too shallow for you to submerge in.",
        "{{B|the brackish pool}}は浅すぎて潜れない。",
        "SubmersionTooShallow",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Recharge),
        "You have partially recharged {{Y|the chem cell}}.",
        "{{Y|ケムセル}}を部分的に充電した。",
        "TinkeringRechargeSuccess",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Recharge),
        "You have recharged {{Y|the chem cell}}.",
        "{{Y|ケムセル}}を充電した。",
        "TinkeringRechargeSuccess",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.Recharge),
        "{{Y|The chem cell}} can't be recharged that way.",
        "{{Y|ケムセル}}はその方法では充電できない。",
        "TinkeringRechargeCannot",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.AttemptOpenContainer),
        "You cannot trade with {{Y|the snapjaw scavenger}}.",
        "{{Y|the snapjaw scavenger}}とは取引できない。",
        "ContainerCannotTrade",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.AttemptOpenContainer),
        "There's nothing in that. Would you like to store an item?",
        "その中には何も入っていない。アイテムを預けるか？",
        "ContainerEmptyStore",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.AttemptOpenContainer),
        "There's nothing on that. Would you like to store an item?",
        "そこには何も置かれていない。アイテムを預けるか？",
        "ContainerEmptyStore",
        PopupMethod.Show)]
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
        "Argyveはすでにあなたの仲間だ。それでも勧誘しますか？",
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
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpiralBorerCurio),
        "The metal satchel opens, folds itself inside out, and transforms into a contraption studded with pinions and drills. It starts to burrow into the ground.",
        "金属製のバッグが開いて裏返り、ピニオンとドリルが突き出た装置へ変形した。それは地面に穴を掘り始めた。",
        "SpiralBorerCurioActivation",
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
        nameof(DummySingleCallsiteOwnerPopupTarget.WishModify),
        "No modification by the name 'freezing' could be found.",
        "'freezing'という改造は見つからない。",
        "IModificationMissingModification",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishModify),
        "No blueprint by the name 'chrome chair' could be found.",
        "'chrome chair'というブループリントは見つからない。",
        "IModificationMissingBlueprint",
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
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleNeutronFluxPourExplodesEvent),
        "There's no magnetic containment inside {{Y|the glass bottle}}. Pour anyway?",
        "{{Y|the glass bottle}}の中には磁気封じ込めがない。それでも注ぐか？",
        "NeutronFluxNoContainment",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleNeutronFluxBeginTakeActionEvent),
        "{{Y|The flask}} beeps loudly and flashes a warning glyph. Do you want to stop travelling?",
        "{{Y|The flask}}が大きくビープ音を鳴らし、警告グリフを点滅させる。移動をやめますか？",
        "NeutronFluxWarningGlyph",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandlePolygelInventoryActionEvent),
        "It's a {{Y|metamorphic polygel}}!",
        "{{Y|metamorphic polygel}}だ！",
        "PolygelIdentified",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandlePolygelInventoryActionEvent),
        "The polygel morphs into another {{Y|phase cannon}}!",
        "ポリジェルがもう1つの{{Y|phase cannon}}へと変形した！",
        "PolygelMorphs",
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
        "あなたはバラサラム派の監視官に昇進した。",
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
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleFood),
        "You eat your {{R|恋人の花}}.\nThat hits the spot!\nYou are now {{|Sated}} and {{|Quenched}}.",
        "{{R|恋人の花}}を食べた。\nおいしく腹に収まった！\n現在、{{|満腹}}、{{|潤っている}}だ。",
        "FoodConsumptionFrame",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ApplySpaceTimeVortex),
        "Your companion, {{G|Q Girl}},has been sucked into the space-time vortex to the east!",
        "あなたの仲間である{{G|Q Girl}}は東側のspace-time vortexに吸い込まれた！",
        "SpaceTimeVortexCompanionSucked",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.CheckPullDown),
        "Your companion, {{G|Q Girl}},has fallen down {{Y|the deep shaft}} to the east!",
        "あなたの仲間である{{G|Q Girl}}は東側にある{{Y|the deep shaft}}の下へ落ちた！",
        "StairsDownCompanionFell",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ProcessTargetedMove),
        "Do you really want to attack {{Y|the snapjaw}}?",
        "本当に{{Y|スナップジョー}}を攻撃しますか？",
        "PhysicsAttackConfirm",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ProcessTargetedMove),
        "{{W|Do you really want to attack the ウォーターヴァイン農家?}}",
        "{{W|本当にウォーターヴァイン農家を攻撃しますか？}}",
        "PhysicsAttackConfirm",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ShowScriptCallToArmsWarning),
        "Otho yells, '{{W|Argyve! Come back here!}}'",
        "オソが叫ぶ。「{{W|Argyve！戻ってこい！}}」",
        "ScriptCallToArmsOthoYells",
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
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleDestroyOnUnequip),
        "{{Y|The light mote}} will be destroyed if it is unequipped. Do you want to continue?",
        "{{Y|The light mote}}は外すと破壊される。続けますか？",
        "DestroyOnUnequipConfirmation",
        PopupMethod.ShowYesNoCancel)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleMagnetizedApplicator),
        "{{Y|The geomagnetic disc}} loses its magnetic charge and crumbles to powder.",
        "{{Y|The geomagnetic disc}}は磁荷を失い、粉々に崩れた。",
        "MagnetizedApplicatorCrumbles",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleCursedCellSocket),
        "{{Y|The chem cell}} locks firmly into the socket, preventing removal.",
        "{{Y|The chem cell}}はソケットにしっかりとはまり、取り外せなくなった。",
        "CursedCellSocketLocks",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleNephalPropertiesBeforeDeathRemoval),
        "A sphere of light in the chord of Saad Amus radiates away.\n\nYou feel it absorbed elsewhere.",
        "Saad Amusの調べの光球が放射されて消えた。\n\nそれがどこか別の場所に吸収されたのを感じた。",
        "NephalPropertiesChordAbsorbed",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishMutation),
        "Did you mean Light Manipulation?",
        "「Light Manipulation」のことか？",
        "MutationWishDidYouMean",
        PopupMethod.ShowYesNo)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishMutation),
        "No mutation by the name 'Light Manipulations' could be found.",
        "「Light Manipulations」という変異は見つからない。",
        "MutationWishMissingName",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishMutation),
        "No mutation by the name 'Wings' and variant 'crystal' could be found.",
        "「Wings」のバリアント「crystal」という変異は見つからない。",
        "MutationWishMissingVariant",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleBlueprintXML),
        "No blueprint named \"Chrome Idol\" found.",
        "「Chrome Idol」というブループリントは見つからない。",
        "GameObjectFactoryMissingBlueprint",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.LoadGame),
        "No saved game exists. (Saves/slot1)",
        "セーブデータが存在しない。（Saves/slot1）",
        "XrlGameMissingSave",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishGeneratePopulation),
        "'abc' is not a valid integer.",
        "'abc'は有効な整数ではない。",
        "PopulationManagerInvalidCount",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishGeneratePopulation),
        "No table by the name 'JoppaVillagers' could be resolved.",
        "'JoppaVillagers'という名前の population table は解決できない。",
        "PopulationManagerMissingTable",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.TransitToThinWorld),
        "The colossal lid slams shut. Darkness engulfs you.",
        "巨大な蓋が閉じた。闇があなたを飲み込んだ。",
        "ThinWorldLidSlams",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.TransitToThinWorld),
        "You died.\n\nEntombed in the burial chamber of Resheph, the Last Sultan.",
        "あなたは死んだ。\n\n最後のスルタン、レシェフの埋葬室に葬られた。",
        "ThinWorldEntombed",
        PopupMethod.ShowSpace)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandlePlayerMuralEndTurn),
        "Herododicus says '&WI'm finished, Moloch! Praise Hosh Resheph, all who canter in this House!&Y'",
        "ヘロドディクスが言う。「&W終わりました、モロク！ Hosh・レシェフを讃えよ、この館を駆ける者たちよ！&Y」",
        "PlayerMuralReshephDisguiseDone",
        PopupMethod.Show)]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandlePlayerMuralEndTurn),
        "Herododicus says '&WI'm done!&Y'",
        "ヘロドディクスが言う。「&W終わった！&Y」",
        "PlayerMuralDone",
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
        "XRL.World.Parts.Skill.Tactics_DeathFromAbove|PerformDeathFromAbove",
        "You cannot perform Death From Above on the world map.",
        "ワールドマップ上ではデス・フロム・アバブを実行できない。",
        "DeathFromAboveWorldMap")]
    [TestCase(
        "XRL.World.Parts.Skill.Tactics_DeathFromAbove|PerformDeathFromAbove",
        "To perform Death From Above from the ground, you must select a target at least two squares away.",
        "地上からデス・フロム・アバブを実行するには、少なくとも2マス離れた対象を選ぶ必要がある。",
        "DeathFromAboveRange")]
    [TestCase(
        "XRL.World.Parts.Skill.Tactics_DeathFromAbove|PerformDeathFromAbove",
        "You cannot perform Death From Above on {{Y|the snapjaw}}.",
        "{{Y|スナップジョー}}にはデス・フロム・アバブを実行できない。",
        "DeathFromAboveInvalidTarget")]
    [TestCase(
        "XRL.World.Parts.Skill.Tactics_DeathFromAbove|PerformDeathFromAbove",
        "You cannot perform Death From Above on {{Y|the obsidian idol}}.",
        "{{Y|obsidian idol}}にはデス・フロム・アバブを実行できない。",
        "DeathFromAboveInvalidTarget")]
    [TestCase(
        "XRL.World.Parts.Skill.Tactics_Charge|PerformCharge",
        "You can't charge more than three spaces.",
        "3マスを超えて突撃することはできない。",
        "ChargeRange")]
    [TestCase(
        "XRL.World.Parts.Skill.Tactics_Juke|HandleEvent",
        "You cannot juke both {{Y|the snapjaw}} and {{R|the snapjaw brute}} out of your way.",
        "{{Y|スナップジョー}}と{{R|スナップジョーの暴漢}}の両方を押しのけてジュークすることはできない。",
        "JukeBothTargetsBlocked")]
    [TestCase(
        "XRL.World.Parts.Skill.Axe_HookAndDrag|FireEvent",
        "You must have an axe equipped in your primary hand to use Hook and Drag.",
        "フック＆ドラッグを使うには主手に斧を装備していなければならない。",
        "AxeHookAndDragNeedAxe")]
    [TestCase(
        "XRL.World.Parts.Skill.Persuasion_Proselytize|AttemptProselytization",
        "Without a tongue, you cannot proselytize {{Y|the snapjaw}}, as you cannot make telepathic contact with them.",
        "舌がないため、{{Y|スナップジョー}}とテレパシー接触できず、勧誘できない。",
        "ProselytizeNoTongueContact")]
    [TestCase(
        "XRL.World.Parts.Skill.Persuasion_Proselytize|Proselytize",
        "{{Y|The snapjaw}} is unconvinced by your pleas.",
        "{{Y|スナップジョー}}はあなたの嘆願に心を動かされない。",
        "ProselytizeUnconvinced")]
    [TestCase(
        "XRL.World.Parts.Skill.Cudgel_Slam|FireEvent",
        "You must have a cudgel equipped in order to use slam.",
        "叩きつけを使うにはこん棒を装備していなければならない。",
        "CudgelSlamNeedCudgel")]
    [TestCase(
        "XRL.World.Parts.Skill.Tinkering_Tinker1|Recharge",
        "You don't have any {{C|A}} bits, which are required for recharging.",
        "{{C|A}}ビットがない。充電にはそれが必要だ。",
        "TinkeringRechargeNoBits")]
    [TestCase(
        "XRL.World.Parts.Skill.Tinkering_Tinker1|Recharge",
        "It would take {{C|3}} {{C|A}} bits to fully recharge {{Y|the chem cell}}. You have {{C|1}}. How many do you want to use?",
        "{{Y|ケムセル}}を完全に充電するには{{C|3}}個の{{C|A}}ビットが必要だ。所持数は{{C|1}}。いくつ使う？",
        "TinkeringRechargeAskNumber")]
    [TestCase(
        "XRL.World.Parts.Skill.Tinkering_Tinker1|Recharge",
        "It would take {{C|1}} {{R|A}} bit to fully recharge your {{c|ケムセル}}. You have {{C|51}}. How many do you want to use?",
        "{{c|ケムセル}}を完全に充電するには{{C|1}}個の{{R|A}}ビットが必要だ。所持数は{{C|51}}。いくつ使う？",
        "TinkeringRechargeAskNumber")]
    [TestCase(
        "XRL.World.Parts.Skill.Tinkering_Tinker1|FireEvent",
        "You have no items that require charging.",
        "充電が必要なアイテムがない。",
        "TinkeringRechargeNoItems")]
    public void Patch_TranslatesIssue747SkillOwnerPopups_ByOwnerKey(
        string ownerKey,
        string source,
        string expected,
        string detail)
    {
        Assert.That(
            SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
                source,
                ownerKey,
                nameof(PopupShowTranslationPatch),
                "SingleCallsiteOwnerPopup",
                out var translated),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(HitCount(detail), Is.EqualTo(1));
        });
    }

    [Test]
    public void ProselytizeUnconvinced_StripsArticleBeforeDirectMarkedDisplayName()
    {
        var source = "The "
            + MessageFrameTranslator.MarkDirectTranslation("ウォーターヴァイン農家のメカニマス教徒改宗者")
            + " is unconvinced by your pleas.";

        Assert.That(
            SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessageForOwnerKey(
                source,
                "XRL.World.Parts.Skill.Persuasion_Proselytize|Proselytize",
                nameof(PopupShowTranslationPatch),
                "SingleCallsiteOwnerPopup",
                out var translated),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家のメカニマス教徒改宗者はあなたの嘆願に心を動かされない。"));
            Assert.That(HitCount("ProselytizeUnconvinced"), Is.EqualTo(1));
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
        "{{B|the brackish pool}} is too shallow for you to submerge in.",
        "SubmersionTooShallow",
        PopupMethod.Show)]
    [TestCase(
        "You have partially recharged {{Y|the chem cell}}.",
        "TinkeringRechargeSuccess",
        PopupMethod.Show)]
    [TestCase(
        "{{Y|The chem cell}} can't be recharged that way.",
        "TinkeringRechargeCannot",
        PopupMethod.Show)]
    [TestCase(
        "You cannot trade with {{Y|the snapjaw scavenger}}.",
        "ContainerCannotTrade",
        PopupMethod.Show)]
    [TestCase(
        "There's nothing in that. Would you like to store an item?",
        "ContainerEmptyStore",
        PopupMethod.Show)]
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
        "The metal satchel opens, folds itself inside out, and transforms into a contraption studded with pinions and drills. It starts to burrow into the ground.",
        "SpiralBorerCurioActivation",
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
        "No modification by the name 'freezing' could be found.",
        "IModificationMissingModification",
        PopupMethod.Show)]
    [TestCase(
        "No blueprint by the name 'chrome chair' could be found.",
        "IModificationMissingBlueprint",
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
        "There's no magnetic containment inside {{Y|the glass bottle}}. Pour anyway?",
        "NeutronFluxNoContainment",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "{{Y|The flask}} beeps loudly and flashes a warning glyph. Do you want to stop travelling?",
        "NeutronFluxWarningGlyph",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "It's a {{Y|metamorphic polygel}}!",
        "PolygelIdentified",
        PopupMethod.Show)]
    [TestCase(
        "The polygel morphs into another {{Y|phase cannon}}!",
        "PolygelMorphs",
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
    [TestCase(
        "{{Y|The light mote}} will be destroyed if it is unequipped. Do you want to continue?",
        "DestroyOnUnequipConfirmation",
        PopupMethod.ShowYesNoCancel)]
    [TestCase(
        "{{Y|The geomagnetic disc}} loses its magnetic charge and crumbles to powder.",
        "MagnetizedApplicatorCrumbles",
        PopupMethod.Show)]
    [TestCase(
        "{{Y|The chem cell}} locks firmly into the socket, preventing removal.",
        "CursedCellSocketLocks",
        PopupMethod.Show)]
    [TestCase(
        "A sphere of light in the chord of Saad Amus radiates away.\n\nYou feel it absorbed elsewhere.",
        "NephalPropertiesChordAbsorbed",
        PopupMethod.Show)]
    [TestCase(
        "Did you mean Light Manipulation?",
        "MutationWishDidYouMean",
        PopupMethod.ShowYesNo)]
    [TestCase(
        "No mutation by the name 'Light Manipulations' could be found.",
        "MutationWishMissingName",
        PopupMethod.Show)]
    [TestCase(
        "No mutation by the name 'Wings' and variant 'crystal' could be found.",
        "MutationWishMissingVariant",
        PopupMethod.Show)]
    [TestCase(
        "No blueprint named \"Chrome Idol\" found.",
        "GameObjectFactoryMissingBlueprint",
        PopupMethod.Show)]
    [TestCase(
        "No saved game exists. (Saves/slot1)",
        "XrlGameMissingSave",
        PopupMethod.Show)]
    [TestCase(
        "'abc' is not a valid integer.",
        "PopulationManagerInvalidCount",
        PopupMethod.Show)]
    [TestCase(
        "No table by the name 'JoppaVillagers' could be resolved.",
        "PopulationManagerMissingTable",
        PopupMethod.Show)]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent(
        string source,
        string detail,
        PopupMethod popupMethod)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => ShowPopup(source, popupMethod));

        Assert.That(HitCount(detail), Is.Zero);
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
        "No modification by the name 'freezing' could be found.",
        "IModificationMissingModification")]
    [TestCase(
        "No blueprint by the name 'chrome chair' could be found.",
        "IModificationMissingBlueprint")]
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
        "There's no magnetic containment inside {{Y|the glass bottle}}. Pour anyway?",
        "NeutronFluxNoContainment")]
    [TestCase(
        "{{Y|The flask}} beeps loudly and flashes a warning glyph. Do you want to stop travelling?",
        "NeutronFluxWarningGlyph")]
    [TestCase(
        "It's a {{Y|metamorphic polygel}}!",
        "PolygelIdentified")]
    [TestCase(
        "The polygel morphs into another {{Y|phase cannon}}!",
        "PolygelMorphs")]
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
        "The metal satchel opens, folds itself inside out, and transforms into a contraption studded with pinions and drills. It starts to burrow into the ground.",
        "SpiralBorerCurioActivation")]
    [TestCase(
        "You eat the {{Y|jerky}}.\n塩辛い。\nYou are now {{|{{g|Sated}}}} and {{|{{g|Quenched}}}}.",
        "FoodConsumptionFrame")]
    [TestCase(
        "Your companion, {{G|Q Girl}},has been sucked into the space-time vortex to the east!",
        "SpaceTimeVortexCompanionSucked")]
    [TestCase(
        "Your companion, {{G|Q Girl}},has fallen down {{Y|the deep shaft}} to the east!",
        "StairsDownCompanionFell")]
    [TestCase(
        "Do you really want to attack {{Y|the snapjaw}}?",
        "PhysicsAttackConfirm")]
    [TestCase(
        "Otho yells, '{{W|Argyve! Come back here!}}'",
        "ScriptCallToArmsOthoYells")]
    [TestCase(
        "You bothered {{G|Yurl}} again.",
        "WaterRitualRecordBothered")]
    [TestCase(
        "The infected crust of skin on your third arm loosens and breaks away.",
        "SpreadPaxCure")]
    [TestCase(
        "Your Strength is increased by {{G|1}}!",
        "TrainingBookAttributeIncrease")]
    [TestCase(
        "{{Y|The light mote}} will be destroyed if it is unequipped. Do you want to continue?",
        "DestroyOnUnequipConfirmation")]
    [TestCase(
        "{{Y|The geomagnetic disc}} loses its magnetic charge and crumbles to powder.",
        "MagnetizedApplicatorCrumbles")]
    [TestCase(
        "{{Y|The chem cell}} locks firmly into the socket, preventing removal.",
        "CursedCellSocketLocks")]
    [TestCase(
        "A sphere of light in the chord of Saad Amus radiates away.\n\nYou feel it absorbed elsewhere.",
        "NephalPropertiesChordAbsorbed")]
    [TestCase(
        "Did you mean Light Manipulation?",
        "MutationWishDidYouMean")]
    [TestCase(
        "No mutation by the name 'Light Manipulations' could be found.",
        "MutationWishMissingName")]
    [TestCase(
        "No mutation by the name 'Wings' and variant 'crystal' could be found.",
        "MutationWishMissingVariant")]
    [TestCase(
        "No blueprint named \"Chrome Idol\" found.",
        "GameObjectFactoryMissingBlueprint")]
    [TestCase(
        "No saved game exists. (Saves/slot1)",
        "XrlGameMissingSave")]
    [TestCase(
        "'abc' is not a valid integer.",
        "PopulationManagerInvalidCount")]
    [TestCase(
        "No table by the name 'JoppaVillagers' could be resolved.",
        "PopulationManagerMissingTable")]
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

    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpiralBorerCurio),
        "The metal satchel opens, folds itself inside out, and transforms into a contraption studded with pinions and drills. It starts to burrow into the ground.",
        "SpiralBorerCurioActivation")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishMutation),
        "No mutation by the name 'Light Manipulations' could be found.",
        "MutationWishMissingName")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishMutation),
        "No mutation by the name 'Wings' and variant 'crystal' could be found.",
        "MutationWishMissingVariant")]
    public void Patch_DoesNotRetranslateDirectMarkedNewFamilyPopup_WhenOwnerPatched(
        string methodName,
        string unmarked,
        string detail)
    {
        Assert.That(
            TryTranslateForOwner(methodName, MessageFrameTranslator.MarkDirectTranslation(unmarked), out var translated),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(unmarked));
            Assert.That(HitCount(detail), Is.Zero);
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

    [Test]
    public void Patch_TranslatesGivesRepWaterRitualCursePrefixAndPreservesAppendedLines_WhenOwnerPatched()
    {
        const string appendedLine = "Your reputation with the Fellowship of Wardens decreases by 100.";
        const string source =
            "You violated the covenant of the water ritual and killed your bonded kith. You are cursed.\n\n"
            + appendedLine;

        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.HandleGivesRepBeforeDeathRemoval), source, out var translated), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(
                translated,
                Is.EqualTo("水の契りを破り、結んだ仲間を殺した。あなたは呪われている。\n\n" + appendedLine));
            Assert.That(translated, Does.Not.Contain("You violated the covenant of the water ritual"));
            Assert.That(translated, Does.Contain(appendedLine));
            Assert.That(HitCount("GivesRepWaterRitualCurse"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedGivesRepWaterRitualCurse_WhenOwnerPatched()
    {
        const string source =
            "You violated the covenant of the water ritual and killed your bonded kith. You are cursed.\n\n"
            + "Your reputation with the Fellowship of Wardens decreases by 100.";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.HandleGivesRepBeforeDeathRemoval), marked, out var translated), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("GivesRepWaterRitualCurse"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotTranslateGivesRepWaterRitualCursePrefix_WhenOwnerAbsent()
    {
        const string appendedLine = "Your reputation with the Fellowship of Wardens decreases by 100.";
        const string source =
            "You violated the covenant of the water ritual and killed your bonded kith. You are cursed.\n\n"
            + appendedLine;

        Assert.That(TryTranslateForOwner(nameof(DummySingleCallsiteOwnerPopupTarget.CreateHolograms), source, out var translated), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("GivesRepWaterRitualCurse"), Is.Zero);
        });
    }

    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.HandleMagnetizedApplicator),
        "{{Y|The steel boots}} become magnetized!",
        "MagnetizedApplicatorCrumbles")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.WishGeneratePopulation),
        "The population 'JoppaVillagers' distributed as follows over 1000 generations;\nSnapjaw: 1.00000%",
        "PopulationManagerMissingTable")]
    [TestCase(
        nameof(DummySingleCallsiteOwnerPopupTarget.ProcessTargetedMove),
        "{{Y|normality field}} prevents spacetime movement.",
        "PhysicsAttackConfirm")]
    public void Patch_DoesNotClaimDeferredRuntimePopups_WhenOwnerPatched(
        string methodName,
        string source,
        string guardedDetail)
    {
        Assert.That(TryTranslateForOwner(methodName, source, out var translated), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount(guardedDetail), Is.Zero);
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

        if (popupMethod == PopupMethod.ShowYesNoCancel)
        {
            _ = DummyPopupShow.ShowYesNoCancel(source);
            return;
        }

        if (popupMethod == PopupMethod.ShowSpace)
        {
            DummyPopupShow.ShowSpace(source);
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
            PopupMethod.ShowYesNoCancel => DummyPopupShow.LastShowYesNoCancelMessage,
            PopupMethod.ShowSpace => DummyPopupShow.LastShowSpaceMessage,
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
            nameof(DummySingleCallsiteOwnerPopupTarget.CastDismember) =>
                "XRL.World.Parts.Skill.Axe_Dismember|Cast",
            nameof(DummySingleCallsiteOwnerPopupTarget.Cast) =>
                "XRL.World.Parts.Skill.Cudgel_Slam|Cast",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleSubmersionCommand) =>
                "XRL.World.Parts.Skill.Submersion|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.Recharge) =>
                "XRL.World.Parts.Skill.Tinkering_Tinker1|Recharge",
            nameof(DummySingleCallsiteOwnerPopupTarget.AttemptOpenContainer) =>
                "XRL.World.Parts.Container|AttemptOpen",
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
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleSpiralBorerCurio) =>
                "XRL.World.Parts.SpiralBorerCurio|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGritGateMainframeTerminal) =>
                "XRL.World.Parts.GritGateMainframeTerminal|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleHindrenMysteryCriticalNpc) =>
                "XRL.World.Parts.HindrenMysteryCriticalNPC|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.WishModify) =>
                "XRL.World.Parts.IModification|WishModify",
            nameof(DummySingleCallsiteOwnerPopupTarget.ReturnKindrishAward) =>
                "XRL.World.Parts.KindrishProperties|ReturnAward",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleLiquidFueledPowerPlant) =>
                "XRL.World.Parts.LiquidFueledPowerPlant|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleNeutronFluxPourExplodesEvent) =>
                "XRL.World.Parts.NeutronFluxContainment|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleNeutronFluxBeginTakeActionEvent) =>
                "XRL.World.Parts.NeutronFluxContainment|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandlePolygelInventoryActionEvent) =>
                "XRL.World.Parts.Polygel|HandleEvent",
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
            nameof(DummySingleCallsiteOwnerPopupTarget.CheckPullDown) =>
                "XRL.World.Parts.StairsDown|CheckPullDown",
            nameof(DummySingleCallsiteOwnerPopupTarget.ProcessTargetedMove) =>
                "XRL.World.Parts.Physics|ProcessTargetedMove",
            nameof(DummySingleCallsiteOwnerPopupTarget.ShowScriptCallToArmsWarning) =>
                "XRL.World.ZoneParts.ScriptCallToArms|ShowWarning",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleWaterRitualRecord) =>
                "XRL.World.Parts.WaterRitualRecord|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.FinishSpreadPax) =>
                "XRL.World.QuestManagers.SpreadPax|Finish",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleTrainingBook) =>
                "XRL.World.Parts.TrainingBook|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleDestroyOnUnequip) =>
                "XRL.World.Parts.DestroyOnUnequip|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleMagnetizedApplicator) =>
                "XRL.World.Parts.MagnetizedApplicator|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleCursedCellSocket) =>
                "XRL.World.Parts.CursedCellSocket|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleNephalPropertiesBeforeDeathRemoval) =>
                "XRL.World.Parts.NephalProperties|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleGivesRepBeforeDeathRemoval) =>
                "XRL.World.Parts.GivesRep|HandleEvent",
            nameof(DummySingleCallsiteOwnerPopupTarget.WishMutation) =>
                "XRL.World.Parts.Mutations|WishMutation",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandleBlueprintXML) =>
                "XRL.World.GameObjectFactory|HandleBlueprintXML",
            nameof(DummySingleCallsiteOwnerPopupTarget.LoadGame) =>
                "XRL.XRLGame|LoadGame",
            nameof(DummySingleCallsiteOwnerPopupTarget.WishGeneratePopulation) =>
                "XRL.PopulationManager|WishGenerate",
            nameof(DummySingleCallsiteOwnerPopupTarget.TransitToThinWorld) =>
                "XRL.World.Parts.ThinWorld|TransitToThinWorld",
            nameof(DummySingleCallsiteOwnerPopupTarget.HandlePlayerMuralEndTurn) =>
                "XRL.World.Parts.PlayerMuralController|HandleEvent",
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
        ShowYesNoCancel,
        ShowSpace,
    }
}
