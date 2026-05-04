#if HAS_GAME_DLL || HAS_TMP
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using QudJP.Patches;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class TargetMethodResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
#if HAS_GAME_DLL
        _ = EnsureGameAssemblyLoaded();
        _ = EnsureManagedAssemblyLoaded("ZString");
#endif
#if HAS_TMP
        _ = EnsureManagedAssemblyLoaded("UnityEngine.CoreModule");
        _ = EnsureManagedAssemblyLoaded("UnityEngine.TextCoreFontEngineModule");
        _ = EnsureManagedAssemblyLoaded("UnityEngine.TextCoreTextEngineModule");
        _ = EnsureManagedAssemblyLoaded("UnityEngine.TextRenderingModule");
        _ = EnsureManagedAssemblyLoaded("UnityEngine.UI");
        _ = EnsureManagedAssemblyLoaded("Unity.TextMeshPro");
#endif
    }

#if HAS_GAME_DLL
    [TestCase(typeof(GetDisplayNamePatch), "GetFor", "XRL.World.GetDisplayNameEvent", "System.String", new[]
    {
        "XRL.World.GameObject",
        "System.String",
        "System.Int32",
        "System.String",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
    })]
    [TestCase(typeof(GetDisplayNameProcessPatch), "ProcessFor", "XRL.World.GetDisplayNameEvent", "System.String", new[] { "XRL.World.GameObject", "System.Boolean" })]
    [TestCase(typeof(LookTooltipContentPatch), "GenerateTooltipContent", "XRL.UI.Look", "System.String", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(DescriptionLongDescriptionPatch), "GetLongDescription", "XRL.World.Parts.Description", "System.Void", new[] { "System.Text.StringBuilder" })]
    [TestCase(typeof(UITextSkinTranslationPatch), "SetText", "XRL.UI.UITextSkin", "System.Boolean", new[] { "System.String" })]
    [TestCase(typeof(CharacterStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.CharacterStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(SaveManagementRowTranslationPatch), "setData", "SaveManagementRow", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(SavesApiReadSaveJsonTranslationPatch), "ReadSaveJson", "Qud.API.SavesAPI", "Qud.API.SaveGameInfo", new[] { "System.String", "System.String" })]
    [TestCase(typeof(TinkeringStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.TinkeringStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(BookLineTranslationPatch), "setData", "Qud.UI.BookLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(PickGameObjectScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.PickGameObjectScreen", "System.Void", new[] { "System.Boolean" })]
    [TestCase(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.InventoryAndEquipmentStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(InventoryLineTranslationPatch), "setData", "Qud.UI.InventoryLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(EquipmentLineTranslationPatch), "setData", "Qud.UI.EquipmentLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(JournalLineTranslationPatch), "setData", "Qud.UI.JournalLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TinkeringLineTranslationPatch), "setData", "Qud.UI.TinkeringLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TinkeringDetailsLineTranslationPatch), "setData", "Qud.UI.TinkeringDetailsLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(AbilityManagerLineTranslationPatch), "setData", "Qud.UI.AbilityManagerLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(PickGameObjectLineTranslationPatch), "setData", "Qud.UI.PickGameObjectLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(FilterBarCategoryButtonTranslationPatch), "SetCategory", "Qud.UI.FilterBarCategoryButton", "System.Void", new[] { "System.String", "System.String" })]
    [TestCase(typeof(CyberneticsTerminalScreenTranslationPatch), "Show", "Qud.UI.CyberneticsTerminalScreen", "System.Void", new string[0])]
    // Re-enable after cybernetics terminal patch is finalized (see PR feat/cybernetics-terminal-patches)
    // [TestCase(typeof(CyberneticsTerminalTextTranslationPatch), "Update", "XRL.UI.TerminalScreen", "System.Void", new string[0])]
    [TestCase(typeof(HelpRowTranslationPatch), "setData", "Qud.UI.HelpRow", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(HelpScreenTranslationPatch), "UpdateMenuBars", "Qud.UI.HelpScreen", "System.Void", new string[0])]
    [TestCase(typeof(KeybindRowTranslationPatch), "setData", "Qud.UI.KeybindRow", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(KeybindsScreenTranslationPatch), "QueryKeybinds", "Qud.UI.KeybindsScreen", "System.Void", new string[0])]
    [TestCase(typeof(XrlManualTranslationPatch), "RenderIndex", "XRL.Help.XRLManual", "System.Void", new[] { "System.Int32" })]
    [TestCase(typeof(InventoryScreenTranslationPatch), "Show", "XRL.UI.InventoryScreen", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(StatusScreenTranslationPatch), "Show", "XRL.UI.StatusScreen", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(JournalScreenTranslationPatch), "Show", "XRL.UI.JournalScreen", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(TinkeringScreenTranslationPatch), "Show", "XRL.UI.TinkeringScreen", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject", "XRL.World.GameObject", "XRL.World.IEvent" })]
    [TestCase(typeof(QuestLogGamepadPromptTranslationPatch), "Show", "XRL.UI.QuestLog", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(FactionsScreenGamepadPromptTranslationPatch), "Show", "XRL.UI.FactionsScreen", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(SkillsAndPowersScreenTranslationPatch), "Show", "XRL.UI.SkillsAndPowersScreen", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(EquipmentScreenTranslationPatch), "Show", "XRL.UI.EquipmentScreen", "XRL.UI.ScreenReturn", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(CharacterStatusScreenMutationDetailsPatch), "HandleHighlightMutation", "Qud.UI.CharacterStatusScreen", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(StatisticGetHelpTextPatch), "GetHelpText", "XRL.World.Statistic", "System.String", new string[0])]
    [TestCase(typeof(ChargenAttributeDescriptionTranslationPatch), "handleUIEvent", "XRL.CharacterBuilds.Qud.QudGenotypeModule", "System.Object", new[] { "System.String", "System.Object" })]
    [TestCase(typeof(CharacterStatusScreenAttributeHighlightPatch), "HandleHighlightAttribute", "Qud.UI.CharacterStatusScreen", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(SkillsAndPowersStatusScreenDetailsPatch), "UpdateDetailsFromNode", "Qud.UI.SkillsAndPowersStatusScreen", "System.Void", new[] { "XRL.UI.SPNode" })]
    [TestCase(typeof(PopupShowSpaceTranslationPatch), "ShowSpace", "XRL.UI.Popup", "System.Void", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "ConsoleLib.Console.Renderable",
        "System.Boolean",
        "System.Boolean",
        "System.String",
    })]
    [TestCase(typeof(PopupPickOptionTranslationPatch), "PickOption", "XRL.UI.Popup", "System.Int32", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "System.String",
        "System.Collections.Generic.IReadOnlyList`1[[System.String]]",
        "System.Collections.Generic.IReadOnlyList`1[[System.Char]]",
        "System.Collections.Generic.IReadOnlyList`1[[ConsoleLib.Console.IRenderable]]",
        "System.Collections.Generic.IReadOnlyList`1[[Qud.UI.QudMenuItem]]",
        "XRL.World.GameObject",
        "ConsoleLib.Console.IRenderable",
        "System.Action`1[[System.Int32]]",
        "System.Int32",
        "System.Int32",
        "System.Int32",
        "System.Int32",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "Genkit.Location2D",
        "System.String",
    })]
    [TestCase(typeof(PopupMessageTranslationPatch), "ShowPopup", "Qud.UI.PopupMessage", "System.Void", new[]
    {
        "System.String",
        "System.Collections.Generic.List`1[[Qud.UI.QudMenuItem]]",
        "System.Action`1[[Qud.UI.QudMenuItem]]",
        "System.Collections.Generic.List`1[[Qud.UI.QudMenuItem]]",
        "System.Action`1[[Qud.UI.QudMenuItem]]",
        "System.String",
        "System.Boolean",
        "System.String",
        "System.Int32",
        "System.Action",
        "ConsoleLib.Console.IRenderable",
        "System.String",
        "ConsoleLib.Console.IRenderable",
        "System.Boolean",
        "System.Boolean",
        "System.Threading.CancellationToken",
        "System.Boolean",
        "System.String",
        "System.String",
        "Genkit.Location2D",
        "System.String",
    })]
    [TestCase(typeof(PopupGetPopupOptionTranslationPatch), "GetPopupOption", "XRL.UI.Popup", "Qud.UI.QudMenuItem", new[]
    {
        "System.Int32",
        "System.Collections.Generic.IReadOnlyList`1[[System.String]]",
        "System.Collections.Generic.IReadOnlyList`1[[System.Char]]",
        "System.Collections.Generic.IReadOnlyList`1[[ConsoleLib.Console.IRenderable]]",
    })]
    [TestCase(typeof(AbilityBarUpdateAbilitiesTextPatch), "UpdateAbilitiesText", "Qud.UI.AbilityBar", "System.Void", new string[0])]
    [TestCase(typeof(AbilityBarButtonTextTranslationPatch), "Update", "Qud.UI.AbilityBar", "System.Void", new string[0])]
    [TestCase(typeof(CherubimSpawnerReplaceDescriptionPatch), "ReplaceDescription", "XRL.World.Parts.CherubimSpawner", "System.Void", new[] { "XRL.World.GameObject", "System.String", "System.String" })]
    [TestCase(typeof(CharacterStatusScreenHighlightEffectPatch), "HandleHighlightEffect", "Qud.UI.CharacterStatusScreen", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(GameObjectShowActiveEffectsPatch), "ShowActiveEffects", "XRL.World.GameObject", "System.Void", new string[0])]
    [TestCase(typeof(DescriptionShortDescriptionPatch), "GetShortDescription", "XRL.World.Parts.Description", "System.String", new[] { "System.Boolean", "System.Boolean", "System.String" })]
    [TestCase(typeof(FactionsLineDataTranslationPatch), "set", "Qud.UI.FactionsLineData", "Qud.UI.FactionsLineData", new[] { "System.String", "System.String", "ConsoleLib.Console.IRenderable", "System.Boolean" })]
    [TestCase(typeof(FactionsLineTranslationPatch), "setData", "Qud.UI.FactionsLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(QudMutationsModuleWindowTranslationPatch), "UpdateControls", "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow", "System.Void", new string[0])]
    [TestCase(typeof(SummaryBlockControlTranslationPatch), "setData", "XRL.UI.Framework.SummaryBlockControl", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TradeLineTranslationPatch), "setData", "Qud.UI.TradeLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(SkillsAndPowersStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.SkillsAndPowersStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(MessageLogPatch), "AddPlayerMessage", "XRL.Messages.MessageQueue", "System.Void", new[] { "System.String", "System.String", "System.Boolean" })]
    [TestCase(typeof(MessageLogStatusScreenTranslationPatch), "GetTabString", "Qud.UI.MessageLogStatusScreen", "System.String", new string[0])]
    [TestCase(typeof(MessageLogLineTranslationPatch), "setData", "Qud.UI.MessageLogLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TutorialManagerTranslationPatch), "ShowCIDPopupAsync", "TutorialManager", "System.Threading.Tasks.Task", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "System.String",
        "System.Int32",
        "System.Int32",
        "System.Single",
        "System.Action",
    })]
    [TestCase(typeof(PhysicsEnterCellPassByTranslationPatch), "AddPlayerMessage", "XRL.Messages.MessageQueue", "System.Void", new[] { "System.String", "System.String", "System.Boolean" })]
    [TestCase(typeof(ZoneManagerSetActiveZoneMessageQueuePatch), "AddPlayerMessage", "XRL.Messages.MessageQueue", "System.Void", new[] { "System.String", "System.String", "System.Boolean" })]
    [TestCase(typeof(XrlCoreLostSightTranslationPatch), "RenderBaseToBuffer", "XRL.Core.XRLCore", "System.Void", new[] { "ConsoleLib.Console.ScreenBuffer" })]
    [TestCase(typeof(ZoneManagerSetActiveZoneTranslationPatch), "SetActiveZone", "XRL.World.ZoneManager", "XRL.World.Zone", new[] { "XRL.World.Zone" })]
    [TestCase(typeof(JournalEntryDisplayTextPatch), "GetDisplayText", "Qud.API.IBaseJournalEntry", "System.String", new string[0])]
    [TestCase(typeof(JournalMapNoteDisplayTextPatch), "GetDisplayText", "Qud.API.JournalMapNote", "System.String", new string[0])]
    [TestCase(typeof(JournalAccomplishmentAddTranslationPatch), "AddAccomplishment", "Qud.API.JournalAPI", "System.Void", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "System.String",
        "System.String",
        "Qud.API.MuralCategory",
        "Qud.API.MuralWeight",
        "System.String",
        "System.Int64",
        "System.Boolean",
    })]
    [TestCase(typeof(JournalMapNoteAddTranslationPatch), "AddMapNote", "Qud.API.JournalAPI", "System.Void", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "System.String[]",
        "System.String",
        "System.Boolean",
        "System.Boolean",
        "System.Int64",
        "System.Boolean",
    })]
    [TestCase(typeof(JournalObservationAddTranslationPatch), "AddObservation", "Qud.API.JournalAPI", "System.Void", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "System.String",
        "System.String[]",
        "System.Boolean",
        "System.Int64",
        "System.String",
        "System.Boolean",
        "System.Boolean",
    })]
    [TestCase(typeof(BaseLineWithTooltipStartTooltipPatch), "StartTooltip", "Qud.UI.BaseLineWithTooltip", "System.Void", new[] { "XRL.World.GameObject", "XRL.World.GameObject", "System.Boolean", "UnityEngine.RectTransform" })]
    [TestCase(typeof(DoesFragmentMarkingPatch), "Does", "XRL.World.GameObject", "System.String", new[]
    {
        "System.String",
        "System.Int32",
        "System.String",
        "System.String",
        "System.String",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.String",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Nullable`1[[System.Boolean]]",
        "System.Boolean",
        "XRL.World.GameObject",
        "System.Boolean",
    })]
    [TestCase(typeof(GrammarMakeAndListPatch), "MakeAndList", "XRL.Language.Grammar", "System.String", new[] { "System.Collections.Generic.IReadOnlyList`1[[System.String]]", "System.Boolean" })]
    [TestCase(typeof(GrammarInitCapsPatch), "InitCap", "XRL.Language.Grammar", "System.String", new[] { "System.String" })]
    [TestCase(typeof(GrammarCardinalNumberPatch), "Cardinal", "XRL.Language.Grammar", "System.String", new[] { "System.Int32" })]
    [TestCase(typeof(QudMenuBottomContextTranslationPatch), "RefreshButtons", "Qud.UI.QudMenuBottomContext", "System.Void", new string[0])]
    [TestCase(typeof(ModManagerUITranslationPatch), "OnSelect", "Qud.UI.ModManagerUI", "System.Void", new[] { "XRL.ModInfo" })]
    [TestCase(typeof(SelectableTextMenuItemProbePatch), "Update", "Qud.UI.SelectableTextMenuItem", "System.Void", new string[0])]
    [TestCase(typeof(LoadingStatusTranslationPatch), "SetLoadingStatus", "XRL.UI.Loading", "System.Void", new[] { "System.String", "System.Boolean" })]
    [TestCase(typeof(CombatAndLogMessageQueuePatch), "AddPlayerMessage", "XRL.Messages.MessageQueue", "System.Void", new[] { "System.String", "System.String", "System.Boolean" })]
    [TestCase(typeof(CombatGetDefenderHitDiceTranslationPatch), "HandleEvent", "XRL.World.Parts.Combat", "System.Boolean", new[] { "XRL.World.GetDefenderHitDiceEvent" })]
    [TestCase(typeof(DoorAttemptOpenTranslationPatch), "AttemptOpen", "XRL.World.Parts.Door", "System.Boolean", new[]
    {
        "XRL.World.GameObject",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "XRL.World.IEvent",
    })]
    [TestCase(typeof(PhysicsObjectEnteringCellTranslationPatch), "HandleEvent", "XRL.World.Parts.Physics", "System.Boolean", new[] { "XRL.World.ObjectEnteringCellEvent" })]
    [TestCase(typeof(PhysicsApplyDischargeTranslationPatch), "ApplyDischarge", "XRL.World.Parts.Physics", "System.Int32", new[]
    {
        "XRL.World.Cell",
        "XRL.World.Cell",
        "System.Int32",
        "System.Int32",
        "System.String",
        "XRL.Rules.DieRoll",
        "XRL.World.GameObject",
        "System.Collections.Generic.List`1[[XRL.World.Cell]]",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "System.Collections.Generic.List`1[[XRL.World.GameObject]]",
        "System.Nullable`1[[System.Boolean]]",
        "System.String",
        "System.String",
        "System.Int32",
        "System.Boolean",
        "System.Boolean",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "System.String",
        "System.Boolean",
    })]
    [TestCase(typeof(GameObjectHealTranslationPatch), "Heal", "XRL.World.GameObject", "System.Int32", new[] { "System.Int32", "System.Boolean", "System.Boolean", "System.Boolean" })]
    [TestCase(typeof(GameObjectMoveTranslationPatch), "Move", "XRL.World.GameObject", "System.Boolean", new[]
    {
        "System.String",
        "XRL.World.GameObject&",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "System.Boolean",
        "System.Nullable`1[[System.Int32]]",
        "System.String",
        "System.Nullable`1[[System.Int32]]",
        "System.Boolean",
        "System.Boolean",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "System.Int32",
    })]
    [TestCase(typeof(GameObjectPerformThrowTranslationPatch), "PerformThrow", "XRL.World.GameObject", "System.Boolean", new[]
    {
        "XRL.World.GameObject",
        "XRL.World.Cell",
        "XRL.World.GameObject",
        "XRL.World.Parts.MissilePath",
        "System.Int32",
        "System.Nullable`1[[System.Int32]]",
        "System.Nullable`1[[System.Int32]]",
        "System.Nullable`1[[System.Int32]]",
    })]
    [TestCase(typeof(GameObjectSpotTranslationPatch), "ArePerceptibleHostilesNearby", "XRL.World.GameObject", "System.Boolean", new[]
    {
        "System.Boolean",
        "System.Boolean",
        "System.String",
        "XRL.OngoingAction",
        "System.String",
        "System.Int32",
        "System.Int32",
        "System.Boolean",
        "System.Boolean",
    })]
    [TestCase(typeof(GameObjectDieTranslationPatch), "Die", "XRL.World.GameObject", "System.Boolean", new[]
    {
        "XRL.World.GameObject",
        "System.String",
        "System.String",
        "System.String",
        "System.Boolean",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "System.Boolean",
        "System.Boolean",
        "System.String",
        "System.String",
        "System.String",
    })]
    [TestCase(typeof(GameObjectRegeneraTranslationPatch), "FireEvent", "XRL.World.GameObject", "System.Boolean", new[] { "XRL.World.Event" })]
    [TestCase(typeof(GameObjectToggleActivatedAbilityTranslationPatch), "ToggleActivatedAbility", "XRL.World.GameObject", "System.Boolean", new[] { "System.Guid", "System.Boolean", "System.Nullable`1[[System.Boolean]]" })]
    [TestCase(typeof(ZoneManagerTryThawZoneTranslationPatch), "TryThawZone", "XRL.World.ZoneManager", "System.Boolean", new[] { "System.String", "XRL.World.Zone&" })]
    [TestCase(typeof(ZoneManagerTickTranslationPatch), "Tick", "XRL.World.ZoneManager", "System.Void", new[] { "System.Boolean" })]
    [TestCase(typeof(ZoneManagerSetActiveZoneMapNotesTranslationPatch), "SetActiveZone", "XRL.World.ZoneManager", "XRL.World.Zone", new[] { "XRL.World.Zone" })]
    [TestCase(typeof(ZoneManagerGenerateZoneTranslationPatch), "GenerateZone", "XRL.World.ZoneManager", "System.Void", new[] { "System.String" })]
    [TestCase(typeof(BedTranslationPatch), "AttemptSleep", "XRL.World.Parts.Bed", "System.Void", new[] { "XRL.World.GameObject", "System.Boolean&", "System.Boolean&", "System.Boolean&" })]
    [TestCase(typeof(ChairTranslationPatch), "SitDown", "XRL.World.Parts.Chair", "System.Boolean", new[] { "XRL.World.GameObject", "XRL.World.IEvent" })]
    [TestCase(typeof(EnclosingTranslationPatch), "ExitEnclosure", "XRL.World.Parts.Enclosing", "System.Boolean", new[] { "XRL.World.GameObject", "XRL.World.IEvent", "XRL.World.Effects.Enclosed" })]
    [TestCase(typeof(GameSummaryScreenMenuBarsTranslationPatch), "UpdateMenuBars", "Qud.UI.GameSummaryScreen", "System.Void", new string[0])]
    [TestCase(typeof(GameSummaryScreenShowTranslationPatch), "_ShowGameSummary", "Qud.UI.GameSummaryScreen", "System.Threading.Tasks.Task`1[[System.Boolean]]", new[] { "System.String", "System.String", "System.String", "System.Boolean" })]
    [TestCase(typeof(GivesRepShortDescriptionTranslationPatch), "HandleEvent", "XRL.World.Parts.GivesRep", "System.Boolean", new[] { "XRL.World.GetShortDescriptionEvent" })]
    [TestCase(typeof(MutationsApiTranslationPatch), "BuyRandomMutation", "Qud.API.MutationsAPI", "System.Boolean", new[] { "XRL.World.GameObject", "System.Int32", "System.Boolean", "System.String" })]
    [TestCase(typeof(ConversationPronounExchangeTranslationPatch), "PronounExchangeDescription", "XRL.World.Parts.ConversationScript", "System.String", new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
    })]
#endif
#if HAS_TMP
    [TestCase(typeof(TextMeshProUguiFontPatch), "OnEnable", "TMPro.TextMeshProUGUI", "System.Void", new string[0])]
    [TestCase(typeof(TextMeshProFontPatch), "OnEnable", "TMPro.TextMeshPro", "System.Void", new string[0])]
    [TestCase(typeof(TmpInputFieldFontPatch), "OnEnable", "TMPro.TMP_InputField", "System.Void", new string[0])]
    [TestCase(typeof(LegacyUITextFontPatch), "OnEnable", "UnityEngine.UI.Text", "System.Void", new string[0])]
    [TestCase(typeof(ModMenuLineTranslationPatch), "Update", "Qud.UI.ModMenuLine", "System.Void", new string[0])]
#endif
    public void TargetMethod_ResolvesExpectedSignature(
        Type patchType,
        string expectedMethodName,
        string expectedDeclaringType,
        string expectedReturnType,
        string[] expectedParameterTypes)
    {
        var targetMethod = InvokeTargetMethod(patchType);

        Assert.Multiple(() =>
        {
            Assert.That(targetMethod, Is.Not.Null, $"TargetMethod returned null for {patchType.FullName}");
            Assert.That(targetMethod!.Name, Is.EqualTo(expectedMethodName));
            Assert.That(targetMethod.DeclaringType?.FullName, Is.EqualTo(expectedDeclaringType));

            var methodInfo = targetMethod as MethodInfo;
            Assert.That(methodInfo, Is.Not.Null, $"Expected MethodInfo for {patchType.FullName}");
            Assert.That(NormalizeTypeName(methodInfo!.ReturnType.FullName), Is.EqualTo(expectedReturnType));

            var parameterTypes = Array.ConvertAll(methodInfo.GetParameters(), static parameter => NormalizeTypeName(parameter.ParameterType.FullName));
            Assert.That(parameterTypes, Is.EqualTo(expectedParameterTypes));
        });
    }

#if HAS_GAME_DLL
    [TestCase(typeof(SinkPrereqSetDataTranslationPatch), new[]
    {
        "XRL.UI.Framework.FrameworkDataElement",
        "XRL.UI.Framework.FrameworkDataElement",
        "XRL.UI.Framework.FrameworkDataElement",
        "XRL.UI.Framework.FrameworkDataElement",
        "XRL.UI.Framework.FrameworkDataElement",
    })]
    [TestCase(typeof(SinkPrereqUiMethodTranslationPatch), new[]
    {
        "XRL.UI.Framework.FrameworkDataElement",
        "XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor|System.Collections.Generic.IEnumerable`1[[XRL.UI.Framework.FrameworkDataElement]]",
        "XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor|System.Collections.Generic.IEnumerable`1[[XRL.UI.Framework.FrameworkDataElement]]",
        "",
        "",
    })]
    [TestCase(typeof(GrammarMakeOrListPatch), new[]
    {
        "System.String[]|System.Boolean",
        "System.Collections.Generic.List`1[[System.String]]|System.Boolean",
    })]
    [TestCase(typeof(PopupTranslationPatch), new[]
    {
        "System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "System.String|ConsoleLib.Console.IRenderable|System.String|System.Collections.Generic.List`1[[System.String]]|System.Boolean|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(PopupShowTranslationPatch), new[]
    {
        "System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "System.String|System.Boolean|System.Boolean|System.Boolean",
        "System.String|System.String|System.Boolean|XRL.UI.DialogResult",
        "System.String",
        "System.String|System.String|System.Boolean|XRL.UI.DialogResult",
        "System.String",
    })]
    [TestCase(typeof(ZoneDisplayNameTranslationPatch), new[]
    {
        "System.String|System.Int32|XRL.World.ZoneBlueprint|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
        "System.String|System.String|System.Int32|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
        "System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(MainMenuLocalizationPatch), new[]
    {
        "",
        "",
    })]
    [TestCase(typeof(CreditsMenuBarsTranslationPatch), new[]
    {
        "",
    })]
    [TestCase(typeof(GameObjectEmitMessageTranslationPatch), new[]
    {
        "System.String|XRL.World.GameObject|System.String|System.Boolean",
        "XRL.World.GameObject|System.String|System.Char|System.Boolean|System.Boolean|System.Boolean|XRL.World.GameObject|XRL.World.GameObject",
    })]
    [TestCase(typeof(BookScreenTranslationPatch), new[]
    {
        "XRL.World.Parts.MarkovBook|System.String|System.Action`1[[System.Int32]]|System.Action`1[[System.Int32]]",
        "System.String|System.String|System.Action`1[[System.Int32]]|System.Action`1[[System.Int32]]",
    })]
    [TestCase(typeof(ConversationDisplayTextPatch), new[]
    {
        "System.Boolean",
        "System.Boolean",
    })]
    [TestCase(typeof(ConversationSimpleTemplateTranslationPatch), new[]
    {
        "XRL.World.GameObject|System.String|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(ConversationQuestionTemplateTranslationPatch), new[]
    {
        "XRL.World.GameObject|System.String|System.String|System.String|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(DescriptionInspectStatusPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
    })]
    [TestCase(typeof(TradeScreenUiTranslationPatch), new[]
    {
        "",
        "System.String|System.Int32|System.Int32|System.Int32|System.String|System.Boolean",
        "System.Double[]|System.Int32[]|System.Collections.Generic.List`1[[XRL.UI.TradeEntry]][]|System.Int32[][]",
    })]
    [TestCase(typeof(TradeUiPopupTranslationPatch), new[]
    {
        "System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "System.String|System.String|System.Boolean|XRL.UI.DialogResult",
    })]
    [TestCase(typeof(SteamWorkshopUploaderViewTranslationPatch), new string[0])]
    [TestCase(typeof(ModInfoTranslationPatch), new[]
    {
        "",
        "",
        "",
        "Cysharp.Text.Utf16ValueStringBuilder&|System.String|System.String",
    })]
    [TestCase(typeof(ModScrollerOneTranslationPatch), new[]
    {
        "XRL.ModInfo",
    })]
    [TestCase(typeof(PopupAskStringTranslationPatch), new[]
    {
        "System.String|System.String|System.String|System.String|System.String|System.Int32|System.Int32|System.Boolean|System.Boolean|System.Nullable`1[[System.Boolean]]",
        "System.String|System.String|System.Int32|System.Int32|System.String|System.Boolean|System.Boolean|System.Nullable`1[[System.Boolean]]|System.Boolean|System.String",
    })]
    [TestCase(typeof(PopupAskNumberTranslationPatch), new[]
    {
        "System.String|System.String|System.String|System.Int32|System.Int32|System.Int32",
        "System.String|System.Int32|System.Int32|System.Int32|System.String|System.Boolean",
    })]
    [TestCase(typeof(LiquidVolumeTranslationPatch), new[]
    {
        "XRL.World.InventoryActionEvent",
        "System.Boolean&|XRL.World.GameObject|XRL.World.Cell|System.Boolean|System.Boolean|System.Int32|System.Boolean",
        "XRL.World.GameObject|System.Boolean&|System.Boolean",
    })]
    [TestCase(typeof(ClonelingVehicleTranslationPatch), new[]
    {
        "XRL.World.InventoryActionEvent",
        "",
        "XRL.World.InventoryActionEvent",
        "XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(CookingEffectTranslationPatch), new[]
    {
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
    })]
    [TestCase(typeof(AsleepMessageTranslationPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.BeginTakeActionEvent",
    })]
    public void TargetMethods_ResolveExpectedOverloads(Type patchType, string[] expectedSignatures)
    {
        var targetMethodsMethod = patchType.GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, $"TargetMethods not found for {patchType.FullName}");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, $"TargetMethods returned null for {patchType.FullName}");

        var actualSignatures = new List<string>();
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            var signature = string.Join("|", Array.ConvertAll(methodInfo.GetParameters(), static parameter => NormalizeTypeName(parameter.ParameterType.FullName)));
            actualSignatures.Add(signature);
        }

        Assert.That(actualSignatures, Is.EquivalentTo(expectedSignatures));
    }

