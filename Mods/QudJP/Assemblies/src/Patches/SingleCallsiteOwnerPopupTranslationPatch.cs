using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SingleCallsiteOwnerPopupTranslationPatch
{
    private const string Context = nameof(SingleCallsiteOwnerPopupTranslationPatch);
    private const string AscensionBarathrumOwner = "XRL.World.Quests.AscensionSystem|BarathrumStartConversation";
    private const string CharacterInitOwner = "XRL.CharacterBuilds.Qud.QudSpecificCharacterInitModule|handleBootEvent";
    private const string ContainerAttemptOpenOwner = "XRL.World.Parts.Container|AttemptOpen";
    private const string DecoyHologramOwner = "XRL.World.Parts.DecoyHologramEmitter|CreateHolograms";
    private const string BaetylRewardWishOwner = "XRL.World.Parts.RandomAltarBaetyl|HandleBaetylRewardWish";
    private const string AxeDismemberOwner = "XRL.World.Parts.Skill.Axe_Dismember|CastForceSuccess";
    private const string AxeDismemberCastOwner = "XRL.World.Parts.Skill.Axe_Dismember|Cast";
    private const string BiomeSurfaceDistributionOwner = "XRL.World.Biomes.BiomeManager|DisplaySurfaceDistribution";
    private const string CudgelSlamOwner = "XRL.World.Parts.Skill.Cudgel_Slam|Cast";
    private const string DynamicQuestRewardGameObjectOwner = "XRL.World.DynamicQuestRewardElement_GameObject|award";
    private const string FactionEncounterWishOwner = "XRL.World.ZoneBuilders.FactionEncounters|HandleFactionEncounterWish";
    private const string ProselytizeOwner = "XRL.World.Parts.Skill.Persuasion_Proselytize|AttemptProselytization";
    private const string TinkeringOwner = "XRL.World.Parts.Skill.Tinkering|LearnNewRecipe";
    private const string TinkeringTinker1RechargeOwner = "XRL.World.Parts.Skill.Tinkering_Tinker1|Recharge";
    private const string GameUniqueOwner = "XRL.World.Parts.GameUnique|OnCreated";
    private const string GenocideCurioOwner = "XRL.World.Parts.GenocideCurio|HandleEvent";
    private const string GritGateMainframeOwner = "XRL.World.Parts.GritGateMainframeTerminal|HandleEvent";
    private const string HindrenMysteryCriticalNpcOwner = "XRL.World.Parts.HindrenMysteryCriticalNPC|HandleEvent";
    private const string IModificationWishModifyOwner = "XRL.World.Parts.IModification|WishModify";
    private const string KindrishReturnAwardOwner = "XRL.World.Parts.KindrishProperties|ReturnAward";
    private const string LiquidFueledPowerPlantOwner = "XRL.World.Parts.LiquidFueledPowerPlant|HandleEvent";
    private const string LookShowLookerOwner = "XRL.UI.Look|ShowLooker";
    private const string MakeFussOnTakenOwner = "XRL.World.Parts.MakeFussOnTaken|MakeFuss";
    private const string MarkovBookOwner = "XRL.World.Parts.MarkovBook|HandleEvent";
    private const string MumblesInfectionOwner = "XRL.World.Parts.MumblesInfection|FireEvent";
    private const string NeutronFluxContainmentOwner = "XRL.World.Parts.NeutronFluxContainment|HandleEvent";
    private const string PolygelOwner = "XRL.World.Parts.Polygel|HandleEvent";
    private const string MutationPointsOnEatOwner = "XRL.World.Parts.MutationPointsOnEat|FireEvent";
    private const string EngulfingDescendsOwner = "XRL.World.Parts.EngulfingDescends|FireEvent";
    private const string ReputationSetFactionRankOwner = "XRL.World.Reputation|SetFactionRank";
    private const string RecoilOnDeathOwner = "XRL.World.Parts.RecoilOnDeath|HandleEvent";
    private const string SpraybottleOwner = "XRL.World.Parts.Spraybottle|HandleEvent";
    private const string FixitSprayOwner = "XRL.World.Parts.FixitSpray|HandleEvent";
    private const string AnimatorSprayOwner = "XRL.World.Parts.AnimatorSpray|HandleEvent";
    private const string StairsDownCheckPullDownOwner = "XRL.World.Parts.StairsDown|CheckPullDown";
    private const string SubmersionOwner = "XRL.World.Parts.Skill.Submersion|HandleEvent";
    private const string SummoningCurioOwner = "XRL.World.Parts.SummoningCurio|HandleEvent";
    private const string FoodOwner = "XRL.World.Parts.Food|HandleEvent";
    private const string ScriptCallToArmsOwner = "XRL.World.ZoneParts.ScriptCallToArms|ShowWarning";
    private const string SpaceTimeVortexOwner = "XRL.World.Parts.SpaceTimeVortex|ApplyVortex";
    private const string SpreadPaxOwner = "XRL.World.QuestManagers.SpreadPax|Finish";
    private const string ToolboxOwner = "XRL.World.Parts.Toolbox|HandleBonus";
    private const string TrainingBookOwner = "XRL.World.Parts.TrainingBook|HandleEvent";
    private const string WaterRitualRecordOwner = "XRL.World.Parts.WaterRitualRecord|HandleEvent";
    private const string CursedCellSocketOwner = "XRL.World.Parts.CursedCellSocket|HandleEvent";
    private const string DestroyOnUnequipOwner = "XRL.World.Parts.DestroyOnUnequip|HandleEvent";
    private const string MagnetizedApplicatorOwner = "XRL.World.Parts.MagnetizedApplicator|HandleEvent";
    private const string MutationsWishMutationOwner = "XRL.World.Parts.Mutations|WishMutation";
    private const string NephalPropertiesHandleEventOwner = "XRL.World.Parts.NephalProperties|HandleEvent";
    private const string PopulationManagerWishGenerateOwner = "XRL.PopulationManager|WishGenerate";
    private const string GameObjectFactoryBlueprintXmlOwner = "XRL.World.GameObjectFactory|HandleBlueprintXML";
    private const string XrlGameLoadGameOwner = "XRL.XRLGame|LoadGame";

    private static readonly Regex DecoyOutOfRangePattern = new(
        "^That is out of range \\((?<range>.+?) (?<unit>squares?)\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BaetylRewardWishPattern = new(
        "^Generated (?<item>.+?) as reward for (?<demand>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BiomeNotFoundPattern = new(
        "^No biome by name '(?<name>.+?)' found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CharacterInitUnknownBlueprintPattern = new(
        "^Error creating player body\\. Unknown blueprint \"(?<blueprint>.+?)\"$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerCannotTradePattern = new(
        "^You cannot trade with (?<object>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerEmptyStorePattern = new(
        "^There's nothing (?<preposition>.+?) that\\. Would you like to store an item\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AxeDismemberSelfPattern = new(
        "^Are you sure you want to dismember (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CudgelSlamSelfPattern = new(
        "^Are you sure you want to slam (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubmersionTooShallowPattern = new(
        "^(?<liquid>.+?) (?:is|are) too shallow for you to submerge in\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringLearnRecipePattern = new(
        "^You have a flash of insight and scribe (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringRechargeSuccessPattern = new(
        "^You have (?<partial>partially )?recharged (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringRechargeCannotPattern = new(
        "^(?<item>.+?) can't be recharged that way\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GameUniqueWishConfirmationPattern = new(
        "^(?<object>.+?) \\((?<blueprint>.+?)\\) is considered unique, are you sure you want to create another\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GenocideCurioActivationPattern = new(
        "^You activate (?<item>.+?) and toss it into the air\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GritGateMainframeUnresponsivePattern = new(
        "^(?<object>.+?) is unresponsive\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HindrenMysteryCriticalNpcDeathPattern = new(
        "^The death of (?<object>.+?) means that the investigation can go no further\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IModificationMissingModificationPattern = new(
        "^No modification by the name '(?<name>.+?)' could be found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IModificationMissingBlueprintPattern = new(
        "^No blueprint by the name '(?<name>.+?)' could be found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LiquidFueledPowerPlantEmptyPattern = new(
        "^Your (?<object>.+?) (?<verb>has|have) consumed all of (?<fuel>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NeutronFluxNoContainmentPattern = new(
        "^There's no magnetic containment inside (?<object>.+?)\\. Pour anyway\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NeutronFluxWarningGlyphPattern = new(
        "^(?<object>.+?) (?:beeps|beep) loudly and (?:flashes|flash) a warning glyph\\. Do you want to stop travelling\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PolygelIdentifiedPattern = new(
        "^It's a (?<object>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PolygelMorphsPattern = new(
        "^The polygel morphs into another (?<object>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MakeFussOnTakenPattern = new(
        "^You have (?<action>.+?) (?<object>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LookNavigationWeightPattern = new(
        "^(?<x>-?\\d+), (?<y>-?\\d+): (?<weight>.+?)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MarkovBookExcerptPattern = new(
        "^You read one of the few legible excerpts from (?<title>.+?):\\n\\n\"(?<excerpt>[\\s\\S]+)\"$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MumblesSecretPattern = new(
        "^The mouths on your skin begin to mumble coherently, revealing the wisdom of a trillion microbes:\\n\\n(?<text>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MutationPointsOnEatPattern = new(
        "^Your genome destabilizes and you gain (?<amount>.+?) mutation (?<unit>point|points)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EngulfingDescendsPassengerFallPattern = new(
        "^(?<object>.+?)&y engulfing you melts through the floor! You fall to the level below\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoFactionMembersPattern = new(
        "^No members found for '(?<faction>.+?)'\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RecoilOnDeathTransportPattern = new(
        "^Just before your demise, you are transported to safety! (?<object>.+?) (?<verb>disintegrates|disintegrate)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReceiveObjectPattern = new(
        "^You receive (?<object>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SpraybottleCoveredPattern = new(
        "^(?<object>.+?) (?<verb>is|are) covered in (?<liquid>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FixitSprayPhasePassThroughPattern = new(
        "^Some sticky goop passes through (?<object>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FixitSprayLiquidMixPattern = new(
        "^Some sticky goop mixes in with (?<object>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FixitSprayCoveredPattern = new(
        "^(?<object>.+?) (?<verb>is|are) covered in sticky goop!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AnimatorSprayIdentifiedPattern = new(
        "^(?:It's|They're|You're|It is|They are|You are) (?<item>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AnimatorSprayImbueLifePattern = new(
        "^You imbue (?<object>.+?) with life\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SummoningCurioActivationPattern = new(
        "^You activate the curio and toss it on the ground\\. It erupts into a throng of tiny polygons, which amalgamate into a fully formed (?<creature>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FoodConsumptionFramePattern = new(
        "^You eat (?<food>.+?)\\.\\n(?<message>[\\s\\S]*?)You are now \\{\\{\\|(?<foodStatus>(?:\\{\\{[^|}]*\\|[^{}]*\\}\\}|[^{}]+))\\}\\} and \\{\\{\\|(?<waterStatus>(?:\\{\\{[^|}]*\\|[^{}]*\\}\\}|[^{}]+))\\}\\}\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SpaceTimeVortexCompanionSuckedPattern = new(
        "^Your companion, (?<companion>.+?),(?<verb>have|has) been sucked into (?<vortex>.+?) (?<direction>to the north|to the south|to the east|to the west|to the northeast|to the northwest|to the southeast|to the southwest|nearby|above|below|here|somewhere)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StairsDownCompanionFellPattern = new(
        "^Your companion, (?<companion>.+?),(?<verb>have|has) fallen (?<preposition>.+?) (?<stairs>.+?) (?<direction>to the north|to the south|to the east|to the west|to the northeast|to the northwest|to the southeast|to the southwest|nearby|above|below|here|somewhere)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ScriptCallToArmsOthoYellsPattern = new(
        "^Otho yells, '\\{\\{W\\|(?<name>.+?)! Come back here!\\}\\}'$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SpreadPaxCurePattern = new(
        "^The infected crust of skin on your (?<location>.+?) loosens and breaks away\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrainingBookAttributeIncreasePattern = new(
        "^Your (?<attribute>.+?) is increased by (?<amount>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ToolboxInoperativeConfirmationPattern = new(
        "^(?<object>.+?) (?<verb>is|are) (?<status>unpowered|still starting up|inoperative)\\. Do you want to continue(?<tail> without .+? benefits|, using .+? without power)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterRitualRecordBotheredPattern = new(
        "^You bothered (?<object>.+?) again\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CursedCellSocketLocksPattern = new(
        "^(?<object>.+?) (?:locks|lock) firmly into the socket, preventing removal\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DestroyOnUnequipConfirmationPattern = new(
        "^(?<object>.+?) will be destroyed if (?<clause>.+?) unequipped\\. Do you want to continue\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MagnetizedApplicatorCrumblesPattern = new(
        "^(?<object>.+?) (?:loses|lose) its magnetic charge and crumbles to powder\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MutationWishDidYouMeanPattern = new(
        "^Did you mean (?<mutation>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NephalPropertiesChordAbsorbedPattern = new(
        "^A sphere of light in the chord of (?<name>.+?) radiates away\\.\\n\\nYou feel it absorbed elsewhere\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PopulationManagerInvalidCountPattern = new(
        "^'(?<count>.+?)' is not a valid integer\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PopulationManagerMissingTablePattern = new(
        "^No table by the name '(?<table>.+?)' could be resolved\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GameObjectFactoryMissingBlueprintPattern = new(
        "^No blueprint named \"(?<blueprint>.+?)\" found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex XrlGameMissingSavePattern = new(
        "^No saved game exists\\. \\((?<path>.+?)\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<string>? ownerStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var iEventType = AccessTools.TypeByName("XRL.World.IEvent");
        var commandEventType = AccessTools.TypeByName("XRL.World.CommandEvent");
        var beforeDeathRemovalEventType = AccessTools.TypeByName("XRL.World.BeforeDeathRemovalEvent");
        var beginConversationEventType = AccessTools.TypeByName("XRL.World.BeginConversationEvent");
        var embarkInfoType = AccessTools.TypeByName("XRL.CharacterBuilds.EmbarkInfo");
        var getTinkeringBonusEventType = AccessTools.TypeByName("XRL.World.GetTinkeringBonusEvent");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        var xrlGameType = AccessTools.TypeByName("XRL.XRLGame");
        var endTurnEventType = AccessTools.TypeByName("XRL.World.EndTurnEvent");
        var beforeDieEventType = AccessTools.TypeByName("XRL.World.BeforeDieEvent");
        var cellChangedEventType = AccessTools.TypeByName("XRL.World.CellChangedEvent");
        var neutronFluxPourExplodesEventType = AccessTools.TypeByName("XRL.World.NeutronFluxPourExplodesEvent");
        var beginTakeActionEventType = AccessTools.TypeByName("XRL.World.BeginTakeActionEvent");
        var beginBeingUnequippedEventType = AccessTools.TypeByName("XRL.World.BeginBeingUnequippedEvent");
        var axeDismemberType = AccessTools.TypeByName("XRL.World.Parts.Skill.Axe_Dismember");
        var cudgelSlamType = AccessTools.TypeByName("XRL.World.Parts.Skill.Cudgel_Slam");
        if (gameObjectType is null
            || eventType is null
            || iEventType is null
            || commandEventType is null
            || beforeDeathRemovalEventType is null
            || beginConversationEventType is null
            || embarkInfoType is null
            || getTinkeringBonusEventType is null
            || inventoryActionEventType is null
            || xrlGameType is null
            || endTurnEventType is null
            || beforeDieEventType is null
            || cellChangedEventType is null
            || neutronFluxPourExplodesEventType is null
            || beginTakeActionEventType is null
            || beginBeingUnequippedEventType is null
            || axeDismemberType is null
            || cudgelSlamType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            "XRL.World.Quests.AscensionSystem",
            "BarathrumStartConversation",
            [gameObjectType]);
        AddTarget(
            targets,
            "XRL.CharacterBuilds.Qud.QudSpecificCharacterInitModule",
            "handleBootEvent",
            [typeof(string), xrlGameType, embarkInfoType, typeof(object)]);
        AddTarget(
            targets,
            "XRL.World.Biomes.BiomeManager",
            "DisplaySurfaceDistribution",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.Container",
            "AttemptOpen",
            [gameObjectType, iEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.DecoyHologramEmitter",
            "CreateHolograms",
            [gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.Parts.RandomAltarBaetyl",
            "HandleBaetylRewardWish",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Axe_Dismember",
            "CastForceSuccess",
            [gameObjectType, axeDismemberType, gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Axe_Dismember",
            "Cast",
            [gameObjectType, axeDismemberType, gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Cudgel_Slam",
            "Cast",
            [gameObjectType, cudgelSlamType, typeof(string), gameObjectType, typeof(bool), typeof(int), typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Submersion",
            "HandleEvent",
            [commandEventType]);
        AddTarget(
            targets,
            "XRL.World.DynamicQuestRewardElement_GameObject",
            "award",
            Type.EmptyTypes);
        AddTarget(
            targets,
            "XRL.World.ZoneBuilders.FactionEncounters",
            "HandleFactionEncounterWish",
            [typeof(Match)]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Persuasion_Proselytize",
            "AttemptProselytization",
            Type.EmptyTypes);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Tinkering",
            "LearnNewRecipe",
            [gameObjectType, typeof(int), typeof(int)]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Tinkering_Tinker1",
            "Recharge",
            [gameObjectType, iEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.GameUnique",
            "OnCreated",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.GenocideCurio",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.GritGateMainframeTerminal",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.HindrenMysteryCriticalNPC",
            "HandleEvent",
            [beforeDeathRemovalEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.IModification",
            "WishModify",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.KindrishProperties",
            "ReturnAward",
            Type.EmptyTypes);
        AddTarget(
            targets,
            "XRL.World.Parts.LiquidFueledPowerPlant",
            "HandleEvent",
            [endTurnEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.NeutronFluxContainment",
            "HandleEvent",
            [neutronFluxPourExplodesEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.NeutronFluxContainment",
            "HandleEvent",
            [beginTakeActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Polygel",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.UI.Look",
            "ShowLooker",
            [typeof(int), typeof(int), typeof(int)]);
        AddTarget(
            targets,
            "XRL.World.Parts.MakeFussOnTaken",
            "MakeFuss",
            [gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.Parts.MarkovBook",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.MumblesInfection",
            "FireEvent",
            [eventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.MutationPointsOnEat",
            "FireEvent",
            [eventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.EngulfingDescends",
            "FireEvent",
            [eventType]);
        AddTarget(
            targets,
            "XRL.World.Reputation",
            "SetFactionRank",
            [typeof(string), typeof(string), typeof(bool), typeof(bool)]);
        AddTarget(
            targets,
            "XRL.World.Parts.RecoilOnDeath",
            "HandleEvent",
            [beforeDieEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Spraybottle",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.FixitSpray",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.AnimatorSpray",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.SummoningCurio",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Food",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.SpaceTimeVortex",
            "ApplyVortex",
            [gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.Parts.StairsDown",
            "CheckPullDown",
            [gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.ZoneParts.ScriptCallToArms",
            "ShowWarning",
            Type.EmptyTypes);
        AddTarget(
            targets,
            "XRL.World.Parts.TrainingBook",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.WaterRitualRecord",
            "HandleEvent",
            [beginConversationEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.CursedCellSocket",
            "HandleEvent",
            [cellChangedEventType]);
        AddTarget(
            targets,
            "XRL.World.QuestManagers.SpreadPax",
            "Finish",
            Type.EmptyTypes);
        AddTarget(
            targets,
            "XRL.World.Parts.Toolbox",
            "HandleBonus",
            [getTinkeringBonusEventType, typeof(int), typeof(int)]);
        AddTarget(
            targets,
            "XRL.World.Parts.DestroyOnUnequip",
            "HandleEvent",
            [beginBeingUnequippedEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.MagnetizedApplicator",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Mutations",
            "WishMutation",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.NephalProperties",
            "HandleEvent",
            [beforeDeathRemovalEventType]);
        AddTarget(
            targets,
            "XRL.PopulationManager",
            "WishGenerate",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.GameObjectFactory",
            "HandleBlueprintXML",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.XRLGame",
            "LoadGame",
            [typeof(string), typeof(bool), typeof(bool), typeof(Dictionary<string, object>)]);
        return targets;
    }

    public static void Prefix(MethodBase __originalMethod)
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
            ownerStack ??= new Stack<string>();
            ownerStack.Push(FormatOwnerKey(__originalMethod));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            if (ownerStack is { Count: > 0 })
            {
                _ = ownerStack.Pop();
            }

            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        return TryTranslatePopupMessageForOwnerKey(source, CurrentOwnerKey(), route, family, out translated);
    }

    internal static bool TryTranslatePopupMessageForOwnerKey(
        string source,
        string? ownerKey,
        string route,
        string family,
        out string translated)
    {
        _ = family;
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (TryTranslateCore(source, ownerKey, out translated, out var detail))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + "." + detail,
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCore(string source, string? ownerKey, out string translated, out string detail)
    {
        if (OwnerMatches(ownerKey, AscensionBarathrumOwner)
            && source.EndsWith(" left your party.", StringComparison.Ordinal)
            && DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated))
        {
            detail = "AscensionBarathrumLeftParty";
            return true;
        }

        var match = BiomeNotFoundPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, BiomeSurfaceDistributionOwner))
        {
            translated = $"'{match.Groups["name"].Value}'という名前のバイオームは見つからない。";
            detail = "BiomeNotFound";
            return true;
        }

        match = CharacterInitUnknownBlueprintPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, CharacterInitOwner))
        {
            translated = $"プレイヤーの体を作成できない。不明なブループリント「{match.Groups["blueprint"].Value}」。";
            detail = "CharacterInitUnknownBlueprint";
            return true;
        }

        match = DecoyOutOfRangePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, DecoyHologramOwner))
        {
            translated = $"範囲外だ（{NormalizeRange(match.Groups["range"].Value)}マス）。";
            detail = "DecoyHologramOutOfRange";
            return true;
        }

        match = BaetylRewardWishPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, BaetylRewardWishOwner))
        {
            translated = $"{match.Groups["demand"].Value}の報酬として{match.Groups["item"].Value}を生成した。";
            detail = "BaetylRewardWish";
            return true;
        }

        match = AxeDismemberSelfPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, AxeDismemberOwner, AxeDismemberCastOwner))
        {
            translated = $"{match.Groups["target"].Value}を切断してもよいか？";
            detail = "AxeDismemberSelfConfirmation";
            return true;
        }

        match = CudgelSlamSelfPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, CudgelSlamOwner))
        {
            translated = $"{match.Groups["target"].Value}を叩きつけてもよいか？";
            detail = "CudgelSlamSelfConfirmation";
            return true;
        }

        match = ContainerCannotTradePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ContainerAttemptOpenOwner))
        {
            translated = $"{match.Groups["object"].Value}とは取引できない。";
            detail = "ContainerCannotTrade";
            return true;
        }

        match = ContainerEmptyStorePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ContainerAttemptOpenOwner))
        {
            translated = match.Groups["preposition"].Value switch
            {
                "in" => "その中には何も入っていない。アイテムを預けるか？",
                "on" => "そこには何も置かれていない。アイテムを預けるか？",
                _ => "そこには何もない。アイテムを預けるか？",
            };
            detail = "ContainerEmptyStore";
            return true;
        }

        match = SubmersionTooShallowPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, SubmersionOwner))
        {
            translated = $"{match.Groups["liquid"].Value}は浅すぎて潜れない。";
            detail = "SubmersionTooShallow";
            return true;
        }

        if (OwnerMatches(ownerKey, ProselytizeOwner)
            && source.Contains(" already your follower. Do you want to proselytize ")
            && DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated))
        {
            detail = "ProselytizeFollowerConfirmation";
            return true;
        }

        match = TinkeringLearnRecipePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, TinkeringOwner))
        {
            translated = $"ひらめきを得て{StringHelpers.StripLeadingEnglishArticle(match.Groups["item"].Value)}を記した。";
            detail = "TinkeringLearnRecipe";
            return true;
        }

        match = TinkeringRechargeSuccessPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, TinkeringTinker1RechargeOwner))
        {
            translated = match.Groups["partial"].Success
                ? $"{match.Groups["item"].Value}を部分的に充電した。"
                : $"{match.Groups["item"].Value}を充電した。";
            detail = "TinkeringRechargeSuccess";
            return true;
        }

        match = TinkeringRechargeCannotPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, TinkeringTinker1RechargeOwner))
        {
            translated = $"{match.Groups["item"].Value}はその方法では充電できない。";
            detail = "TinkeringRechargeCannot";
            return true;
        }

        match = GameUniqueWishConfirmationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, GameUniqueOwner))
        {
            translated = $"{match.Groups["object"].Value}（{match.Groups["blueprint"].Value}）は一意とみなされています。もう1つ作成しますか？";
            detail = "GameUniqueWishConfirmation";
            return true;
        }

        match = GenocideCurioActivationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, GenocideCurioOwner))
        {
            translated = $"{match.Groups["item"].Value}を起動して空中に放り投げた。";
            detail = "GenocideCurioActivation";
            return true;
        }

        match = GritGateMainframeUnresponsivePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, GritGateMainframeOwner))
        {
            translated = $"{match.Groups["object"].Value}は反応しない。";
            detail = "GritGateMainframeUnresponsive";
            return true;
        }

        match = HindrenMysteryCriticalNpcDeathPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, HindrenMysteryCriticalNpcOwner))
        {
            translated = $"{match.Groups["object"].Value}の死により、調査はこれ以上進められなくなった。";
            detail = "HindrenMysteryCriticalNpcDeath";
            return true;
        }

        match = IModificationMissingModificationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, IModificationWishModifyOwner))
        {
            translated = $"'{match.Groups["name"].Value}'という改造は見つからない。";
            detail = "IModificationMissingModification";
            return true;
        }

        match = IModificationMissingBlueprintPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, IModificationWishModifyOwner))
        {
            translated = $"'{match.Groups["name"].Value}'というブループリントは見つからない。";
            detail = "IModificationMissingBlueprint";
            return true;
        }

        match = LiquidFueledPowerPlantEmptyPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, LiquidFueledPowerPlantOwner))
        {
            translated = $"あなたの{match.Groups["object"].Value}は{match.Groups["fuel"].Value}をすべて消費した。";
            detail = "LiquidFueledPowerPlantEmpty";
            return true;
        }

        match = NeutronFluxNoContainmentPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, NeutronFluxContainmentOwner))
        {
            translated = $"{match.Groups["object"].Value}の中には磁気封じ込めがない。それでも注ぐか？";
            detail = "NeutronFluxNoContainment";
            return true;
        }

        match = NeutronFluxWarningGlyphPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, NeutronFluxContainmentOwner))
        {
            translated = $"{match.Groups["object"].Value}が大きくビープ音を鳴らし、警告グリフを点滅させる。移動をやめるか？";
            detail = "NeutronFluxWarningGlyph";
            return true;
        }

        match = PolygelIdentifiedPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, PolygelOwner))
        {
            translated = $"{match.Groups["object"].Value}だ！";
            detail = "PolygelIdentified";
            return true;
        }

        match = PolygelMorphsPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, PolygelOwner))
        {
            translated = $"ポリジェルがもう1つの{match.Groups["object"].Value}へと変形した！";
            detail = "PolygelMorphs";
            return true;
        }

        match = LookNavigationWeightPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, LookShowLookerOwner))
        {
            translated = $"{match.Groups["x"].Value}, {match.Groups["y"].Value}: ナビゲーション重み {match.Groups["weight"].Value}";
            detail = "LookNavigationWeight";
            return true;
        }

        match = MakeFussOnTakenPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, MakeFussOnTakenOwner))
        {
            translated = $"{match.Groups["object"].Value}を{TranslateAcquisitionAction(match.Groups["action"].Value)}！";
            detail = "MakeFussOnTaken";
            return true;
        }

        match = MarkovBookExcerptPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, MarkovBookOwner))
        {
            translated = $"{match.Groups["title"].Value}から判読できる数少ない抜粋の1つを読んだ:\n\n「{match.Groups["excerpt"].Value}」";
            detail = "MarkovBookExcerpt";
            return true;
        }

        match = MumblesSecretPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, MumblesInfectionOwner))
        {
            translated = $"肌の口がはっきりとつぶやき始め、一兆の微生物の叡智を明かした:\n\n{match.Groups["text"].Value}";
            detail = "MumblesSecret";
            return true;
        }

        match = MutationPointsOnEatPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, MutationPointsOnEatOwner))
        {
            translated = $"ゲノムが不安定化し、変異ポイントを{match.Groups["amount"].Value}得た。";
            detail = "MutationPointsOnEat";
            return true;
        }

        match = EngulfingDescendsPassengerFallPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, EngulfingDescendsOwner))
        {
            var subject = StringHelpers.StripLeadingEnglishArticle(match.Groups["object"].Value, includeCapitalizedDefiniteArticle: true);
            translated = $"{subject}があなたを飲み込んだまま床を溶かして下っていった！ あなたは下の階層へ落ちた。";
            detail = "EngulfingDescendsPassengerFall";
            return true;
        }

        match = NoFactionMembersPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, FactionEncounterWishOwner))
        {
            translated = $"'{match.Groups["faction"].Value}'のメンバーは見つからない。";
            detail = "FactionEncounterNoMembers";
            return true;
        }

        match = ReceiveObjectPattern.Match(source);
        if (match.Success
            && OwnerMatches(
                ownerKey,
                DynamicQuestRewardGameObjectOwner,
                KindrishReturnAwardOwner))
        {
            translated = $"{match.Groups["object"].Value}を受け取った。";
            detail = "ReceiveObject";
            return true;
        }

        if (OwnerMatches(ownerKey, ReputationSetFactionRankOwner)
            && source.StartsWith("You are promoted to the ", StringComparison.Ordinal)
            && DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated))
        {
            detail = "ReputationRankPromotion";
            return true;
        }

        match = RecoilOnDeathTransportPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, RecoilOnDeathOwner))
        {
            translated = $"死の直前、あなたは安全な場所へ転送された！ {match.Groups["object"].Value}は崩壊した。";
            detail = "RecoilOnDeathTransport";
            return true;
        }

        match = SpraybottleCoveredPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, SpraybottleOwner))
        {
            translated = $"{match.Groups["object"].Value}は{match.Groups["liquid"].Value}に覆われた！";
            detail = "SpraybottleCovered";
            return true;
        }

        match = FixitSprayPhasePassThroughPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, FixitSprayOwner))
        {
            translated = $"ねばつく粘液が{match.Groups["object"].Value}を通り抜けた。";
            detail = "FixitSprayPhasePassThrough";
            return true;
        }

        match = FixitSprayLiquidMixPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, FixitSprayOwner))
        {
            translated = $"ねばつく粘液が{match.Groups["object"].Value}に混ざった。";
            detail = "FixitSprayLiquidMix";
            return true;
        }

        match = FixitSprayCoveredPattern.Match(source);
        if (match.Success
            && !string.Equals(match.Groups["object"].Value, "You", StringComparison.Ordinal)
            && !string.Equals(match.Groups["object"].Value, "you", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, FixitSprayOwner))
        {
            translated = $"{match.Groups["object"].Value}はべとべとの粘液に覆われた！";
            detail = "FixitSprayCovered";
            return true;
        }

        match = AnimatorSprayIdentifiedPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, AnimatorSprayOwner))
        {
            var item = StringHelpers.StripLeadingEnglishArticle(match.Groups["item"].Value, includeCapitalizedDefiniteArticle: true);
            translated = $"{item}だ！";
            detail = "AnimatorSprayIdentified";
            return true;
        }

        match = AnimatorSprayImbueLifePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, AnimatorSprayOwner))
        {
            translated = $"{match.Groups["object"].Value}に命を吹き込んだ。";
            detail = "AnimatorSprayImbueLife";
            return true;
        }

        match = SummoningCurioActivationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, SummoningCurioOwner))
        {
            translated = $"キュリオを起動して地面に投げた。小さなポリゴンの群れが噴出し、完全な形をした{match.Groups["creature"].Value}へと融合した。";
            detail = "SummoningCurioActivation";
            return true;
        }

        match = FoodConsumptionFramePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, FoodOwner))
        {
            var food = StringHelpers.StripLeadingEnglishArticle(match.Groups["food"].Value, includeCapitalizedDefiniteArticle: true);
            var foodStatus = TranslateFoodOrWaterStatus(match.Groups["foodStatus"].Value);
            var waterStatus = TranslateFoodOrWaterStatus(match.Groups["waterStatus"].Value);
            translated = $"{food}を食べた。\n{match.Groups["message"].Value}現在、{{{{|{foodStatus}}}}}、{{{{|{waterStatus}}}}}だ。";
            detail = "FoodConsumptionFrame";
            return true;
        }

        match = SpaceTimeVortexCompanionSuckedPattern.Match(source);
        if (match.Success
            && OwnerMatches(ownerKey, SpaceTimeVortexOwner)
            && TryTranslateDirectionPhrase(match.Groups["direction"].Value, out var direction))
        {
            var vortex = StringHelpers.StripLeadingEnglishArticle(match.Groups["vortex"].Value, includeCapitalizedDefiniteArticle: true);
            translated = $"あなたの仲間である{match.Groups["companion"].Value}は{direction}の{vortex}に吸い込まれた！";
            detail = "SpaceTimeVortexCompanionSucked";
            return true;
        }

        match = StairsDownCompanionFellPattern.Match(source);
        if (match.Success
            && OwnerMatches(ownerKey, StairsDownCheckPullDownOwner)
            && TryTranslateDirectionPhrase(match.Groups["direction"].Value, out var fallDirection))
        {
            var companion = match.Groups["companion"].Value;
            var stairs = match.Groups["stairs"].Value;
            var fallPreposition = TranslateFallPreposition(match.Groups["preposition"].Value);
            translated = $"あなたの仲間である{companion}は{fallDirection}にある{stairs}の{fallPreposition}落ちた！";
            detail = "StairsDownCompanionFell";
            return true;
        }

        match = ScriptCallToArmsOthoYellsPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ScriptCallToArmsOwner))
        {
            translated = $"オソが叫ぶ。「{{{{W|{match.Groups["name"].Value}！戻ってこい！}}}}」";
            detail = "ScriptCallToArmsOthoYells";
            return true;
        }

        match = SpreadPaxCurePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, SpreadPaxOwner))
        {
            translated = $"あなたの{match.Groups["location"].Value}の感染した皮殻が緩み、剥がれ落ちた。";
            detail = "SpreadPaxCure";
            return true;
        }

        match = TrainingBookAttributeIncreasePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, TrainingBookOwner))
        {
            translated = $"あなたの{match.Groups["attribute"].Value}が{match.Groups["amount"].Value}上昇した！";
            detail = "TrainingBookAttributeIncrease";
            return true;
        }

        match = ToolboxInoperativeConfirmationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ToolboxOwner))
        {
            translated = $"{match.Groups["object"].Value}は{TranslateToolboxStatus(match.Groups["status"].Value)}。{TranslateToolboxContinuation(match.Groups["tail"].Value)}続けますか？";
            detail = "ToolboxInoperativeConfirmation";
            return true;
        }

        match = WaterRitualRecordBotheredPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, WaterRitualRecordOwner))
        {
            translated = $"{match.Groups["object"].Value}にまた迷惑をかけた。";
            detail = "WaterRitualRecordBothered";
            return true;
        }

        match = CursedCellSocketLocksPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, CursedCellSocketOwner))
        {
            translated = $"{match.Groups["object"].Value}はソケットにしっかりとはまり、取り外せなくなった。";
            detail = "CursedCellSocketLocks";
            return true;
        }

        match = DestroyOnUnequipConfirmationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, DestroyOnUnequipOwner))
        {
            translated = $"{match.Groups["object"].Value}は外すと破壊される。続けますか？";
            detail = "DestroyOnUnequipConfirmation";
            return true;
        }

        match = MagnetizedApplicatorCrumblesPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, MagnetizedApplicatorOwner))
        {
            translated = $"{match.Groups["object"].Value}は磁荷を失い、粉々に崩れた。";
            detail = "MagnetizedApplicatorCrumbles";
            return true;
        }

        match = MutationWishDidYouMeanPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, MutationsWishMutationOwner))
        {
            translated = $"「{match.Groups["mutation"].Value}」のことか？";
            detail = "MutationWishDidYouMean";
            return true;
        }

        match = NephalPropertiesChordAbsorbedPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, NephalPropertiesHandleEventOwner))
        {
            translated = $"{match.Groups["name"].Value}の調べの光球が放射されて消えた。\n\nそれがどこか別の場所に吸収されたのを感じた。";
            detail = "NephalPropertiesChordAbsorbed";
            return true;
        }

        match = PopulationManagerInvalidCountPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, PopulationManagerWishGenerateOwner))
        {
            translated = $"'{match.Groups["count"].Value}'は有効な整数ではない。";
            detail = "PopulationManagerInvalidCount";
            return true;
        }

        match = PopulationManagerMissingTablePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, PopulationManagerWishGenerateOwner))
        {
            translated = $"'{match.Groups["table"].Value}'という名前の population table は解決できない。";
            detail = "PopulationManagerMissingTable";
            return true;
        }

        match = GameObjectFactoryMissingBlueprintPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, GameObjectFactoryBlueprintXmlOwner))
        {
            translated = $"「{match.Groups["blueprint"].Value}」というブループリントは見つからない。";
            detail = "GameObjectFactoryMissingBlueprint";
            return true;
        }

        match = XrlGameMissingSavePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, XrlGameLoadGameOwner))
        {
            translated = $"セーブデータが存在しない。（{match.Groups["path"].Value}）";
            detail = "XrlGameMissingSave";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string? CurrentOwnerKey()
    {
        return ownerStack is { Count: > 0 } ? ownerStack.Peek() : null;
    }

    private static bool OwnerMatches(string? actual, params string[] expected)
    {
        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        for (var index = 0; index < expected.Length; index++)
        {
            if (string.Equals(actual, expected[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatOwnerKey(MethodBase method)
    {
        return (method.DeclaringType?.FullName ?? string.Empty) + "|" + method.Name;
    }

    private static string NormalizeRange(string source)
    {
        var trimmed = source.Trim();
        return trimmed switch
        {
            "zero" => "0",
            "one" => "1",
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "six" => "6",
            "seven" => "7",
            "eight" => "8",
            "nine" => "9",
            "ten" => "10",
            _ => trimmed,
        };
    }

    private static string TranslateAcquisitionAction(string source)
    {
        return source.Trim() switch
        {
            "found" => "見つけた",
            "taken" => "取った",
            "recovered" => "取り戻した",
            _ => source.Trim(),
        };
    }

    private static string TranslateToolboxStatus(string source)
    {
        return source.Trim() switch
        {
            "unpowered" => "電力が供給されていない",
            "still starting up" => "まだ起動中だ",
            "inoperative" => "動作していない",
            _ => source.Trim(),
        };
    }

    private static string TranslateToolboxContinuation(string source)
    {
        var trimmed = source.Trim();
        if (trimmed.Contains("full benefits"))
        {
            return "完全な利点なしで";
        }

        if (trimmed.Contains("benefits"))
        {
            return "利点なしで";
        }

        if (trimmed.Contains("without power"))
        {
            return "電力なしで使用して";
        }

        return trimmed + " ";
    }

    private static string TranslateFallPreposition(string source)
    {
        return source.Trim() switch
        {
            "down" => "下へ",
            "into" => "中へ",
            "through" => "通り抜けて",
            _ => source.Trim() + " ",
        };
    }

    private static bool TryTranslateDirectionPhrase(string source, out string translated)
    {
        translated = source switch
        {
            "to the north" => "北側",
            "to the south" => "南側",
            "to the east" => "東側",
            "to the west" => "西側",
            "to the northeast" => "北東側",
            "to the northwest" => "北西側",
            "to the southeast" => "南東側",
            "to the southwest" => "南西側",
            "nearby" => "近く",
            "above" => "上方",
            "below" => "下方",
            "here" => "ここ",
            "somewhere" => "どこか",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static string TranslateFoodOrWaterStatus(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateFoodOrWaterStatusVisible);
    }

    private static string TranslateFoodOrWaterStatusVisible(string source)
    {
        return source switch
        {
            "Sated" => "満腹",
            "Hungry" => "空腹",
            "Wilted!" => "枯れた！",
            "Famished!" => "飢餓！",
            "Quenched" => "潤っている",
            "Thirsty" => "喉が渇いた",
            "Parched" => "乾き",
            "Dehydrated!" => "脱水！",
            "Desiccated!" => "干からびた！",
            "Dry" => "乾き",
            "Moist" => "潤い",
            "Wet" => "濡れ",
            "Soaked" => "びしょ濡れ",
            "Tumescent" => "膨満",
            _ => source,
        };
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }
}