#if HAS_GAME_DLL
    [Test]
    public void ActiveEffectOwnerPatches_TargetBaseAndOverridesButNotCookingOwnerMethods()
    {
        var descriptionTargets = ResolveTargetMethodNames(typeof(EffectDescriptionPatch));
        var detailsTargets = ResolveTargetMethodNames(typeof(EffectDetailsPatch));

        Assert.Multiple(() =>
        {
            Assert.That(descriptionTargets, Does.Contain("XRL.World.Effect|GetDescription"));
            Assert.That(descriptionTargets, Does.Contain("XRL.World.Effects.LiquidCovered|GetDescription"));
            Assert.That(descriptionTargets, Does.Contain("XRL.World.Effects.Swimming|GetDescription"));
            Assert.That(descriptionTargets, Does.Not.Contain("XRL.World.Effects.CookingDomainLove_UnitEgo|GetDescription"));

            Assert.That(detailsTargets, Does.Contain("XRL.World.Effect|GetDetails"));
            Assert.That(detailsTargets, Does.Contain("XRL.World.Effects.LiquidCovered|GetDetails"));
            Assert.That(detailsTargets, Does.Contain("XRL.World.Effects.Swimming|GetDetails"));
            Assert.That(detailsTargets, Does.Not.Contain("XRL.World.Effects.BasicCookingEffect_XP|GetDetails"));
        });
    }

    [Test]
    public void SinkPrereqPatches_DoNotRetargetDedicatedOwnerSurfaces()
    {
        var uiMethodTargets = ResolveTargetMethodNames(typeof(SinkPrereqUiMethodTranslationPatch));
        var setDataTargets = ResolveTargetMethodNames(typeof(SinkPrereqSetDataTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(uiMethodTargets, Does.Not.Contain("Qud.UI.AbilityManagerScreen|HandleHighlightLeft"));
            Assert.That(uiMethodTargets, Does.Not.Contain("Qud.UI.TradeScreen|HandleHighlightObject"));
            Assert.That(uiMethodTargets, Does.Not.Contain("Qud.UI.TradeScreen|UpdateTitleBars"));
            Assert.That(uiMethodTargets, Does.Not.Contain("Qud.UI.PlayerStatusBar|Update"));
            Assert.That(uiMethodTargets, Does.Not.Contain("MapScrollerPinItem|SetData"));

            Assert.That(setDataTargets, Does.Not.Contain("Qud.UI.CharacterAttributeLine|setData"));
            Assert.That(setDataTargets, Does.Not.Contain("Qud.UI.CharacterEffectLine|setData"));
            Assert.That(setDataTargets, Does.Not.Contain("Qud.UI.TinkeringDetailsLine|setData"));
            Assert.That(setDataTargets, Does.Not.Contain("XRL.UI.Framework.SummaryBlockControl|setData"));
            Assert.That(setDataTargets, Does.Not.Contain("Qud.UI.TradeLine|setData"));
        });
    }
#endif

    [TestCase(typeof(PopupTranslationPatch), new[]
    {
        "XRL.UI.Popup|ShowBlock|ConsoleLib.Console.Keys|System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "XRL.UI.Popup|ShowConversation|System.Int32|System.String|ConsoleLib.Console.IRenderable|System.String|System.Collections.Generic.List`1[[System.String]]|System.Boolean|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(PopupShowTranslationPatch), new[]
    {
        "XRL.UI.Popup|Show|System.Void|System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "XRL.UI.Popup|ShowFail|System.Void|System.String|System.Boolean|System.Boolean|System.Boolean",
        "XRL.UI.Popup|ShowYesNo|XRL.UI.DialogResult|System.String|System.String|System.Boolean|XRL.UI.DialogResult",
        "XRL.UI.Popup|ShowYesNoAsync|System.Threading.Tasks.Task`1[[XRL.UI.DialogResult]]|System.String",
        "XRL.UI.Popup|ShowYesNoCancel|XRL.UI.DialogResult|System.String|System.String|System.Boolean|XRL.UI.DialogResult",
        "XRL.UI.Popup|ShowYesNoCancelAsync|System.Threading.Tasks.Task`1[[XRL.UI.DialogResult]]|System.String",
    })]
    [TestCase(typeof(TradeUiPopupTranslationPatch), new[]
    {
        "XRL.UI.Popup|Show|System.Void|System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "XRL.UI.Popup|ShowBlock|ConsoleLib.Console.Keys|System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|Genkit.Location2D",
        "XRL.UI.Popup|ShowYesNo|XRL.UI.DialogResult|System.String|System.String|System.Boolean|XRL.UI.DialogResult",
    })]
    [TestCase(typeof(PopupAskStringTranslationPatch), new[]
    {
        "XRL.UI.Popup|AskString|System.String|System.String|System.String|System.String|System.String|System.String|System.Int32|System.Int32|System.Boolean|System.Boolean|System.Nullable`1[[System.Boolean]]",
        "XRL.UI.Popup|AskStringAsync|System.Threading.Tasks.Task`1[[System.String]]|System.String|System.String|System.Int32|System.Int32|System.String|System.Boolean|System.Boolean|System.Nullable`1[[System.Boolean]]|System.Boolean|System.String",
    })]
    [TestCase(typeof(PopupAskNumberTranslationPatch), new[]
    {
        "XRL.UI.Popup|AskNumber|System.Nullable`1[[System.Int32]]|System.String|System.String|System.String|System.Int32|System.Int32|System.Int32",
        "XRL.UI.Popup|AskNumberAsync|System.Threading.Tasks.Task`1[[System.Nullable`1[[System.Int32]]]]|System.String|System.Int32|System.Int32|System.Int32|System.String|System.Boolean",
    })]
    public void PopupTargetMethods_ResolveExpectedNamedNonObsoleteOverloads(Type patchType, string[] expectedSignatures)
    {
        var targetMethodsMethod = patchType.GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, $"TargetMethods not found for {patchType.FullName}");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, $"TargetMethods returned null for {patchType.FullName}");

        var actualSignatures = new List<string>();
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            Assert.That(
                methodInfo.GetCustomAttribute<ObsoleteAttribute>(),
                Is.Null,
                $"{patchType.FullName} resolved obsolete popup method {methodInfo.DeclaringType?.FullName}.{methodInfo.Name}.");

            actualSignatures.Add(FullMethodSignature(methodInfo));
        }

        Assert.That(actualSignatures, Is.EquivalentTo(expectedSignatures));
    }

    private static string FullMethodSignature(MethodInfo methodInfo)
    {
        return string.Join(
            "|",
            new[]
            {
                methodInfo.DeclaringType?.FullName ?? string.Empty,
                methodInfo.Name,
                NormalizeTypeName(methodInfo.ReturnType.FullName),
            }.Concat(Array.ConvertAll(
                methodInfo.GetParameters(),
                static parameter => NormalizeTypeName(parameter.ParameterType.FullName))));
    }

    [Test]
    public void ConversationDisplayTextPatch_TargetMethods_ResolveBaseAndChoice()
    {
        var targetMethodsMethod = typeof(ConversationDisplayTextPatch).GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, "TargetMethods not found for ConversationDisplayTextPatch");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, "TargetMethods returned null for ConversationDisplayTextPatch");

        var declaringTypes = new List<string>();
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            declaringTypes.Add(NormalizeTypeName(methodInfo.DeclaringType?.FullName));
        }

        Assert.That(declaringTypes, Is.EquivalentTo(new[]
        {
            "XRL.World.Conversations.IConversationElement",
            "XRL.World.Conversations.Choice",
        }));
    }

    [TestCase("XRL.World.Conversations.ConversationLoader", "LoadConversations", 0)]
    [TestCase("XRL.World.Conversations.ConversationLoader", "ReadConversation", 2)]
    [TestCase("XRL.World.GameObjectFactory", "LoadBlueprints", 0)]
    [TestCase("XRL.World.GameObjectFactory", "LoadBakedXML", 1)]
    public void HookInventoryProbe_ResolvesXmlLoaderMethods(
        string declaringTypeName,
        string methodName,
        int parameterCount)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var declaringType = assembly.GetType(declaringTypeName, throwOnError: false);
        Assert.That(declaringType, Is.Not.Null, $"Type not found: {declaringTypeName}");

        var method = FindMethodByNameAndParameterCount(declaringType!, methodName, parameterCount);
        Assert.That(
            method,
            Is.Not.Null,
            $"Method not found: {declaringTypeName}.{methodName} with {parameterCount} parameter(s)");
    }

    [TestCase("XRL.UI.Popup", "ShowConversation", 7, "System.Int32", true)]
    [TestCase("XRL.GameText", "VariableReplace", 4, "System.String")]
    [TestCase("XRL.GameText", "Process", 6, "System.Void")]
    [TestCase("XRL.World.Text.ReplaceBuilder", "Process", 0, "System.Void")]
    [TestCase("XRL.World.DescriptionBuilder", "ToString", 0, "System.String")]
    public void Issue29Probe_ResolvesUpstreamCandidateMethods(
        string declaringTypeName,
        string methodName,
        int parameterCount,
        string expectedReturnType,
        bool expectNonObsolete = false)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var declaringType = assembly.GetType(declaringTypeName, throwOnError: false);
        Assert.That(declaringType, Is.Not.Null, $"Type not found: {declaringTypeName}");

        var method = FindMethodByNameAndParameterCount(declaringType!, methodName, parameterCount, expectNonObsolete) as MethodInfo;
        Assert.Multiple(() =>
        {
            Assert.That(
                method,
                Is.Not.Null,
                $"Method not found: {declaringTypeName}.{methodName} with {parameterCount} parameter(s)");
            Assert.That(method?.ReturnType.FullName, Is.EqualTo(expectedReturnType));
            if (expectNonObsolete)
            {
                Assert.That(method?.IsDefined(typeof(ObsoleteAttribute), inherit: false), Is.False);
            }
        });
    }

    [TestCase("XRL.Messages.Messaging", "XRL.World.Messaging", "XRL.UI.Messaging")]
    [TestCase("XRL.World.Conversations.ConversationUI", "XRL.World.ConversationUI", null)]
    public void NamespaceProbe_DocumentsCurrentDecompilationGapCandidates(
        string firstCandidateTypeName,
        string secondCandidateTypeName,
        string? thirdCandidateTypeName)
    {
        string?[] candidateTypeNames =
        {
            firstCandidateTypeName,
            secondCandidateTypeName,
            thirdCandidateTypeName,
        };

        var assembly = EnsureGameAssemblyLoaded();
        var resolvedTypeName = Array.Find(
            candidateTypeNames,
            candidateTypeName => candidateTypeName is not null
                && assembly.GetType(candidateTypeName, throwOnError: false) is not null);

        Assert.That(
            resolvedTypeName,
            Is.Null,
            $"Expected current decompilation-gap candidates to remain unresolved: {string.Join(", ", candidateTypeNames)}");
    }

    [TestCase("QudGenotypeModule")]
    [TestCase("QudMutationsModule")]
    [TestCase("QudCyberneticsModule")]
    [TestCase("EmbarkBuilder")]
    public void CharGenProbe_ResolvesKnownSimpleTypeNames(string simpleTypeName)
    {
        AssertSimpleTypeNameResolves(simpleTypeName);
    }

    [TestCase("CharacterStatusScreen")]
    [TestCase("FactionsStatusScreen")]
    [TestCase("SkillsAndPowersStatusScreen")]
    [TestCase("InventoryAndEquipmentStatusScreen")]
    [TestCase("JournalStatusScreen")]
    [TestCase("MessageLogStatusScreen")]
    [TestCase("QuestsStatusScreen")]
    [TestCase("TinkeringStatusScreen")]
    [TestCase("StatusScreensScreen")]
    public void Issue29Probe_ResolvesKnownStatusScreenTypeNames(string simpleTypeName)
    {
        AssertSimpleTypeNameResolves(simpleTypeName);
    }

    [TestCase("Qud.UI.CharacterStatusScreen")]
    [TestCase("Qud.UI.FactionsStatusScreen")]
    [TestCase("Qud.UI.JournalStatusScreen")]
    [TestCase("Qud.UI.MessageLogStatusScreen")]
    public void Issue29Probe_ResolvesKnownQualifiedStatusScreenTypes(string typeName)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var resolvedType = assembly.GetType(typeName, throwOnError: false);

        Assert.That(resolvedType, Is.Not.Null, $"Type not found: {typeName}");
    }

    [Test]
    public void Issue29Probe_ResolvesDescriptionBuilderSurfaceMethods()
    {
        var assembly = EnsureGameAssemblyLoaded();
        var descriptionBuilderType = FindTypeBySimpleName(assembly, "DescriptionBuilder");
        Assert.That(descriptionBuilderType, Is.Not.Null, "Type not found by simple name: DescriptionBuilder");

        var methods = descriptionBuilderType!.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        var methodNames = new HashSet<string>(Array.ConvertAll(methods, static method => method.Name), StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(methodNames.Contains("AddAdjective"), Is.True, "DescriptionBuilder.AddAdjective not found.");
            Assert.That(methodNames.Contains("AddClause"), Is.True, "DescriptionBuilder.AddClause not found.");
            Assert.That(methodNames.Contains("ToString"), Is.True, "DescriptionBuilder.ToString not found.");
        });
    }

    [TestCase("PrimaryBase")]
    [TestCase("LastAdded")]
    public void Issue29Probe_DescriptionBuilderContainsStringField(string fieldName)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var descriptionBuilderType = assembly.GetType("XRL.World.DescriptionBuilder", throwOnError: false);
        Assert.That(descriptionBuilderType, Is.Not.Null, "Type not found: XRL.World.DescriptionBuilder");

        var field = descriptionBuilderType!.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(field, Is.Not.Null, $"DescriptionBuilder.{fieldName} field not found.");
            Assert.That(field?.FieldType, Is.EqualTo(typeof(string)));
        });
    }

    [TestCase("attributePointsText")]
    [TestCase("mutationPointsText")]
    public void Issue29Probe_CharacterStatusScreenContainsUITextSkinField(string fieldName)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var type = assembly.GetType("Qud.UI.CharacterStatusScreen", throwOnError: false);
        Assert.That(type, Is.Not.Null, "Type not found: Qud.UI.CharacterStatusScreen");

        var field = type!.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(field, Is.Not.Null, $"CharacterStatusScreen.{fieldName} field not found.");
            Assert.That(field?.FieldType.FullName, Is.EqualTo("XRL.UI.UITextSkin"));
        });
    }

    [Test]
    public void Issue29Probe_SkillsAndPowersStatusScreenContainsSpTextField()
    {
        var assembly = EnsureGameAssemblyLoaded();
        var type = FindTypeBySimpleName(assembly, "SkillsAndPowersStatusScreen");
        Assert.That(type, Is.Not.Null, "Type not found by simple name: SkillsAndPowersStatusScreen");

        var field = type!.GetField("spText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(field, Is.Not.Null, "SkillsAndPowersStatusScreen.spText field not found.");
            Assert.That(field?.FieldType.FullName, Is.EqualTo("XRL.UI.UITextSkin"));
        });
    }

    [TestCase("rawData")]
    [TestCase("sortedData")]
    public void Issue29Probe_FactionsStatusScreenContainsLineCollectionField(string fieldName)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var type = assembly.GetType("Qud.UI.FactionsStatusScreen", throwOnError: false);
        Assert.That(type, Is.Not.Null, "Type not found: Qud.UI.FactionsStatusScreen");

        var field = type!.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(field, Is.Not.Null, $"FactionsStatusScreen.{fieldName} field not found.");
            Assert.That(field?.FieldType.FullName, Does.StartWith("System.Collections.Generic.List`1[[Qud.UI.FactionsLineData"));
        });
    }

    [Test]
    public void Issue29Probe_GetDisplayNameEventContainsDescriptionBuilderField()
    {
        var assembly = EnsureGameAssemblyLoaded();
        var eventType = assembly.GetType("XRL.World.GetDisplayNameEvent", throwOnError: false);
        Assert.That(eventType, Is.Not.Null, "Type not found: XRL.World.GetDisplayNameEvent");

        var field = eventType!.GetField("DB", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(field, Is.Not.Null, "GetDisplayNameEvent.DB field not found.");
            Assert.That(field?.FieldType.Name, Is.EqualTo("DescriptionBuilder"));
        });
    }

    [Test]
    public void CharGenLocalizationPatch_TargetMethods_ResolveCurrentCharGenSurface()
    {
        var targetMethodsMethod = typeof(CharGenLocalizationPatch).GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, "TargetMethods not found for CharGenLocalizationPatch");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, "TargetMethods returned null for CharGenLocalizationPatch");

        var foundKnownCharGenType = false;
        var foundAnyMethod = false;
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            foundAnyMethod = true;
            var declaringTypeName = methodInfo.DeclaringType?.Name;
            if (!string.IsNullOrEmpty(declaringTypeName)
                && (declaringTypeName.Contains("Embark", StringComparison.Ordinal)
                    || declaringTypeName.Contains("Genotype", StringComparison.Ordinal)
                    || declaringTypeName.Contains("Mutation", StringComparison.Ordinal)
                    || declaringTypeName.Contains("Cybernetics", StringComparison.Ordinal)
                    || declaringTypeName.Contains("CharacterCreation", StringComparison.Ordinal)
                    || declaringTypeName.Contains("Calling", StringComparison.Ordinal)))
            {
                foundKnownCharGenType = true;
                break;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(foundAnyMethod, Is.True, "CharGenLocalizationPatch resolved no target methods.");
            Assert.That(foundKnownCharGenType, Is.True, "CharGenLocalizationPatch did not resolve any char-gen-related declaring types.");
        });
    }

    [Test]
    public void CharGenLocalizationPatch_TargetMethods_IncludeValidationMessages()
    {
        var targetMethodsMethod = typeof(CharGenLocalizationPatch).GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, "TargetMethods not found for CharGenLocalizationPatch");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, "TargetMethods returned null for CharGenLocalizationPatch");

        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            signatures.Add((methodInfo.DeclaringType?.FullName ?? string.Empty) + "|" + methodInfo.Name);
        }

        Assert.Multiple(() =>
        {
            Assert.That(signatures, Does.Contain("XRL.CharacterBuilds.Qud.QudAttributesModule|DataWarnings"));
            Assert.That(signatures, Does.Contain("XRL.CharacterBuilds.Qud.QudAttributesModule|DataErrors"));
            Assert.That(signatures, Does.Contain("XRL.CharacterBuilds.Qud.QudCyberneticsModule|DataErrors"));
        });
    }
#endif

    private static MethodBase? InvokeTargetMethod(Type patchType)
    {
        var targetMethod = patchType.GetMethod("TargetMethod", BindingFlags.NonPublic | BindingFlags.Static);
        return targetMethod?.Invoke(null, null) as MethodBase;
    }

    private static HashSet<string> ResolveTargetMethodNames(Type patchType)
    {
        var targetMethodsMethod = patchType.GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, $"TargetMethods not found for {patchType.FullName}");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, $"TargetMethods returned null for {patchType.FullName}");

        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in result!)
        {
            if (item is MethodInfo methodInfo)
            {
                signatures.Add((methodInfo.DeclaringType?.FullName ?? string.Empty) + "|" + methodInfo.Name);
            }
        }

        return signatures;
    }

    private static MethodBase? FindMethodByNameAndParameterCount(
        Type declaringType,
        string methodName,
        int parameterCount,
        bool requireNonObsolete = false)
    {
        var methods = declaringType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        for (var index = 0; index < methods.Length; index++)
        {
            var method = methods[index];
            if (method.Name != methodName || method.GetParameters().Length != parameterCount)
            {
                continue;
            }

            if (!requireNonObsolete || !method.IsDefined(typeof(ObsoleteAttribute), inherit: false))
            {
                return method;
            }
        }

        return null;
    }

    private static Type? FindTypeBySimpleName(Assembly assembly, string simpleTypeName)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = Array.FindAll(ex.Types, static type => type is not null)!;
        }

        for (var index = 0; index < types.Length; index++)
        {
            if (types[index].Name == simpleTypeName)
            {
                return types[index];
            }
        }

        return null;
    }

    private static void AssertSimpleTypeNameResolves(string simpleTypeName)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var resolvedType = FindTypeBySimpleName(assembly, simpleTypeName);

        Assert.That(resolvedType, Is.Not.Null, $"Type not found by simple name: {simpleTypeName}");
    }

    // Regex: strip assembly-qualified parts from generic type args
    // "List`1[[System.String, System.Private.CoreLib, Version=...]]" → "List`1[[System.String]]"
    private static string NormalizeTypeName(string? typeName)
    {
        if (typeName is null)
        {
            return string.Empty;
        }

        return Regex.Replace(typeName, @",\s*[^\[\],]+,\s*Version=[^\]]+", string.Empty);
    }

    private static string ResolveManagedDirectory()
    {
        var envDir = Environment.GetEnvironmentVariable("COQ_MANAGED_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            return envDir;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultDir = Path.Combine(
            home,
            "Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed");

        if (Directory.Exists(defaultDir))
        {
            return defaultDir;
        }

        Assert.Ignore("Game managed directory not found. Set COQ_MANAGED_DIR to run game-DLL-backed tests.");
        return string.Empty;
    }

#if HAS_GAME_DLL
    private static Assembly EnsureGameAssemblyLoaded()
    {
        var loadedAssembly = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            static assembly => string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        var managedDir = ResolveManagedDirectory();
        var assemblyPath = Path.Combine(managedDir, "Assembly-CSharp.dll");

        Assert.That(File.Exists(assemblyPath), Is.True, $"Assembly-CSharp.dll not found at {assemblyPath}");
        loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        Assert.That(loadedAssembly.GetType("XRL.World.GameObject", throwOnError: false), Is.Not.Null);
        return loadedAssembly;
    }
#endif

    private static Assembly EnsureManagedAssemblyLoaded(string assemblyName)
    {
        var loadedAssembly = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        var managedDir = ResolveManagedDirectory();
        var assemblyPath = Path.Combine(managedDir, assemblyName + ".dll");

        Assert.That(File.Exists(assemblyPath), Is.True, $"{assemblyName}.dll not found at {assemblyPath}");
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    }
}
#endif
