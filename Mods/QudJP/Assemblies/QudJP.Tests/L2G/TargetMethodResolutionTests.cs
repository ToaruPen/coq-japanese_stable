#if HAS_GAME_DLL || HAS_TMP
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using HarmonyLib;
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
        _ = EnsureManagedAssemblyLoaded("UniTask");
        _ = EnsureManagedAssemblyLoaded("ZString");
        _ = EnsureManagedAssemblyLoaded("Unity.InputSystem");
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
    [TestCase(typeof(CyberneticsBehaviorDescriptionTranslationPatch), "GetFor", "XRL.World.GetCyberneticsBehaviorDescriptionEvent", "System.String", new[] { "XRL.World.GameObject", "System.String" })]
    [TestCase(typeof(GetDisplayNameProcessPatch), "ProcessFor", "XRL.World.GetDisplayNameEvent", "System.String", new[] { "XRL.World.GameObject", "System.Boolean" })]
    [TestCase(typeof(LookTooltipContentPatch), "GenerateTooltipContent", "XRL.UI.Look", "System.String", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(LookTooltipInformationWrapPatch), "GenerateTooltipInformation", "XRL.UI.Look", "XRL.UI.Look+TooltipInformation", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(DescriptionLongDescriptionPatch), "GetLongDescription", "XRL.World.Parts.Description", "System.Void", new[] { "System.Text.StringBuilder" })]
    [TestCase(typeof(UITextSkinTranslationPatch), "SetText", "XRL.UI.UITextSkin", "System.Boolean", new[] { "System.String" })]
    [TestCase(typeof(CharacterStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.CharacterStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(SaveManagementRowTranslationPatch), "setData", "SaveManagementRow", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(SavesApiReadSaveJsonTranslationPatch), "ReadSaveJson", "Qud.API.SavesAPI", "Qud.API.SaveGameInfo", new[] { "System.String", "System.String" })]
    [TestCase(typeof(TinkeringStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.TinkeringStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(BookLineTranslationPatch), "setData", "Qud.UI.BookLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(BookAutoformatCjkWrapPatch), "AutoformatPages", "XRL.UI.BookUI", "System.Collections.Generic.List`1[[XRL.UI.BookPage]]", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "System.Int32",
        "System.Int32",
        "System.Int32",
        "System.Int32",
    })]
    [TestCase(typeof(CharacterAttributeLineTranslationPatch), "setData", "Qud.UI.CharacterAttributeLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(CharacterEffectLineTranslationPatch), "setData", "Qud.UI.CharacterEffectLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(PickGameObjectScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.PickGameObjectScreen", "System.Void", new[] { "System.Boolean" })]
    [TestCase(typeof(PickItemShowPickerTitleTranslationPatch), "ShowPicker", "XRL.UI.PickItem", "XRL.World.GameObject", new[]
    {
        "System.Collections.Generic.IList`1[[XRL.World.GameObject]]",
        "System.Boolean&",
        "System.String",
        "XRL.UI.PickItem+PickItemDialogStyle",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.Cell",
        "System.String",
        "System.Boolean",
        "System.Func`1[[System.Collections.Generic.List`1[[XRL.World.GameObject]]]]",
        "System.Boolean",
        "System.Boolean",
        "System.Boolean",
    })]
    [TestCase(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.InventoryAndEquipmentStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(InventoryAndEquipmentStatusScreenShowRepairPatch), "ShowScreen", "Qud.UI.InventoryAndEquipmentStatusScreen", "XRL.UI.Framework.NavigationContext", new[] { "XRL.World.GameObject", "Qud.UI.StatusScreensScreen" })]
    [TestCase(typeof(InventoryLineTranslationPatch), "setData", "Qud.UI.InventoryLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(InventoryLineRenderProbePatch), "setData", "Qud.UI.InventoryLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(InventoryLineActiveTextRefreshPatch), "LateUpdate", "Qud.UI.InventoryLine", "System.Void", new string[0])]
    [TestCase(typeof(EquipmentLineTranslationPatch), "setData", "Qud.UI.EquipmentLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(JournalLineTranslationPatch), "setData", "Qud.UI.JournalLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TinkeringLineTranslationPatch), "setData", "Qud.UI.TinkeringLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TinkeringBitsLineTranslationPatch), "setData", "Qud.UI.TinkeringBitsLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
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
    [TestCase(typeof(KeybindBoxTranslationPatch), "Update", "Qud.UI.KeybindBox", "System.Void", new string[0])]
    [TestCase(typeof(GameTextDeathReasonTranslationPatch), "RoughConvertSecondPersonToThirdPerson", "XRL.GameText", "System.String", new[] { "System.String", "XRL.World.GameObject" })]
    [TestCase(typeof(VehicleFollowerPopupTranslationPatch), "HandleEvent", "XRL.World.Parts.Vehicle", "System.Boolean", new[] { "XRL.World.InventoryActionEvent" })]
    [TestCase(typeof(XrlManualTranslationPatch), "RenderIndex", "XRL.Help.XRLManual", "System.Void", new[] { "System.Int32" })]
    [TestCase(typeof(CharacterStatusScreenMutationDetailsPatch), "HandleHighlightMutation", "Qud.UI.CharacterStatusScreen", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(StatisticGetHelpTextPatch), "GetHelpText", "XRL.World.Statistic", "System.String", new string[0])]
    [TestCase(typeof(EnergyStorageChargeStatusTranslationPatch), "GetChargeStatus", "XRL.World.Capabilities.EnergyStorage", "System.String", new[]
    {
        "System.Int32",
        "System.Int32",
        "System.String",
    })]
    [TestCase(typeof(ChargenAttributeDescriptionTranslationPatch), "handleUIEvent", "XRL.CharacterBuilds.Qud.QudGenotypeModule", "System.Object", new[] { "System.String", "System.Object" })]
    [TestCase(typeof(CharacterStatusScreenAttributeHighlightPatch), "HandleHighlightAttribute", "Qud.UI.CharacterStatusScreen", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(SkillsAndPowersStatusScreenDetailsPatch), "UpdateDetailsFromNode", "Qud.UI.SkillsAndPowersStatusScreen", "System.Void", new[] { "XRL.UI.SPNode" })]
    [TestCase(typeof(StatisticStatShiftDisplayNameTranslationPatch), "AddShift", "XRL.World.Statistic", "System.Guid", new[] { "System.Int32", "System.String", "System.Boolean" })]
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
    [TestCase(typeof(SelectableTextMenuItemTranslationPatch), "SelectChanged", "Qud.UI.SelectableTextMenuItem", "System.Void", new[] { "System.Boolean" })]
    [TestCase(typeof(MissileWeaponAreaTranslationPatch), "AfterRender", "Qud.UI.MissileWeaponArea", "System.Void", new[] { "XRL.Core.XRLCore", "ConsoleLib.Console.ScreenBuffer" })]
    [TestCase(typeof(CherubimSpawnerReplaceDescriptionPatch), "ReplaceDescription", "XRL.World.Parts.CherubimSpawner", "System.Void", new[] { "XRL.World.GameObject", "System.String", "System.String" })]
    [TestCase(typeof(CherubimSpawnerHandleEventTranslationPatch), "HandleEvent", "XRL.World.Parts.CherubimSpawner", "System.Boolean", new[] { "XRL.World.BeforeObjectCreatedEvent" })]
    [TestCase(typeof(CherubimSpawnerBestowElementTranslationPatch), "BestowElement", "XRL.World.Parts.CherubimSpawner", "System.Void", new[] { "XRL.World.GameObject", "System.String", "System.Boolean" })]
    [TestCase(typeof(HexacherubimSpawnerHandleEventTranslationPatch), "HandleEvent", "XRL.World.Parts.HexacherubimSpawner", "System.Boolean", new[] { "XRL.World.BeforeObjectCreatedEvent" })]
    [TestCase(typeof(CharacterStatusScreenHighlightEffectPatch), "HandleHighlightEffect", "Qud.UI.CharacterStatusScreen", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(GameObjectShowActiveEffectsPatch), "ShowActiveEffects", "XRL.World.GameObject", "System.Void", new string[0])]
    [TestCase(typeof(DescriptionShortDescriptionPatch), "GetShortDescription", "XRL.World.Parts.Description", "System.String", new[] { "System.Boolean", "System.Boolean", "System.String" })]
    [TestCase(typeof(FactionsLineDataTranslationPatch), "set", "Qud.UI.FactionsLineData", "Qud.UI.FactionsLineData", new[] { "System.String", "System.String", "ConsoleLib.Console.IRenderable", "System.Boolean" })]
    [TestCase(typeof(FactionsLineTranslationPatch), "setData", "Qud.UI.FactionsLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(QudMutationsModuleWindowTranslationPatch), "UpdateControls", "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow", "System.Void", new string[0])]
    [TestCase(typeof(QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch), "HandleMenuOption", "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow", "System.Void", new[] { "XRL.UI.Framework.MenuOption" })]
    [TestCase(typeof(SummaryBlockControlTranslationPatch), "setData", "XRL.UI.Framework.SummaryBlockControl", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TradeLineTranslationPatch), "setData", "Qud.UI.TradeLine", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(TradeUiLegacyScreenTranslationPatch), "ShowTradeScreen", "XRL.UI.TradeUI", "System.Void", new[] { "XRL.World.GameObject", "System.Single", "XRL.UI.TradeUI+TradeScreenMode" })]
    [TestCase(typeof(SkillsAndPowersStatusScreenTranslationPatch), "UpdateViewFromData", "Qud.UI.SkillsAndPowersStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(ActivatedAbilityNotUsableDescriptionTranslationPatch), "get_NotUsableDescription", "XRL.World.Parts.ActivatedAbilityEntry", "System.String", new string[0])]
    [TestCase(typeof(MessageQueueTranslationPatch), "AddPlayerMessage", "XRL.Messages.MessageQueue", "System.Void", new[] { "System.String", "System.String", "System.Boolean" })]
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
    [TestCase(typeof(TutorialManagerCellPopupTranslationPatch), "ShowCellPopup", "TutorialManager", "System.Threading.Tasks.Task", new[]
    {
        "Genkit.Location2D",
        "System.String",
        "System.String",
        "System.Int32",
        "System.Int32",
        "System.Action",
    })]
    [TestCase(typeof(TutorialManagerHighlightTranslationPatch), "HighlightByCID", "TutorialManager", "System.Boolean", new[]
    {
        "System.String",
        "System.String",
        "System.String",
        "System.Int32",
        "System.Int32",
        "System.Single",
        "System.String",
    })]
    [TestCase(typeof(TutorialManagerCellHighlightTranslationPatch), "HighlightCell", "TutorialManager", "System.Void", new[]
    {
        "System.Int32",
        "System.Int32",
        "System.String",
        "System.String",
        "System.Single",
        "System.Single",
        "System.Single",
    })]
    [TestCase(typeof(TutorialManagerDirectHighlightTranslationPatch), "Highlight", "TutorialManager", "System.Void", new[]
    {
        "UnityEngine.RectTransform",
        "System.String",
        "System.String",
        "System.Single",
        "System.Single",
        "System.Single",
        "System.String",
    })]
    [TestCase(typeof(XrlCoreLostSightTranslationPatch), "RenderBaseToBuffer", "XRL.Core.XRLCore", "System.Void", new[] { "ConsoleLib.Console.ScreenBuffer" })]
    [TestCase(typeof(XrlCoreHotloadConfigurationTranslationPatch), "HotloadConfiguration", "XRL.Core.XRLCore", "System.Void", new[] { "System.Boolean" })]
    [TestCase(typeof(BrainThinkTranslationPatch), "Think", "XRL.World.Parts.Brain", "System.Void", new[] { "System.String" })]
    [TestCase(typeof(BrainWriteFeelingSamplesPopupTranslationPatch), "WriteFeelingSamples", "XRL.World.Parts.Brain", "System.Void", new[] { "System.Boolean" })]
    [TestCase(typeof(CyberneticsWishImplantPopupTranslationPatch), "WishImplant", "XRL.World.Capabilities.Cybernetics", "System.Void", new[] { "System.String" })]
    [TestCase(typeof(ModQuantumReverbDisplayNameTranslationPatch), "CreateHologramOf", "XRL.World.Parts.ModQuantumReverb", "XRL.World.GameObject", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(RandomStatueDisplayNameTranslationPatch), "SetCreature", "XRL.World.Parts.RandomStatue", "System.Void", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(PetPhylacteryDisplayNameTranslationPatch), "HandleEvent", "XRL.World.Parts.PetPhylactery", "System.Boolean", new[] { "XRL.World.AfterObjectCreatedEvent" })]
    [TestCase(typeof(TombCultistTemplateDisplayNameTranslationPatch), "Apply", "XRL.World.Parts.TombCultistTemplate", "System.Void", new[] { "XRL.World.GameObject", "HistoryKit.HistoricEntitySnapshot" })]
    [TestCase(typeof(ZoneManagerSetActiveZoneTranslationPatch), "SetActiveZone", "XRL.World.ZoneManager", "XRL.World.Zone", new[] { "XRL.World.Zone" })]
    [TestCase(typeof(JournalEntryDisplayTextPatch), "GetDisplayText", "Qud.API.IBaseJournalEntry", "System.String", new string[0])]
    [TestCase(typeof(JournalMapNoteDisplayTextPatch), "GetDisplayText", "Qud.API.JournalMapNote", "System.String", new string[0])]
    [TestCase(typeof(SultanateYearNameTranslationPatch), "GenerateSultanateYearName", "XRL.Annals.QudHistoryHelpers", "System.String", new string[0])]
    [TestCase(typeof(ImportedFoodOrDrinkFactionNameTranslationPatch), "generateFactionName", "XRL.Annals.ImportedFoodorDrink", "System.String", new[] { "System.String" })]
    [TestCase(typeof(QudHistoryFactoryNameRuinsSiteTranslationPatch), "NameRuinsSite", "XRL.Annals.QudHistoryFactory", "System.String", new[] { "HistoryKit.History", "System.Boolean&", "System.String&" })]
    [TestCase(typeof(QudHistoryFactoryGenerateCultNameTranslationPatch), "GenerateCultName", "XRL.Annals.QudHistoryFactory", "System.Void", new[] { "HistoryKit.HistoricEntity", "HistoryKit.History" })]
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
    [TestCase(typeof(PetEitherOrExplodeTranslationPatch), "explode", "XRL.World.Parts.PetEitherOr", "System.Void", new string[0])]
    [TestCase(typeof(ZoneWindChangeTranslationPatch), "WindChange", "XRL.World.Zone", "System.Void", new[] { "System.Int64" })]
    [TestCase(typeof(CudgelConkPopupTranslationPatch), "PerformConk", "XRL.World.Parts.Skill.Cudgel_Conk", "System.Boolean", new string[0])]
    [TestCase(typeof(CrippleApplyTranslationPatch), "Apply", "XRL.World.Effects.Cripple", "System.Boolean", new[] { "XRL.World.GameObject" })]
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
    [TestCase(typeof(PhysicsProcessTakeDamageTranslationPatch), "ProcessTakeDamage", "XRL.World.Parts.Physics", "System.Boolean", new[] { "XRL.World.Event" })]
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
    [TestCase(typeof(ExperienceAwardXpTranslationPatch), "HandleEvent", "XRL.World.Parts.Experience", "System.Boolean", new[] { "XRL.World.AwardXPEvent" })]
    [TestCase(typeof(ZoneManagerTryThawZoneTranslationPatch), "TryThawZone", "XRL.World.ZoneManager", "System.Boolean", new[] { "System.String", "XRL.World.Zone&" })]
    [TestCase(typeof(ZoneManagerTickTranslationPatch), "Tick", "XRL.World.ZoneManager", "System.Void", new[] { "System.Boolean" })]
    [TestCase(typeof(ZoneManagerSetActiveZoneMapNotesTranslationPatch), "SetActiveZone", "XRL.World.ZoneManager", "XRL.World.Zone", new[] { "XRL.World.Zone" })]
    [TestCase(typeof(ZoneManagerGenerateZoneTranslationPatch), "GenerateZone", "XRL.World.ZoneManager", "System.Void", new[] { "System.String" })]
    [TestCase(typeof(BedTranslationPatch), "AttemptSleep", "XRL.World.Parts.Bed", "System.Void", new[] { "XRL.World.GameObject", "System.Boolean&", "System.Boolean&", "System.Boolean&" })]
    [TestCase(typeof(ChairTranslationPatch), "SitDown", "XRL.World.Parts.Chair", "System.Boolean", new[] { "XRL.World.GameObject", "XRL.World.IEvent" })]
    [TestCase(typeof(StasisTranslationPatch), "HandleEvent", "XRL.World.Effects.Stasis", "System.Boolean", new[] { "XRL.World.BeforeApplyDamageEvent" })]
    [TestCase(typeof(SultanRegionRevealDescriptionTranslationPatch), "FireEvent", "XRL.World.Parts.SultanRegion", "System.Boolean", new[] { "XRL.World.Event" })]
    [TestCase(typeof(BlazeTonicRemoveTranslationPatch), "Remove", "XRL.World.Effects.Blaze_Tonic", "System.Void", new[] { "XRL.World.GameObject" })]
    [TestCase(typeof(LatchedOntoExpiredTranslationPatch), "Expired", "XRL.World.Effects.LatchedOnto", "System.Void", new string[0])]
    [TestCase(typeof(TinkeringBuildPopupTranslationPatch), "PerformUITinkerBuild", "XRL.UI.TinkeringScreen", "System.Boolean", new[] { "XRL.World.GameObject", "XRL.World.Tinkering.TinkerData", "XRL.World.IEvent" })]
    [TestCase(typeof(TinkeringModPopupTranslationPatch), "PerformUITinkerMod", "XRL.UI.TinkeringScreen", "System.Boolean", new[] { "XRL.World.GameObject", "XRL.World.GameObject", "XRL.World.Tinkering.TinkerData", "XRL.World.Tinkering.BitCost", "XRL.World.IEvent", "System.Boolean&", "System.Collections.Generic.List`1[[XRL.World.GameObject]]" })]
    [TestCase(typeof(PickItemTakeAllPopupTranslationPatch), "TakeAll", "XRL.UI.PickItem", "System.Boolean", new[] { "XRL.World.GameObject", "XRL.World.GameObject", "XRL.World.Cell", "System.Collections.Generic.IList`1[[XRL.World.GameObject]]", "System.Boolean&" })]
    [TestCase(typeof(AbsorbablePsychePopupTranslationPatch), "HandleEvent", "XRL.World.Parts.AbsorbablePsyche", "System.Boolean", new[] { "XRL.World.BeforeDeathRemovalEvent" })]
    [TestCase(typeof(DataDiskLearnPopupTranslationPatch), "HandleEvent", "XRL.World.Parts.DataDisk", "System.Boolean", new[] { "XRL.World.InventoryActionEvent" })]
    [TestCase(typeof(PhysicsInventoryActionPopupTranslationPatch), "HandleEvent", "XRL.World.Parts.Physics", "System.Boolean", new[] { "XRL.World.InventoryActionEvent" })]
    [TestCase(typeof(BroadcastPowerOcclusionReasonTranslationPatch), "HandleEvent", "XRL.World.Parts.BroadcastPowerReceiver", "System.Boolean", new[] { "XRL.World.GetShortDescriptionEvent" })]
    [TestCase(typeof(CampfireCookFromIngredientsTranslationPatch), "CookFromIngredients", "XRL.World.Parts.Campfire", "System.Boolean", new[] { "System.Boolean" })]
    [TestCase(typeof(CampfireCookFromRecipeTranslationPatch), "CookFromRecipe", "XRL.World.Parts.Campfire", "System.Boolean", new string[0])]
    [TestCase(typeof(CampfireCookPresetMealTranslationPatch), "CookPresetMeal", "XRL.World.Parts.Campfire", "System.Boolean", new[] { "System.Int32" })]
    [TestCase(typeof(CampfireRollIngredientsTranslationPatch), "RollIngredients", "XRL.World.Parts.Campfire", "System.String[]", new[] { "System.Int32", "System.Collections.Generic.IReadOnlyList`1[[XRL.World.GameObject]]", "System.Random" })]
    [TestCase(typeof(CampfireDescribeMealTranslationPatch), "DescribeMeal", "XRL.World.Parts.Campfire", "System.String", new[] { "System.Collections.Generic.IReadOnlyList`1[[XRL.World.GameObject]]" })]
    [TestCase(typeof(CookbookDisplayNameTranslationPatch), "GenerateCookbook", "XRL.World.Parts.Cookbook", "System.Void", new string[0])]
    [TestCase(typeof(CookingRecipeGenerateRecipeTileTranslationScopePatch), "GenerateRecipeTile", "XRL.World.Skills.Cooking.CookingRecipe", "ConsoleLib.Console.Renderable", new[] { "XRL.World.Skills.Cooking.CookingRecipe" })]
    [TestCase(typeof(RelicGeneratorGeneratedNameTranslationPatch), "GenerateRelicName", "XRL.World.RelicGenerator", "System.String", new[] { "System.String", "HistoryKit.HistoricEntitySnapshot", "System.String", "System.String&" })]
    [TestCase(typeof(RelicDescriptionAddendumTranslationPatch), "GenerateRelic", "XRL.World.RelicGenerator", "XRL.World.GameObject", new[] { "System.String", "System.Int32", "HistoryKit.HistoricEntitySnapshot", "System.Collections.Generic.List`1[[System.String]]", "System.Collections.Generic.Dictionary`2[[System.String],[System.Collections.Generic.List`1[[System.String]]]]", "System.String", "System.String", "System.String" })]
    [TestCase(typeof(PseudoRelicGeneratedNameTranslationPatch), "Send", "XRL.World.AfterPseudoRelicGeneratedEvent", "System.Void", new[] { "XRL.World.GameObject", "System.String", "System.String", "System.String", "System.Int32" })]
    [TestCase(typeof(ItemNamingGeneratedNameTranslationPatch), "GenerateRelicStyleName", "XRL.World.Capabilities.ItemNaming", "System.String", new[] { "XRL.World.GameObject", "XRL.World.GameObject", "XRL.World.GameObject", "XRL.World.GameObject", "System.String", "System.String&", "System.String&" })]
    [TestCase(typeof(DynamicQuestConversationTranslationPatch), "appendQuestCompletionSequence", "XRL.World.DynamicQuestConversationHelper", "System.Void", new[] { "XRL.World.Conversations.ConversationXMLBlueprint", "XRL.World.Quest", "XRL.World.Conversations.ConversationXMLBlueprint", "System.String", "System.String", "System.Action`1[[XRL.World.Conversations.ConversationXMLBlueprint]]", "System.Action`1[[XRL.World.Conversations.ConversationXMLBlueprint]]", "System.Action`1[[XRL.World.Conversations.ConversationXMLBlueprint]]", "System.Action`1[[XRL.World.Conversations.ConversationXMLBlueprint]]", "System.Action`1[[XRL.World.Conversations.ConversationXMLBlueprint]]" })]
    [TestCase(typeof(VillageDynamicQuestItemNameMutationTranslationPatch), "getQuestItemNameMutation", "XRL.World.VillageDynamicQuestContext", "System.String", new[] { "System.String" })]
    [TestCase(typeof(VillageBaseCreateVillageFactionDisplayNameTranslationPatch), "CreateVillageFaction", "XRL.World.ZoneBuilders.VillageBase", "XRL.World.Faction", new[] { "HistoryKit.HistoricEntitySnapshot" })]
    [TestCase(typeof(TemporalFugueCreateFugueCopyDisplayNameTranslationPatch), "CreateFugueCopyOf", "XRL.World.Parts.Mutation.TemporalFugue", "XRL.World.GameObject", new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.Cell",
        "XRL.World.GameObject",
        "System.Boolean",
        "System.Int32",
        "System.Int32",
        "System.String",
        "System.String",
        "System.String",
        "System.String",
        "System.String",
        "XRL.World.IPart",
    })]
    [TestCase(typeof(PlayerMuralDisplayNameTranslationPatch), "updatePlayerMural", "XRL.World.Parts.PlayerMuralController", "System.Void", new[] { "System.Collections.Generic.List`1[[Genkit.Location2D]]", "Qud.API.JournalAccomplishment", "System.Int32" })]
    [TestCase(typeof(VillageDynamicQuestRewardDisplayNameTranslationPatch), "getQuestReward", "XRL.World.VillageDynamicQuestContext", "XRL.World.DynamicQuestReward", new string[0])]
    [TestCase(typeof(GameObjectDestroyTranslationPatch), "Destroy", "XRL.World.GameObject", "System.Boolean", new[] { "System.String", "System.Boolean", "System.Boolean", "System.String" })]
    [TestCase(typeof(DynamicQuestSignpostConversationTranslationPatch), "HandleEvent", "XRL.World.Parts.DynamicQuestSignpostConversation", "System.Boolean", new[] { "XRL.World.BeforeConversationEvent" })]
    [TestCase(typeof(MerchantAdvertisementTextTranslationPatch), "GenerateMerchantLocation", "XRL.World.Parts.MerchantRevealer", "System.Void", new string[0])]
    [TestCase(typeof(TempleDedicationPlaqueInscriptionTranslationPatch), "GenerateInscription", "XRL.World.Parts.TempleDedicationPlaque", "System.String", new string[0])]
    [TestCase(typeof(VillageTerrainRevealDescriptionTranslationPatch), "FireEvent", "XRL.World.Parts.VillageTerrain", "System.Boolean", new[] { "XRL.World.Event" })]
    [TestCase(typeof(EaterCryptPlaqueTextTranslationPatch), "GeneratePlaque", "XRL.World.Parts.EaterCryptPlaque", "System.Void", new string[0])]
    [TestCase(typeof(StairsDownTranslationPatch), "HandleEvent", "XRL.World.Parts.StairsDown", "System.Boolean", new[] { "XRL.World.InventoryActionEvent" })]
    [TestCase(typeof(StairsUpTranslationPatch), "HandleEvent", "XRL.World.Parts.StairsUp", "System.Boolean", new[] { "XRL.World.InventoryActionEvent" })]
    [TestCase(typeof(GameSummaryScreenMenuBarsTranslationPatch), "UpdateMenuBars", "Qud.UI.GameSummaryScreen", "System.Void", new string[0])]
    [TestCase(typeof(GameSummaryScreenShowTranslationPatch), "_ShowGameSummary", "Qud.UI.GameSummaryScreen", "System.Threading.Tasks.Task`1[[System.Boolean]]", new[] { "System.String", "System.String", "System.String", "System.Boolean" })]
    [TestCase(typeof(InventoryActionMenuShowTimingPatch), "ShowInventoryActionMenu", "Qud.API.EquipmentAPI", "XRL.World.InventoryAction", new[]
    {
        "System.Collections.Generic.Dictionary`2[[System.String],[XRL.World.InventoryAction]]",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "System.Boolean",
        "System.Boolean",
        "System.String",
        "System.Collections.Generic.IComparer`1[[XRL.World.InventoryAction]]",
        "System.Boolean",
    })]
    [TestCase(typeof(InventoryActionMenuPopupHideTimingPatch), "Hide", "Qud.UI.PopupMessage", "System.Void", new string[0])]
    [TestCase(typeof(InventoryActionMenuPopupUpdateTimingPatch), "Update", "Qud.UI.PopupMessage", "System.Void", new string[0])]
    [TestCase(typeof(InventoryActionMenuUpdateViewTimingPatch), "UpdateViewFromData", "Qud.UI.InventoryAndEquipmentStatusScreen", "System.Void", new string[0])]
    [TestCase(typeof(InventoryActionMenuCursorSoundPlayClickPatch), "PlayClick", "Qud.UI.QudBaseMenuController`2[[Qud.UI.QudMenuItem],[Qud.UI.SelectableTextMenuItem]]", "System.Void", new string[0])]
    [TestCase(typeof(InventoryActionMenuCursorSoundPopupContextPatch), "ShowPopup", "Qud.UI.PopupMessage", "System.Void", new[]
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
    [TestCase(typeof(InventoryActionMenuCursorSoundPopupHideContextPatch), "Hide", "Qud.UI.PopupMessage", "System.Void", new string[0])]
    [TestCase(typeof(MainMenuRowTranslationPatch), "setData", "MainMenuRow", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(LeftSideCategoryTranslationPatch), "setData", "Qud.UI.LeftSideCategory", "System.Void", new[] { "XRL.UI.Framework.FrameworkDataElement" })]
    [TestCase(typeof(PickTargetWindowUpdateTranslationPatch), "Update", "Qud.UI.PickTargetWindow", "System.Void", new string[0])]
    [TestCase(typeof(GivesRepShortDescriptionTranslationPatch), "HandleEvent", "XRL.World.Parts.GivesRep", "System.Boolean", new[] { "XRL.World.GetShortDescriptionEvent" })]
    [TestCase(typeof(CyberneticsBaseItemShortDescriptionTranslationPatch), "HandleEvent", "XRL.World.Parts.CyberneticsBaseItem", "System.Boolean", new[] { "XRL.World.GetShortDescriptionEvent" })]
    [TestCase(typeof(MutationsApiTranslationPatch), "BuyRandomMutation", "Qud.API.MutationsAPI", "System.Boolean", new[] { "XRL.World.GameObject", "System.Int32", "System.Boolean", "System.String" })]
    [TestCase(typeof(GritGateTerminalKnowledgePopupTranslationPatch), "Activate", "XRL.UI.GritGateTerminalScreenKnowledge", "System.Void", new string[0])]
    [TestCase(typeof(GritGateTerminalScreenMessageTranslationPatch), "Activate", "XRL.UI.GritGateTerminalScreenMessage", "System.Void", new string[0])]
    [TestCase(typeof(TextFiltersAngryTranslationPatch), "Angry", "XRL.Language.TextFilters", "System.String", new[] { "System.String" })]
    [TestCase(typeof(TextFiltersLallatedTranslationPatch), "Lallated", "XRL.Language.TextFilters", "System.String", new[] { "System.String", "System.String" })]
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
    [TestCase(typeof(LegacyTextMeshFontPatch), "set_text", "UnityEngine.TextMesh", "System.Void", new[] { "System.String" })]
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
            Assert.That(NormalizeTypeName(targetMethod.DeclaringType?.FullName), Is.EqualTo(expectedDeclaringType));

            var methodInfo = targetMethod as MethodInfo;
            Assert.That(methodInfo, Is.Not.Null, $"Expected MethodInfo for {patchType.FullName}");
            Assert.That(NormalizeTypeName(methodInfo!.ReturnType.FullName), Is.EqualTo(expectedReturnType));

            var parameterTypes = Array.ConvertAll(methodInfo.GetParameters(), static parameter => NormalizeTypeName(parameter.ParameterType.FullName));
            Assert.That(parameterTypes, Is.EqualTo(expectedParameterTypes));
        });
    }

#if HAS_GAME_DLL
    [Test]
    public void InventoryActionMenuCursorSoundPatch_ResolvesPlayableUiSoundMethod()
    {
        var accessor = typeof(InventoryActionMenuCursorSoundPatch).GetMethod(
            "GetPlayUiSoundMethod",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(accessor, Is.Not.Null, "GetPlayUiSoundMethod helper is missing.");

        var resolved = accessor!.Invoke(null, null) as MethodInfo;

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.Not.Null, "SoundManager.PlayUISound was not resolved.");
            Assert.That(resolved!.Name, Is.EqualTo("PlayUISound"));
            Assert.That(resolved.DeclaringType?.FullName, Is.EqualTo("SoundManager"));

            var parameterTypes = Array.ConvertAll(
                resolved.GetParameters(),
                static parameter => NormalizeTypeName(parameter.ParameterType.FullName));
            Assert.That(parameterTypes, Is.EqualTo(new[]
            {
                "System.String",
                "System.Single",
                "System.Boolean",
                "System.Boolean",
                "SoundRequest+SoundEffectType",
            }));
        });
    }

    [TestCase(typeof(VillageDynamicQuestRewardGameObjectTranslationPatch), "XRL.World.DynamicQuestRewardElement_GameObject", new[] { "XRL.World.GameObject" })]
    public void TargetConstructor_ResolvesExpectedSignature(
        Type patchType,
        string expectedDeclaringType,
        string[] expectedParameterTypes)
    {
        var targetMethod = InvokeTargetMethod(patchType);

        Assert.Multiple(() =>
        {
            Assert.That(targetMethod, Is.Not.Null, $"TargetMethod returned null for {patchType.FullName}");
            Assert.That(targetMethod!.Name, Is.EqualTo(".ctor"));
            Assert.That(targetMethod.DeclaringType?.FullName, Is.EqualTo(expectedDeclaringType));
            Assert.That(targetMethod, Is.InstanceOf<ConstructorInfo>(), $"Expected ConstructorInfo for {patchType.FullName}");

            var parameterTypes = Array.ConvertAll(
                targetMethod.GetParameters(),
                static parameter => NormalizeTypeName(parameter.ParameterType.FullName));
            Assert.That(parameterTypes, Is.EqualTo(expectedParameterTypes));
        });
    }
#endif

    [Test]
    public void CrippleApplyTargetMethod_ResolvesExpectedFullSignature()
    {
        var targetMethod = InvokeTargetMethod(typeof(CrippleApplyTranslationPatch));

        Assert.That(targetMethod, Is.Not.Null, "CrippleApplyTranslationPatch TargetMethod returned null.");
        Assert.That(
            FullMethodSignature(targetMethod!),
            Is.EqualTo("XRL.World.Effects.Cripple|Apply|System.Boolean|XRL.World.GameObject"));
    }

    [Test]
    public void CudgelConkPopupTargetMethod_ResolvesExpectedFullSignature()
    {
        var targetMethod = InvokeTargetMethod(typeof(CudgelConkPopupTranslationPatch));

        Assert.That(targetMethod, Is.Not.Null, "CudgelConkPopupTranslationPatch TargetMethod returned null.");
        Assert.That(
            FullMethodSignature(targetMethod!),
            Is.EqualTo("XRL.World.Parts.Skill.Cudgel_Conk|PerformConk|System.Boolean"));
    }

#if HAS_GAME_DLL
    [Test]
    public void ImportedFoodOrDrinkFactionNameTargetMethod_ResolvesExactRuntimeSignature()
    {
        var targetType = AccessTools.TypeByName("XRL.Annals.ImportedFoodorDrink");
        Assert.That(targetType, Is.Not.Null, "Type not found: XRL.Annals.ImportedFoodorDrink");

        var runtimeMethod = AccessTools.Method(targetType, "generateFactionName", new[] { typeof(string) });
        var targetMethod = InvokeTargetMethod(typeof(ImportedFoodOrDrinkFactionNameTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(runtimeMethod, Is.Not.Null, "generateFactionName(string) runtime method not found.");
            Assert.That(runtimeMethod!.ReturnType, Is.EqualTo(typeof(string)));
            Assert.That(targetMethod, Is.Not.Null, "ImportedFoodOrDrinkFactionNameTranslationPatch TargetMethod returned null.");
            Assert.That(targetMethod, Is.EqualTo(runtimeMethod));
        });
    }
#endif

#if HAS_GAME_DLL && HAS_TMP
    [Test]
    public void SelectableTextMenuItemPopupIdParentRouteContractsResolve()
    {
        var gameAssembly = EnsureGameAssemblyLoaded();
        var popupMessageType = gameAssembly.GetType("Qud.UI.PopupMessage", throwOnError: false);
        var componentType = Type.GetType("UnityEngine.Component, UnityEngine.CoreModule", throwOnError: false);

        Assert.That(popupMessageType, Is.Not.Null, "Type not found: Qud.UI.PopupMessage");
        Assert.That(componentType, Is.Not.Null, "Type not found: UnityEngine.Component");

        var popupIdField = popupMessageType!.GetField(
            "PopupID",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var includeInactiveGetComponentInParent = componentType!.GetMethod(
            "GetComponentInParent",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(Type), typeof(bool) },
            modifiers: null);
        var legacyGetComponentInParent = componentType.GetMethod(
            "GetComponentInParent",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(Type) },
            modifiers: null);

        Assert.Multiple(() =>
        {
            Assert.That(popupIdField, Is.Not.Null, "PopupMessage.PopupID field not found.");
            Assert.That(popupIdField?.FieldType, Is.EqualTo(typeof(string)));
            Assert.That(
                includeInactiveGetComponentInParent ?? legacyGetComponentInParent,
                Is.Not.Null,
                "UnityEngine.Component.GetComponentInParent(Type[, bool]) route not found.");
        });
    }
#endif

#if HAS_GAME_DLL
    [TestCase(typeof(SinkPrereqSetDataTranslationPatch), new[]
    {
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
        "System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
        "System.String|System.Boolean|System.Boolean|System.Boolean",
        "System.String|System.Threading.CancellationToken",
        "System.String|System.String|System.Boolean|XRL.UI.DialogResult",
        "System.String",
        "System.String|System.String|System.Boolean|XRL.UI.DialogResult",
        "System.String",
    })]
    [TestCase(typeof(GameObjectStatPopupTranslationPatch), new[]
    {
        "System.Int32|System.Boolean",
        "System.Int32|System.Boolean",
        "System.Int32|System.Boolean",
        "System.Int32|System.Boolean",
        "System.Int32|System.Boolean",
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
    [TestCase(typeof(XrlCoreStartMainMenuTranslationPatch), new[]
    {
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
    [TestCase(typeof(CombatTextSurfaceTranslationPatch), new[]
    {
        "XRL.World.GetDefenderHitDiceEvent",
        "XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Anatomy.BodyPart|System.String|System.Int32|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean|System.Boolean",
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
    [TestCase(typeof(TradeUiVendorPopupTranslationPatch), new[]
    {
        "XRL.World.GameObject|System.Single|XRL.UI.TradeUI+TradeScreenMode",
        "XRL.World.GameObject|XRL.World.GameObject|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Boolean",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(SteamWorkshopUploaderViewTranslationPatch), new string[0])]
    [TestCase(typeof(ModInfoTranslationPatch), new[]
    {
        "",
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
    [TestCase(typeof(PopupShowColorPickerTranslationPatch), new[]
    {
        "System.String|System.Int32|System.String|System.Int32|System.Boolean|System.Boolean|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.String",
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
    [TestCase(typeof(RepairTranslationPatch), new[]
    {
        "XRL.World.InventoryActionEvent",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.InventoryActionEvent",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.GameObject",
    })]
    [TestCase(typeof(PlayerDanceRitualTranslationPatch), new[]
    {
        "XRL.World.Event",
        "System.String|System.String",
        "System.String",
        "System.String",
        "System.String",
        "System.String",
    })]
    [TestCase(typeof(BeguilingSifrahTranslationPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
    })]
    [TestCase(typeof(ProselytizationSifrahTranslationPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
    })]
    [TestCase(typeof(RebukingSifrahTranslationPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject",
    })]
    [TestCase(typeof(ExaminerTranslationPatch), new[]
    {
        "XRL.World.InventoryActionEvent",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject|System.Int32",
        "System.Boolean",
        "System.Boolean",
    })]
    [TestCase(typeof(ExaminerMakeUnderstandingInventoryRefreshPatch), new[]
    {
        "System.Boolean",
        "System.Boolean",
    })]
    [TestCase(typeof(ExaminerResultUnderstandingInventoryRefreshPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject|System.Int32",
    })]
    [TestCase(typeof(ItemNamingTranslationPatch), new[]
    {
        "XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean",
        "XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|XRL.World.GameObject|XRL.World.GameObject|System.String|System.Boolean&|System.Int32&|System.Boolean&",
        "XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.String|System.Boolean|System.Int32|System.Boolean",
        "XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.Boolean",
        "System.Text.RegularExpressions.Match",
    })]
    [TestCase(typeof(TinkeringHelpersMakersMarkTranslationPatch), new[]
    {
        "XRL.World.GameObject|XRL.World.GameObject|XRL.World.Parts.IModification|System.String",
    })]
    [TestCase(typeof(SavesApiFatalSaveErrorTranslationPatch), new[]
    {
        "System.Exception|System.String",
    })]
    [TestCase(typeof(EquipmentScreenBodypartEquipPopupTranslationPatch), new[]
    {
        "XRL.World.GameObject|XRL.World.Anatomy.BodyPart",
    })]
    [TestCase(typeof(ModDisguiseBeingAppliedPopupTranslationPatch), new[]
    {
        "XRL.World.GameObject|XRL.World.GameObject",
    })]
    [TestCase(typeof(DeployableInfrastructureTranslationPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject|XRL.World.Cell|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(DesalinationPelletTranslationPatch), new[]
    {
        "XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(EnclosingTranslationPatch), new[]
    {
        "XRL.World.GameObject|XRL.World.IEvent",
        "XRL.World.GameObject|XRL.World.IEvent|XRL.World.Effects.Enclosed",
        "XRL.World.GameObject|System.Boolean|XRL.World.Effects.Enclosed",
    })]
    [TestCase(typeof(ClonelingVehicleTranslationPatch), new[]
    {
        "XRL.World.InventoryActionEvent",
        "",
        "XRL.World.InventoryActionEvent",
        "XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(AsleepMessageTranslationPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.BeginTakeActionEvent",
    })]
    [TestCase(typeof(CookingRuntimeTranslationPatch), new[]
    {
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.GameObject",
        "XRL.World.Event",
        "XRL.World.Event",
        "XRL.World.Event",
        "XRL.World.Event",
        "XRL.World.BeforeApplyDamageEvent",
        "XRL.World.GameObject|XRL.World.GameObject|XRL.World.Damage",
        "",
        "XRL.World.GameObject|System.Boolean",
        "XRL.World.Conversations.EnteredElementEvent",
    })]
    [TestCase(typeof(CookingRecipeDisplayNameTranslationPatch), new[]
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

    [Test]
    public void CookingRuntimeTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(CookingRuntimeTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.Effects.BasicCookingEffect_Hitpoints|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.BasicCookingEffect_MA|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.BasicCookingEffect_MS|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.BasicCookingEffect_Quickness|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.BasicCookingEffect_ToHit|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.BasicCookingEffect_XP|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.BasicCookingEffect_Regeneration|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.BasicCookingEffect_RandomStat|ApplyEffect|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.CookingDomainSpecial_UnitCrystalTransform|ApplyTo|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.CookingDomainSpecial_UnitSlogTransform|ApplyTo|System.Void|XRL.World.GameObject",
            "XRL.World.Effects.CookingDomainReflect_UnitReflectDamage|FireEvent|System.Void|XRL.World.Event",
            "XRL.World.Effects.CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.ReflectDamage|HandleEvent|System.Boolean|XRL.World.BeforeApplyDamageEvent",
            "XRL.World.Parts.ModBlinkEscape|CheckBlinkEscape|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Damage",
            "XRL.World.Effects.CookingDomainTeleport_UnitBlink|FireEvent|System.Void|XRL.World.Event",
            "XRL.World.Effects.NoPhase_ProceduralCookingTriggeredAction_Effect|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Effects.ProceduralCookingEffectWithTrigger|Trigger|System.Void",
            "XRL.World.Skills.Cooking.CookingRecipe|ApplyEffectsTo|System.Boolean|XRL.World.GameObject|System.Boolean",
            "XRL.World.Conversations.Parts.WaterRitualCookingRecipe|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        }));
    }

    [Test]
    public void CookingRecipeDisplayNameTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(CookingRecipeDisplayNameTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.Skills.Cooking.CookingRecipe|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.AppleMatz|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.BoneBabka|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.CloacaSurprise|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.CrystalDelight|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.GoatAndSweetLeaf|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.HotandSpiny|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.MahLahSoup|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.MushroomCider|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.ThePorridge|GetDisplayName|System.String",
            "XRL.World.Skills.Cooking.TongueAndCheek|GetDisplayName|System.String",
        }));
    }

    [Test]
    public void VillageWallDescriptionTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(VillageWallDescriptionTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.ZoneBuilders.VillageBase|getAVillageCanvas|XRL.World.GameObject",
            "XRL.World.ZoneBuilders.VillageBase|getAVillageWall|XRL.World.GameObject",
            "XRL.World.ZoneBuilders.VillageCodaBase|getAVillageCanvas|XRL.World.GameObject",
            "XRL.World.ZoneBuilders.VillageCodaBase|getAVillageWall|XRL.World.GameObject",
        }));
    }

    [Test]
    public void VillagePetConversationTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(VillagePetConversationTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.ZoneBuilders.VillageBase|AddVillagerConversation|System.Void|XRL.World.GameObject|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean",
            "XRL.World.ZoneBuilders.VillageCodaBase|AddVillagerConversation|System.Void|XRL.World.GameObject|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean",
        }));
    }

    [Test]
    public void VillageLeaderConversationTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(VillageLeaderConversationTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.ZoneBuilders.VillageBase|AddVillagerConversation|System.Void|XRL.World.GameObject|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean",
            "XRL.World.ZoneBuilders.VillageCodaBase|AddVillagerConversation|System.Void|XRL.World.GameObject|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean",
            "Qud.API.ConversationsAPI|addSimpleConversationToObject|XRL.World.Conversations.ConversationXMLBlueprint|XRL.World.GameObject|System.String|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean",
        }));
    }

    [Test]
    public void MemorialInscriptionIntroTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(MemorialInscriptionIntroTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.Parts.Tombstone|GenerateTombstone|System.Void",
            "XRL.World.Parts.RachelsTombstone|GenerateTombstone|System.Void",
            "XRL.World.Parts.EaterUrn|GenerateUrn|System.Void",
        }));
    }

    [Test]
    public void DynamicQuestConstructorConversationTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(DynamicQuestConstructorConversationTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.ZoneBuilders.FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver|addQuestConversationToGiver|System.Void|XRL.World.GameObject|XRL.World.Quest|XRL.World.GameObject",
            "XRL.World.ZoneBuilders.FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver|addQuestConversationToGiver|System.Void|XRL.World.GameObject|XRL.World.Quest",
            "XRL.World.ZoneBuilders.InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver|addQuestConversationToGiver|System.Void|XRL.World.GameObject|XRL.World.Quest|XRL.World.GameObject",
        }));
    }

    [Test]
    public void DynamicQuestGeneratedQuestTextTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(DynamicQuestGeneratedQuestTextTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.ZoneBuilders.FindASpecificItemDynamicQuestTemplate_FabricateQuestGiver|fabricateFindASpecificItemQuest|XRL.World.Quest|XRL.World.GameObject|System.String",
            "XRL.World.ZoneBuilders.FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver|fabricateFindASpecificSiteQuest|XRL.World.Quest|XRL.World.GameObject",
            "XRL.World.ZoneBuilders.InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver|fabricateInteractWithAnObjectQuest|XRL.World.Quest|XRL.World.GameObject|System.String",
        }));
    }

    [Test]
    public void DynamicQuestIntroChoiceTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(DynamicQuestIntroChoiceTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.DynamicQuestConversationHelper|fabricateIntroAcceptChoice|XRL.World.Conversations.ConversationXMLBlueprint|System.String|XRL.World.Conversations.ConversationXMLBlueprint|XRL.World.Quest",
            "XRL.World.DynamicQuestConversationHelper|fabricateIntroRejectChoice|XRL.World.Conversations.ConversationXMLBlueprint|System.String|XRL.World.Conversations.ConversationXMLBlueprint",
            "XRL.World.DynamicQuestConversationHelper|fabricateIntroAdditionalChoice|XRL.World.Conversations.ConversationXMLBlueprint|System.String|XRL.World.Conversations.ConversationXMLBlueprint",
        }));
    }

    [Test]
    public void PsychicHunterGeneratedTitleTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(PsychicHunterGeneratedTitleTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.PsychicHunterSystem|CreateSeekerHunters|System.Void|System.Int32|XRL.World.Zone",
            "XRL.PsychicHunterSystem|CreateExtradimensionalSoloHunters|System.Void|XRL.World.Zone|System.Int32|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
            "XRL.PsychicHunterSystem|CreateExtradimensionalSoloDeviant|System.Void|XRL.World.Zone",
            "XRL.PsychicHunterSystem|CreateExtradimensionalCultHunters|System.Void|XRL.World.Zone|System.Int32|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
        }));
    }

    [Test]
    public void SettlementFarmNameTranslationPatch_TargetMethod_ResolvesExpectedOwner()
    {
        var method = InvokeTargetMethod(typeof(SettlementFarmNameTranslationPatch));

        Assert.That(
            FullMethodSignature(method!),
            Is.EqualTo("XRL.Names.SettlementNames|GenerateFarmName|System.String|HistoryKit.History|System.String"));
    }

    [Test]
    public void DimensionManagerGeneratedNameTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(DimensionManagerGeneratedNameTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.Encounters.DimensionManager|InitializeFaction|XRL.World.Encounters.PsychicFaction",
        }));
    }

    [Test]
    public void DimensionManagerExtraDimensionNameTranslationPatch_TargetMethod_ResolvesExpectedOwner()
    {
        var method = InvokeTargetMethod(typeof(DimensionManagerExtraDimensionNameTranslationPatch));

        Assert.That(
            FullMethodSignature(method!),
            Is.EqualTo("XRL.World.Encounters.DimensionManager|GenerateMoreDimensions|System.Void"));
    }

    [Test]
    public void TombstoneDeathCauseTranslationPatch_TargetMethods_ResolveExpectedOwners()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(TombstoneDeathCauseTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.Parts.Tombstone|GenerateTombstone|System.Void",
            "XRL.World.Parts.RachelsTombstone|GenerateTombstone|System.Void",
        }));
    }

    [TestCase(typeof(GameObjectStatPopupTranslationPatch), new[]
    {
        "XRL.World.GameObject|GainSP|System.Void|System.Int32|System.Boolean",
        "XRL.World.GameObject|GainEgo|System.Void|System.Int32|System.Boolean",
        "XRL.World.GameObject|LoseEgo|System.Void|System.Int32|System.Boolean",
        "XRL.World.GameObject|GainIntelligence|System.Void|System.Int32|System.Boolean",
        "XRL.World.GameObject|GainWillpower|System.Void|System.Int32|System.Boolean",
    })]
    [TestCase(typeof(GameObjectPopupTranslationPatch), new[]
    {
        "XRL.World.GameObject|ConfirmUseImportantAsync|System.Threading.Tasks.Task`1[[System.Boolean]]|XRL.World.GameObject|System.String|System.String|System.Int32",
        "XRL.World.GameObject|ConfirmUseImportant|System.Boolean|XRL.World.GameObject|System.String|System.String|System.Int32",
        "XRL.World.GameObject|HandleInventoryActionEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.GameObject|HandleRename|System.Void|XRL.World.InventoryActionEvent",
        "XRL.World.GameObject|ChangeCompanionAbilityUse|System.Void|XRL.World.GameObject|XRL.World.Parts.ActivatedAbilities",
        "XRL.World.GameObject|CheckCompanionDirection|System.Boolean|XRL.World.GameObject",
        "XRL.World.GameObject|PullDown|System.Void|System.Boolean",
    })]
    [TestCase(typeof(SultanMuralDisplayNameTranslationPatch), new[]
    {
        "XRL.World.Parts.SultanMuralController|updateHistoricMural|System.Void|System.Collections.Generic.List`1[[XRL.World.Cell]]|HistoryKit.HistoricEvent",
        "XRL.World.Parts.SultanMuralController|ruinMural|System.Void|System.Collections.Generic.List`1[[XRL.World.Cell]]|HistoryKit.HistoricEvent",
    })]
    [TestCase(typeof(RealityStabilizedInterdictTranslationPatch), new[]
    {
        "XRL.World.Effects.RealityStabilized|ShowGenericInterdictMessage|System.Void|XRL.World.GameObject|XRL.World.Event",
        "XRL.World.Effects.RealityStabilized|ShowDistantInterdictMessage|System.Void|XRL.World.GameObject|XRL.World.Event",
        "XRL.World.Effects.RealityStabilized|ShowDualInterdictMessage|System.Void|XRL.World.GameObject|XRL.World.Event",
    })]
    [TestCase(typeof(HackingSifrahResultTranslationPatch), new[]
    {
        "XRL.World.Parts.Door|HackingResultSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.Door|HackingResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.Door|HackingResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.Door|HackingResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.Door|HackingResultCriticalFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.PowerSwitch|HackingResultSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.PowerSwitch|HackingResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.PowerSwitch|HackingResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.PowerSwitch|HackingResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.PowerSwitch|HackingResultCriticalFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.TemplarPhylactery|HackingResultSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.TemplarPhylactery|HackingResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.TemplarPhylactery|HackingResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.TemplarPhylactery|HackingResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.TemplarPhylactery|HackingResultCriticalFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.CyberneticsTerminal2|HackingResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.CyberneticsTerminal2|HackingResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.CyberneticsTerminal2|HackingResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
        "XRL.World.Parts.CyberneticsTerminal2|HackingResultCriticalFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah",
    })]
    [TestCase(typeof(QuestLifecyclePopupTranslationPatch), new[]
    {
        "XRL.World.Quest|ShowStartPopup|System.Void",
        "XRL.World.Quest|ShowFailPopup|System.Void",
        "XRL.World.Quest|ShowFailStepPopup|System.Void|XRL.World.QuestStep",
        "XRL.World.Quest|ShowFinishPopup|System.Void",
        "XRL.World.Quest|ShowFinishStepPopup|System.Void|XRL.World.QuestStep",
    })]
    [TestCase(typeof(FlightTranslationPatch), new[]
    {
        "XRL.World.Capabilities.Flight|StartFlying|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Capabilities.IFlightSource",
        "XRL.World.Capabilities.Flight|StopFlying|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Capabilities.IFlightSource|System.Boolean|System.Boolean",
        "XRL.World.Capabilities.Flight|Land|System.Void|XRL.World.GameObject|System.Boolean",
        "XRL.World.Capabilities.Flight|FailFlying|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Capabilities.IFlightSource",
    })]
    [TestCase(typeof(BodyTranslationPatch), new[]
    {
        "XRL.World.Parts.Body|CheckUnsupportedPartLoss|System.Void",
        "XRL.World.Parts.Body|CheckPartRecovery|System.Void",
        "XRL.World.Parts.Body|Dismember|XRL.World.GameObject|XRL.World.Anatomy.BodyPart|XRL.World.GameObject|XRL.World.IInventory|System.Boolean|System.Boolean|XRL.World.IEvent",
        "XRL.World.Parts.Body|RegenerateLimb|System.Boolean|System.Boolean|XRL.World.Parts.Body+DismemberedPart|System.Nullable`1[[System.Int32]]|System.Nullable`1[[System.Int32]]|System.Int32[]|System.Nullable`1[[System.Int32]]|System.Int32[]|System.Boolean",
    })]
    [TestCase(typeof(ItemModdingSifrahTranslationPatch), new[]
    {
        "XRL.World.ItemModdingSifrah|ResultFailure|System.Void|XRL.World.GameObject",
        "XRL.World.ItemModdingSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.ItemModdingSifrah|ResultSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.ItemModdingSifrah|ResultCriticalSuccess|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(SifrahPureOwnerPopupTranslationPatch), new[]
    {
        "XRL.World.BaetylOfferingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
        "XRL.World.FormalWaterRitualSifrah|.ctor|System.Void|XRL.World.GameObject",
        "XRL.World.HagglingSifrah|.ctor|System.Void|XRL.World.GameObject",
        "XRL.World.DisarmingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Boolean",
        "XRL.World.ExamineSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32|System.Int32",
        "XRL.World.HackingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32",
        "XRL.World.ProselytizationSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
        "XRL.World.RebukingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
        "XRL.World.ItemModdingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32",
        "XRL.World.ItemNamingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
        "XRL.World.RepairSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32",
        "XRL.World.PsychicCombatSifrah|.ctor|System.Void|XRL.World.GameObject|System.String|System.Int32|System.Int32|System.String",
        "XRL.World.RealityDistortionSifrah|.ctor|System.Void|XRL.World.GameObject|System.String|System.String|System.Int32|System.Int32",
        "XRL.World.ReverseEngineeringSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32|XRL.World.Tinkering.TinkerData",
        "XRL.SifrahGame|UseInsight|System.Void|XRL.World.GameObject",
        "XRL.World.BaetylOfferingSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.BeguilingSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.FormalWaterRitualSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.HagglingSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.ItemNamingSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.ProselytizationSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.PsychicCombatSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.RebukingSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.ReverseEngineeringSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
        "XRL.World.ReverseEngineeringSifrah|Finish|System.Void|XRL.World.GameObject",
        "XRL.World.RitualSifrahTokenAttributeSacrifice|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.RitualSifrahTokenInvokeHigherBeing|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.SocialSifrahTokenSecret|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenBit|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenCharge|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenComputePower|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenLiquid|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.SifrahGame|MakeMoveForSlot|System.Boolean|System.Int32|XRL.World.GameObject",
    })]
    [TestCase(typeof(SifrahTokenItemPopupTranslationPatch), new[]
    {
        "XRL.World.SocialSifrahTokenGift|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.SocialSifrahTokenItem|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
    })]
    [TestCase(typeof(SifrahTokenGetDescriptionTranslationPatch), new[]
    {
        "XRL.World.SocialSifrahTokenGift|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.SocialSifrahTokenItem|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.SocialSifrahTokenSecret|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.SocialSifrahTokenLeverageBeingFavored|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.SocialSifrahTokenLeverageBeingLoved|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenBit|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenCharge|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenCopperWire|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
        "XRL.World.TinkeringSifrahTokenLiquid|GetDescription|System.String|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
    })]
    [TestCase(typeof(SunderMindTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.SunderMind|CancelSunder|System.Void",
        "XRL.World.Parts.Mutation.SunderMind|BeginSunder|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.Mutation.SunderMind|PenetrationFailure|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.Mutation.SunderMind|Tick|System.Void",
        "XRL.World.Parts.Mutation.SunderMind|Nosebleed|System.Void|XRL.World.GameObject|XRL.World.Effects.MemberOfPsychicBattle",
    })]
    [TestCase(typeof(LiquidWarmStaticTranslationPatch), new[]
    {
        "XRL.Liquids.LiquidWarmStatic|GlitchSkills|System.Boolean|XRL.World.GameObject",
        "XRL.Liquids.LiquidWarmStatic|GlitchMutations|System.Boolean|XRL.World.GameObject",
        "XRL.Liquids.LiquidWarmStatic|GlitchLiquidComponents|System.Boolean|XRL.World.GameObject|System.String|System.Int32|System.Boolean",
        "XRL.Liquids.LiquidWarmStatic|WishWarmEffect|System.Void",
        "XRL.Liquids.LiquidWarmStatic|WishWarmEffectSpec|System.Void|System.String",
    })]
    [TestCase(typeof(KeybindsScreenConflictTranslationPatch), new[]
    {
        "Qud.UI.KeybindsScreen|ConfirmConflictBind|System.Threading.Tasks.Task`1[[System.Boolean]]|System.String|System.Collections.Generic.List`1[[XRL.UI.GameCommand]]|System.String",
        "Qud.UI.KeybindsScreen|ConfirmDynamicConflictBind|System.Threading.Tasks.Task`1[[System.Boolean]]|System.String|System.Collections.Generic.List`1[[XRL.UI.GameCommand]]|System.String",
        "Qud.UI.KeybindsScreen|RequiredConflictBind|System.Threading.Tasks.Task|System.String|System.String",
    })]
    [TestCase(typeof(AbilityManagerPopupTranslationPatch), new[]
    {
        "Qud.UI.AbilityManagerScreen|HandleFilterItems|System.Void",
        "Qud.UI.AbilityManagerScreen+<showScreen>d__18|MoveNext|System.Void",
        "Qud.UI.AbilityManagerScreen+<HandleRebindAsync>d__47|MoveNext|System.Void",
        "Qud.UI.AbilityManagerScreen+<HandleRemoveBindAsync>d__48|MoveNext|System.Void",
    })]
    [TestCase(typeof(QudMutationsModuleWindowVariantPopupTranslationPatch), new[]
    {
        "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow+<SelectVariant>d__29|MoveNext|System.Void",
    })]
    [TestCase(typeof(CodeRedemptionPopupTranslationPatch), new[]
    {
        "CodeRedemptionManager+<redeemNoProgress>d__0|MoveNext|System.Void",
        "CodeRedemptionManager+<>c__DisplayClass1_0+<<redeem>b__0>d|MoveNext|System.Void",
    })]
    [TestCase(typeof(XrlCoreRestoreModsLoadedTranslationPatch), new[]
    {
        "XRL.Core.XRLCore+<RestoreModsLoadedAsync>d__121|MoveNext|System.Void",
    })]
    [TestCase(typeof(SkillsAndPowersSelectNodePopupTranslationPatch), new[]
    {
        "XRL.UI.SkillsAndPowersScreen|SelectNode|System.Void|XRL.UI.SPNode|XRL.World.GameObject",
    })]
    [TestCase(typeof(RealityStabilizedEventTranslationPatch), new[]
    {
        "XRL.World.Effects.RealityStabilized|TryContest|XRL.World.Effects.RealityStabilized+ContestResult|XRL.World.GameObject|System.Int32|System.Int32",
        "XRL.World.Effects.RealityStabilized|OptionToContest|XRL.World.Effects.RealityStabilized+ContestResult|XRL.World.GameObject|System.Int32|System.Boolean",
        "XRL.World.Effects.RealityStabilized|FailedToContest|System.Void|XRL.World.GameObject",
        "XRL.World.Effects.RealityStabilized|ShortCircuitDevice|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Event",
    })]
    [TestCase(typeof(MassMindTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.MassMind|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(TelekinesisTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Telekinesis|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Mutation.Telekinesis|Activate|System.Boolean|System.Boolean",
        "XRL.World.Parts.Mutation.Telekinesis|AttemptTelekinesis|System.Boolean",
    })]
    [TestCase(typeof(CyberneticRejectionSyndromeTranslationPatch), new[]
    {
        "XRL.World.Effects.CyberneticRejectionSyndrome|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.CyberneticRejectionSyndrome|Remove|System.Void|XRL.World.GameObject",
        "XRL.World.Effects.CyberneticRejectionSyndrome|Reduce|System.Void|System.Int32",
    })]
    [TestCase(typeof(GeomagneticDiscTranslationPatch), new[]
    {
        "XRL.World.Parts.GeomagneticDisc|SignalFailure|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.GeomagneticDisc|SignalLowPower|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.GeomagneticDisc|ExamineFailure|System.Boolean|XRL.World.IExamineEvent|System.Int32",
    })]
    [TestCase(typeof(CampfireCookAvailabilityTranslationPatch), new[]
    {
        "XRL.World.Parts.Campfire|Cook|System.Boolean",
    })]
    [TestCase(typeof(CampfireNostrumsTranslationPatch), new[]
    {
        "XRL.World.Parts.Campfire|NostrumsStopBleeding|System.Void",
        "XRL.World.Parts.Campfire|NostrumsTreatPoison|System.Void",
        "XRL.World.Parts.Campfire|NostrumsTreatIllness|System.Void",
        "XRL.World.Parts.Campfire|NostrumsTreatDiseaseOnset|System.Void",
    })]
    [TestCase(typeof(TeleprojectorTranslationPatch), new[]
    {
        "XRL.World.Parts.Teleprojector|HandleEvent|System.Boolean|XRL.World.BootSequenceDoneEvent",
        "XRL.World.Parts.Teleprojector|ActivateTeleprojector|System.Boolean",
        "XRL.World.Parts.Teleprojector|RoboDom|System.Boolean|XRL.World.MentalAttackEvent",
    })]
    [TestCase(typeof(TombAnchorSystemTranslationPatch), new[]
    {
        "XRL.ITombAnchorSystem|OnEndTurn|System.Void",
        "XRL.ITombAnchorSystem|Recall|System.Void|XRL.World.Zone",
        "XRL.ITombAnchorSystem|AnchorCall|System.Void",
    })]
    [TestCase(typeof(CyberneticsMedassistModuleTranslationPatch), new[]
    {
        "XRL.World.Parts.CyberneticsMedassistModule|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.CyberneticsMedassistModule|AttemptMedicalAssistance|System.Void|XRL.World.Damage",
    })]
    [TestCase(typeof(LiquidLoaderTranslationPatch), new[]
    {
        "XRL.World.Parts.BioAmmoLoader|HandleEvent|System.Boolean|XRL.World.CommandReloadEvent",
        "XRL.World.Parts.BioAmmoLoader|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.BioAmmoLoader|HandleEvent|System.Boolean|XRL.World.CheckLoadAmmoEvent",
        "XRL.World.Parts.BioAmmoLoader|HandleEvent|System.Boolean|XRL.World.LoadAmmoEvent",
        "XRL.World.Parts.BioAmmoLoader|HandleEvent|System.Boolean|XRL.World.GetNotReadyToFireMessageEvent",
        "XRL.World.Parts.LiquidAmmoLoader|HandleEvent|System.Boolean|XRL.World.CommandReloadEvent",
        "XRL.World.Parts.LiquidAmmoLoader|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.ModLiquidCooled|HandleEvent|System.Boolean|XRL.World.CommandReloadEvent",
        "XRL.World.Parts.ModLiquidCooled|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(EnergyLoaderCannotTakeTranslationPatch), new[]
    {
        "XRL.World.Parts.ElectricalDischargeLoader|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.EnergyAmmoLoader|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(EnergyCellSocketAccessPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.EnergyCellSocket|AttemptReplaceCell|System.Boolean|XRL.World.GameObject|XRL.World.InventoryActionEvent|System.Int32|XRL.World.GameObject",
    })]
    [TestCase(typeof(EquipmentApiTwiddleObjectTranslationPatch), new[]
    {
        "Qud.API.EquipmentAPI|TwiddleObject|System.Void|XRL.World.GameObject|XRL.World.GameObject|System.Boolean&|XRL.World.InventoryAction&|System.Boolean|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(CampfireRemainsAttemptLightTranslationPatch), new[]
    {
        "XRL.World.Parts.CampfireRemains|AttemptLight|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(TrollKingTranslationPatch), new[]
    {
        "XRL.World.Parts.TrollKing|CheckSpawn|System.Void|System.Int32",
        "XRL.World.Parts.TrollKing|StopBudding|System.Void|System.Int32",
    })]
    [TestCase(typeof(MutatingTranslationPatch), new[]
    {
        "XRL.World.Effects.Mutating|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Mutating|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
    })]
    [TestCase(typeof(QuillsTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Quills|HandleEvent|System.Boolean|XRL.World.TookDamageEvent",
        "XRL.World.Parts.Mutation.Quills|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(LightManipulationTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.LightManipulation|HandleEvent|System.Boolean|XRL.World.CommandEvent",
        "XRL.World.Parts.Mutation.LightManipulation|Lase|System.Boolean|XRL.World.Cell|System.Int32",
    })]
    [TestCase(typeof(LatchesOnTranslationPatch), new[]
    {
        "XRL.World.Parts.LatchesOn|HandleEvent|System.Boolean|XRL.World.UnequippedEvent",
        "XRL.World.Parts.LatchesOn|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(AsleepOwnerTranslationPatch), new[]
    {
        "XRL.World.Effects.Asleep|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Asleep|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
        "XRL.World.Effects.Asleep|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(BuddingTranslationPatch), new[]
    {
        "XRL.World.Effects.Budding|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Budding|Remove|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(BeguilingTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Beguiling|Cast|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Mutation.Beguiling|XRL.World.Event|System.Int32",
        "XRL.World.Parts.Mutation.Beguiling|Beguile|System.Boolean|XRL.World.MentalAttackEvent",
    })]
    [TestCase(typeof(AscensionCableTranslationPatch), new[]
    {
        "XRL.World.Parts.AscensionCable|TryAscend|System.Boolean|XRL.World.GameObject|System.Boolean",
        "XRL.World.Parts.AscensionCable|TryDescend|System.Boolean|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(CarapaceTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Carapace|Tighten|System.Void|System.Boolean",
        "XRL.World.Parts.Mutation.Carapace|Loosen|System.Void|System.Boolean",
    })]
    [TestCase(typeof(SvardymSystemTranslationPatch), new[]
    {
        "XRL.SvardymSystem|BeginStorm|System.Void",
        "XRL.SvardymSystem|Tick|System.Void",
    })]
    [TestCase(typeof(PhasedTranslationPatch), new[]
    {
        "XRL.World.Effects.Phased|HandleEvent|System.Boolean|XRL.World.EffectAppliedEvent",
        "XRL.World.Effects.Phased|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
        "XRL.World.Effects.Phased|Remove|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(PersuasionRebukeRobotTranslationPatch), new[]
    {
        "XRL.World.Parts.Skill.Persuasion_RebukeRobot|Rebuke|System.Boolean|XRL.World.MentalAttackEvent",
    })]
    [TestCase(typeof(NephalPropertiesTranslationPatch), new[]
    {
        "XRL.World.Parts.NephalProperties|TryPacify|System.Boolean",
    })]
    [TestCase(typeof(TonicTranslationPatch), new[]
    {
        "XRL.World.Parts.Tonic|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(TonicApplicatorTranslationPatch), new[]
    {
        "XRL.World.Parts.LoveTonicApplicator|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.SphynxSalt_Tonic_Applicator|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(XrlGameTranslationPatch), new[]
    {
        "XRL.XRLGame|FinishQuestStep|System.Boolean|XRL.World.Quest|System.String|System.Int32|System.Boolean|System.String",
    })]
    [TestCase(typeof(IntegratedWeaponHostsTranslationPatch), new[]
    {
        "XRL.World.Capabilities.IntegratedWeaponHosts|GenerateTurret|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.Boolean",
        "XRL.World.Capabilities.IntegratedWeaponHosts|HandleTurretWish|System.Boolean|System.Text.RegularExpressions.Match",
    })]
    [TestCase(typeof(BoostStatisticTranslationPatch), new[]
    {
        "XRL.World.Effects.BoostStatistic|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.BoostStatistic|Remove|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(EmboldenedTranslationPatch), new[]
    {
        "XRL.World.Effects.Emboldened|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Emboldened|Remove|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(FungalSporeInfectionTranslationPatch), new[]
    {
        "XRL.World.Effects.FungalSporeInfection|ChooseLimbForInfection|System.Boolean|System.Collections.Generic.List`1[[XRL.World.Anatomy.BodyPart]]|System.String|XRL.World.Anatomy.BodyPart&|System.String&|System.Boolean",
        "XRL.World.Effects.FungalSporeInfection|ApplyFungalInfection|System.Boolean|XRL.World.GameObject|System.String|XRL.World.Anatomy.BodyPart",
        "XRL.World.Effects.FungalSporeInfection|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.GasFungalSpores|ApplyGas|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.PaxInfection|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.PuffInfection|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(HealingTranslationPatch), new[]
    {
        "XRL.World.Effects.Healing|HandleEvent|System.Boolean|XRL.World.UseEnergyEvent",
        "XRL.World.Effects.Healing|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(StressedTranslationPatch), new[]
    {
        "XRL.World.Effects.Stressed|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Stressed|Remove|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(MonochromeOnsetTranslationPatch), new[]
    {
        "XRL.World.Effects.MonochromeOnset|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.MonochromePoisonOnDamage|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(GlotrotOnsetTranslationPatch), new[]
    {
        "XRL.World.Effects.GlotrotOnset|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(IronshankTranslationPatch), new[]
    {
        "XRL.World.Effects.Ironshank|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(IronshankOnsetTranslationPatch), new[]
    {
        "XRL.World.Effects.IronshankOnset|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(AdrenalControlTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.AdrenalControl2|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(AmnesiaTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Amnesia|HandleEvent|System.Boolean|XRL.World.SecretVisibilityChangedEvent",
        "XRL.World.Parts.Mutation.Amnesia|HandleEvent|System.Boolean|XRL.World.EnteredCellEvent",
    })]
    [TestCase(typeof(BlinkingTicTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.BlinkingTic|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.BlinkingTicSickness|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(BrittleBonesTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.BrittleBones|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(ElectromagneticImpulseTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.ElectromagneticImpulse|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(FearAuraTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.FearAura|ApplyFear|System.Boolean|XRL.World.MentalAttackEvent",
    })]
    [TestCase(typeof(MeditatingTranslationPatch), new[]
    {
        "XRL.World.Effects.Meditating|Remove|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(RegenerationTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Regeneration|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(EffectStaticMessageTranslationPatch), new[]
    {
        "XRL.World.Effects.AxonsDeflated|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.AxonsInflated|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.BasiliskPoison|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.Berserk|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
        "XRL.World.Effects.Cudgel_SmashingUp|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.EmptyTheClips|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Exhausted|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Flagging|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
        "XRL.World.Effects.NocturnalApexed|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Paralyzed|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
    })]
    [TestCase(typeof(EffectMobilityBlockTranslationPatch), new[]
    {
        "XRL.World.Effects.Engulfed|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.Immobilized|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.Stuck|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(MutationInfectionTranslationPatch), new[]
    {
        "XRL.World.Effects.MutationInfection|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(PsychometryTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Psychometry|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(SpindleNegotiationTranslationPatch), new[]
    {
        "XRL.World.Parts.SpindleNegotiation|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(FriendOrFoeReasonTranslationPatch), new[]
    {
        "XRL.World.Parts.GenerateFriendOrFoe|replacePlaceholders|System.String|System.String",
        "XRL.World.Parts.GenerateFriendOrFoe_HEB|replacePlaceholders|System.String|System.String",
    })]
    [TestCase(typeof(MutationActionFailureTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.ElectricalGeneration|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Mutation.ElectricalGeneration|PerformDischarge|System.Boolean|System.Boolean",
        "XRL.World.Parts.Mutation.RepellingForce|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.TeleportOther|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(MutationGeneratedTextTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.PhotosyntheticSkin|HandleEvent|System.Boolean|XRL.World.CommandEvent",
        "XRL.World.Parts.Mutation.LifeDrain|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.PackRat|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.Belcher|Cast|System.Boolean|XRL.World.Parts.Mutation.Belcher|System.String|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(PickTargetShowPickerTranslationPatch), new[]
    {
        "XRL.UI.PickTarget|ShowPicker|XRL.World.Cell|XRL.UI.PickTarget+PickStyle|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean|XRL.World.AllowVis|System.Predicate`1[[XRL.World.GameObject]]|System.Predicate`1[[XRL.World.GameObject]]|XRL.World.GameObject|System.Nullable`1[[Genkit.Point2D]]|System.String|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(MissileWeaponShowPickerTranslationPatch), new[]
    {
        "XRL.World.Parts.MissileWeapon|ShowPicker|XRL.World.Parts.MissilePath|System.Int32|System.Int32|System.Boolean|XRL.World.AllowVis|System.Int32|System.Boolean|XRL.World.GameObject|XRL.World.Parts.FireType&|System.Int32",
    })]
    [TestCase(typeof(DisassemblyStartTranslationPatch), new[]
    {
        "XRL.World.Tinkering.Disassembly|Continue|System.Boolean",
        "XRL.World.Tinkering.Disassembly|End|System.Void",
    })]
    [TestCase(typeof(DanceRitualOpponentTranslationPatch), new[]
    {
        "XRL.World.Parts.DanceRitualOpponent|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.DanceRitualOpponent|HandleEvent|System.Boolean|XRL.World.BeforeAITakingActionEvent",
        "XRL.World.Parts.DanceRitualOpponent|Register|System.Void|XRL.World.GameObject|XRL.IEventRegistrar",
    })]
    [TestCase(typeof(IExamineEventProcessIdentifyTranslationPatch), new[]
    {
        "XRL.World.IExamineEvent|ProcessIdentify|System.Boolean",
    })]
    [TestCase(typeof(SelfTearExplosionTranslationPatch), new[]
    {
        "XRL.World.Parts.Clockwork|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Flywheel|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(SystemStaticMessageTranslationPatch), new[]
    {
        "XRL.CheckpointingSystem|CheckpointOn|System.Boolean",
        "XRL.HolyPlaceSystem|SetHolyZone|System.Void|XRL.World.Zone|XRL.World.Faction",
        "XRL.World.Parts.Mutation.HeightenedIntelligence|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.HeightenedAgility|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.Metamorphosis|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.QuantumJitters|Sunder|System.Void",
        "XRL.World.Parts.Mutation.SpacetimeVortex|Vortex|System.Void|XRL.World.Cell",
        "XRL.World.Parts.TrembleEarthquakes|Quake|System.Void",
        "XRL.World.Parts.WorldTeleporter|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.DoorSwitch|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.SpawningEggSac|tickEgg|System.Void",
        "XRL.World.Parts.LuminousInfection|TryGrowMushroom|System.Void",
        "XRL.World.Parts.TorchProperties|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
        "XRL.World.Effects.Lost|HandleEvent|System.Boolean|XRL.World.CanTravelEvent",
        "XRL.World.Parts.Mutation.Teleportation|Cast|System.Boolean|XRL.World.Parts.Mutation.Teleportation|System.String|XRL.World.IEvent|XRL.World.Cell|XRL.World.GameObject|System.Boolean|System.Int32",
        "XRL.World.Parts.CatacombsExitTeleporter|HandleEvent|System.Boolean|XRL.World.ObjectEnteredCellEvent",
    })]
    [TestCase(typeof(MutationAbsorptionHealingTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.ColdAbsorption|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.HeatAbsorption|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(OnEatRewardMessageTranslationPatch), new[]
    {
        "XRL.World.Parts.MPOnEat|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.RefreshAllCooldownsOnEat|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(TenfoldPathInitiatoryTranslationPatch), new[]
    {
        "XRL.World.Parts.Skill.TenfoldPath_Ket|HandleEvent|System.Boolean|XRL.World.BeforeDieEvent",
        "XRL.World.Parts.Skill.TenfoldPath_Vur|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.TenfoldPath_Yis|AddSkill|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(PowerEntryRequirementPopupTranslationPatch), new[]
    {
        "XRL.World.Skills.PowerEntry|MeetsRequirements|System.Boolean|XRL.World.GameObject|System.Boolean",
        "XRL.World.Skills.PowerEntryRequirement|MeetsRequirement|System.Boolean|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(MagneticPulseTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.MagneticPulse|EmitMagneticPulse|System.Void|XRL.World.GameObject|System.Int32",
    })]
    [TestCase(typeof(PetGloamingTranslationPatch), new[]
    {
        "XRL.World.Parts.PetGloaming|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(WindupTranslationPatch), new[]
    {
        "XRL.World.Parts.Windup|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(DamagePenetrationDebugTranslationPatch), new[]
    {
        "XRL.Rules.Stat|RollDamagePenetrations|System.Int32|System.Int32|System.Int32|System.Int32",
    })]
    [TestCase(typeof(SoundManagerSetChannelTrackTranslationPatch), new[]
    {
        "SoundManager+<SetChannelTrack>d__36|MoveNext|System.Void",
    })]
    [TestCase(typeof(BasePronounProviderCustomizePopupTranslationPatch), new[]
    {
        // Source owner: XRL.World.BasePronounProvider.CustomizeProcess(string); target is the async state machine.
        "XRL.World.BasePronounProvider+<CustomizeProcess>d__121|MoveNext|System.Void",
        // Source owner: XRL.World.Gender.CustomizeProcess(string); target is the async state machine.
        "XRL.World.Gender+<CustomizeProcess>d__59|MoveNext|System.Void",
    })]
    [TestCase(typeof(BaseMutationSelectVariantPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.BaseMutation|SelectVariant|System.Boolean|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(EmbarkBuilderValidationPopupTranslationPatch), new[]
    {
        "XRL.CharacterBuilds.EmbarkBuilder+<checkStateAsync>d__12|MoveNext|System.Void",
    })]
    [TestCase(typeof(FugueOnStepTranslationPatch), new[]
    {
        "XRL.World.Parts.FugueOnStep|Activate|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(MentalShieldTranslationPatch), new[]
    {
        "XRL.World.Parts.MentalShield|HandleEvent|System.Boolean|XRL.World.BeforeApplyDamageEvent",
        "XRL.World.Parts.MentalShield|HandleEvent|System.Boolean|XRL.World.BeginMentalDefendEvent",
    })]
    [TestCase(typeof(TabulaRasaeTranslationPatch), new[]
    {
        "XRL.World.Parts.TabulaRasae|HandleEvent|System.Boolean|XRL.World.BeforeApplyDamageEvent",
        "XRL.World.Parts.TabulaRasae|HandleEvent|System.Boolean|XRL.World.TookDamageEvent",
        "XRL.World.Parts.Mutation.Confusion|Confuse|System.Boolean|XRL.World.MentalAttackEvent|System.Boolean|System.Int32|System.Int32|System.Boolean",
    })]
    [TestCase(typeof(EatMemoriesOnHitTranslationPatch), new[]
    {
        "XRL.World.Parts.EatMemoriesOnHit|EatMemories|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String",
    })]
    [TestCase(typeof(CyberneticsStasisEntanglerTranslationPatch), new[]
    {
        "XRL.World.Parts.CyberneticsStasisEntangler|DeployToCells|XRL.World.GameObject|XRL.World.Zone|XRL.World.GameObject|XRL.World.GameObject|System.Int32|System.Int32",
    })]
    [TestCase(typeof(EngulfingTranslationPatch), new[]
    {
        "XRL.World.Parts.Engulfing|Engulf|System.Boolean|XRL.World.GameObject|XRL.World.Event",
    })]
    [TestCase(typeof(ClonelingVehicleTranslationPatch), new[]
    {
        "XRL.World.Parts.Cloneling|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Cloneling|AttemptCloning|System.Boolean",
        "XRL.World.Parts.VehicleRepair|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.VehicleRecall|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(TemporaryRealityStabilizeTranslationPatch), new[]
    {
        "XRL.World.Parts.Temporary|HandleEvent|System.Boolean|XRL.World.RealityStabilizeEvent",
    })]
    [TestCase(typeof(CloningStartBuddedCloneTranslationPatch), new[]
    {
        "XRL.World.Capabilities.Cloning|StartBuddedClone|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject",
    })]
    [TestCase(typeof(HiddenRenderTranslationPatch), new[]
    {
        "XRL.World.Parts.Hidden|RevealInternal|System.Void|System.Boolean",
        "XRL.World.Parts.HiddenRender|Reveal|System.Void",
    })]
    [TestCase(typeof(EngraverTranslationPatch), new[]
    {
        "XRL.World.Parts.Engraver|AttemptEngrave|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(TattooGunTranslationPatch), new[]
    {
        "XRL.World.Parts.TattooGun|AttemptTattoo|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(BrainBrineCurseTranslationPatch), new[]
    {
        "XRL.World.Effects.BrainBrineCurse|GainChoice|System.Void|System.String",
    })]
    [TestCase(typeof(CombatTextSurfaceTranslationPatch), new[]
    {
        "XRL.World.Parts.Combat|HandleEvent|System.Boolean|XRL.World.GetDefenderHitDiceEvent",
        "XRL.World.Parts.Combat|MeleeAttackWithWeaponInternal|XRL.World.Parts.MeleeAttackResult|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Anatomy.BodyPart|System.String|System.Int32|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(RepairTranslationPatch), new[]
    {
        "XRL.World.Parts.Repair|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Repair|RepairResultSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Repair|RepairResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Repair|RepairResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Repair|RepairResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Repair|RepairResultCriticalFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Tinkering_Repair|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultCriticalFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject",
    })]
    [TestCase(typeof(PlayerDanceRitualTranslationPatch), new[]
    {
        "XRL.World.Parts.PlayerDanceRitual|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.PlayerDanceRitual|ExecuteMove|System.Void|System.String|System.String",
        "XRL.World.Parts.PlayerDanceRitual|PassStep|System.Void|System.String",
        "XRL.World.Parts.PlayerDanceRitual|FailStep|System.Void|System.String",
        "XRL.World.Parts.PlayerDanceRitual|FailDance|System.Void|System.String",
        "XRL.World.Parts.PlayerDanceRitual|SuccessDance|System.Void|System.String",
    })]
    [TestCase(typeof(BeguilingSifrahTranslationPatch), new[]
    {
        "XRL.World.BeguilingSifrah|ResultCriticalFailure|System.Void|XRL.World.GameObject",
        "XRL.World.BeguilingSifrah|ResultFailure|System.Void|XRL.World.GameObject",
        "XRL.World.BeguilingSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.BeguilingSifrah|ResultSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.BeguilingSifrah|ResultExceptionalSuccess|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(ProselytizationSifrahTranslationPatch), new[]
    {
        "XRL.World.ProselytizationSifrah|ResultCriticalFailure|System.Void|XRL.World.GameObject",
        "XRL.World.ProselytizationSifrah|ResultFailure|System.Void|XRL.World.GameObject",
        "XRL.World.ProselytizationSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.ProselytizationSifrah|ResultSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.ProselytizationSifrah|ResultExceptionalSuccess|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(RebukingSifrahTranslationPatch), new[]
    {
        "XRL.World.RebukingSifrah|ResultCriticalFailure|System.Void|XRL.World.GameObject",
        "XRL.World.RebukingSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(GameObjectParticleTextTranslationPatch), new[]
    {
        "XRL.World.GameObject|ParticleText|System.Void|System.String|System.Single|System.Int32",
        "XRL.World.GameObject|ParticleText|System.Void|System.String|System.Boolean",
        "XRL.World.GameObject|ParticleText|System.Void|System.String|System.Single|System.Single|System.Char|System.Boolean",
        "XRL.World.GameObject|ParticleText|System.Void|System.String|System.Char|System.Boolean|System.Single|System.Single",
    })]
    [TestCase(typeof(JoppaZealotTranslationPatch), new[]
    {
        "XRL.World.Parts.JoppaZealot|ZealotDeclaim|System.Void|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(SixDayZealotTranslationPatch), new[]
    {
        "XRL.World.Parts.SixDayZealot|ZealotDeclaim|System.Void|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(ErosTeleportationTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.ErosTeleportation|Cast|System.Boolean|XRL.World.Parts.Mutation.ErosTeleportation|System.String|XRL.World.Event|XRL.World.Cell",
    })]
    [TestCase(typeof(PreacherHomilyTranslationPatch), new[]
    {
        "XRL.World.Parts.Preacher|PreacherHomily|System.Void|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(CanticlesChromaicParticleTextTranslationPatch), new[]
    {
        "XRL.World.SocialSifrahTokenReadFromTheCanticlesChromaic|UseToken|System.Void|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
    })]
    [TestCase(typeof(ExaminerTranslationPatch), new[]
    {
        "XRL.World.Parts.Examiner|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Examiner|ResultSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.Examiner|ResultExceptionalSuccess|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.Examiner|ResultFailure|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.Examiner|ResultFakeConfusionFailure|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.Examiner|ResultCriticalFailure|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.Examiner|ResultPartialSuccess|System.Void|XRL.World.GameObject|System.Int32",
        "XRL.World.Parts.Examiner|MakeUnderstood|System.Boolean|System.Boolean",
        "XRL.World.Parts.Examiner|MakePartiallyUnderstood|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(ItemNamingTranslationPatch), new[]
    {
        "XRL.World.Capabilities.ItemNaming|Opportunity|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean",
        "XRL.World.Capabilities.ItemNaming|CheckBestowals|System.Void|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|XRL.World.GameObject|XRL.World.GameObject|System.String|System.Boolean&|System.Int32&|System.Boolean&",
        "XRL.World.Capabilities.ItemNaming|NameItem|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.String|System.Boolean|System.Int32|System.Boolean",
        "XRL.World.Capabilities.ItemNaming|NameItem|System.Nullable`1[[System.Boolean]]|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.Boolean",
        "XRL.World.Capabilities.ItemNaming|HandleItemNamingWish|System.Boolean|System.Text.RegularExpressions.Match",
    })]
    [TestCase(typeof(TinkeringHelpersMakersMarkTranslationPatch), new[]
    {
        "XRL.World.Tinkering.TinkeringHelpers|CheckMakersMark|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Parts.IModification|System.String",
    })]
    [TestCase(typeof(SavesApiFatalSaveErrorTranslationPatch), new[]
    {
        "Qud.API.SavesAPI|FatalSaveError|System.Void|System.Exception|System.String",
    })]
    [TestCase(typeof(QudHistoryHelpersItemNameTranslationPatch), new[]
    {
        "XRL.Annals.QudHistoryHelpers|NameItem|System.String|System.String|HistoryKit.History|HistoryKit.HistoricEntity",
        "XRL.Annals.QudHistoryHelpers|NameItemNounRoot|System.String|System.String|HistoryKit.History|HistoryKit.HistoricEntity",
        "XRL.Annals.QudHistoryHelpers|NameItemAdjRoot|System.String|System.String|HistoryKit.History|HistoryKit.HistoricEntity",
    })]
    [TestCase(typeof(CookingEffectTranslationPatch), new[]
    {
        "XRL.World.Effects.ProceduralCookingEffect|GetDescription|System.String",
        "XRL.World.Effects.ProceduralCookingEffect|GetProceduralEffectDescription|System.String",
        "XRL.World.Effects.ProceduralCookingEffect|GetTemplatedProceduralEffectDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_Discharge_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_Discharge_ProceduralCookingTriggeredAction|GetTemplatedDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_EMP_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_EMP_ProceduralCookingTriggeredAction|GetTemplatedDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_OnElectricDamaged|GetTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_OnElectricDamaged|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainMedicinal_OnEatYuckwheat|GetTriggerDescription|System.String",
        "XRL.World.Effects.ProceduralCookingEffectWithTrigger|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainArmor_OnPenetration|GetTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainArmor_OnPenetration|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect|GetDetails|System.String",
        "XRL.World.Effects.CookingDomainHP_IncreaseHP_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainHP_IncreaseHP_ProceduralCookingTriggeredAction|GetTemplatedDescription|System.String",
        "XRL.World.Effects.CookingDomainHP_IncreaseHP_ProceduralCookingTriggeredActionEffect|GetDetails|System.String",
        "XRL.World.Effects.CookingDomainHP_OnDamaged|GetTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainHP_OnDamaged|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainHP_OnDamagedMidTier|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainReflect_OnDamaged|GetTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainReflect_OnDamaged|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainReflect_OnDamagedHighTier|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainRegenLowtier_OnDamaged|GetTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainRegenLowtier_OnDamaged|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainRegenHightier_OnDamaged|GetTemplatedTriggerDescription|System.String",
        "XRL.World.Effects.CookingDomainAgility_LargeAgilityBuff_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainArmor_LargeAVBuff_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainStrength_LargeStrengthBuff_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainCold_ColdResist_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainCold_LargeColdResist_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_SmallElectricResist_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainElectric_LargeElectricResist_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainHeat_HeatResist_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.CookingDomainHeat_LargeHeatResist_ProceduralCookingTriggeredAction|GetDescription|System.String",
        "XRL.World.Effects.BasicCookingEffect_Hitpoints|GetDetails|System.String",
        "XRL.World.Effects.BasicCookingEffect_MA|GetDetails|System.String",
        "XRL.World.Effects.BasicCookingEffect_MS|GetDetails|System.String",
        "XRL.World.Effects.BasicCookingEffect_Quickness|GetDetails|System.String",
        "XRL.World.Effects.BasicCookingEffect_ToHit|GetDetails|System.String",
        "XRL.World.Effects.BasicCookingEffect_XP|GetDetails|System.String",
        "XRL.World.Effects.BasicCookingEffect_Regeneration|GetDetails|System.String",
        "XRL.World.Effects.BasicCookingEffect_RandomStat|GetDetails|System.String",
        "XRL.World.Effects.BasicTriggeredCookingStatEffect|GetDetails|System.String",
        "XRL.World.Skills.Cooking.AppleMatz|GetDescription|System.String",
        "XRL.World.Skills.Cooking.BoneBabka|GetDescription|System.String",
        "XRL.World.Skills.Cooking.CloacaSurprise|GetDescription|System.String",
        "XRL.World.Skills.Cooking.CrystalDelight|GetDescription|System.String",
        "XRL.World.Skills.Cooking.GoatAndSweetLeaf|GetDescription|System.String",
        "XRL.World.Skills.Cooking.HotandSpiny|GetDescription|System.String",
        "XRL.World.Skills.Cooking.MahLahSoup|GetDescription|System.String",
        "XRL.World.Skills.Cooking.MushroomCider|GetDescription|System.String",
        "XRL.World.Skills.Cooking.ThePorridge|GetDescription|System.String",
        "XRL.World.Skills.Cooking.TongueAndCheek|GetDescription|System.String",
    })]
    [TestCase(typeof(StatusScreenTabTranslationPatch), new[]
    {
        "Qud.UI.JournalStatusScreen|GetTabString|System.String",
        "Qud.UI.MessageLogStatusScreen|GetTabString|System.String",
        "Qud.UI.QuestsStatusScreen|GetTabString|System.String",
    })]
    [TestCase(typeof(LegacyGamepadPromptTranslationPatch), new[]
    {
        "XRL.UI.InventoryScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
        "XRL.UI.StatusScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
        "XRL.UI.JournalScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
        "XRL.UI.TinkeringScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject|XRL.World.GameObject|XRL.World.IEvent",
        "XRL.UI.QuestLog|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
        "XRL.UI.FactionsScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
        "XRL.UI.SkillsAndPowersScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
        "XRL.UI.EquipmentScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
        "XRL.UI.AbilityManager|Show|System.String|XRL.World.GameObject",
    })]
    [TestCase(typeof(EquipmentScreenBodypartEquipPopupTranslationPatch), new[]
    {
        "XRL.UI.EquipmentScreen|ShowBodypartEquipUI|System.Void|XRL.World.GameObject|XRL.World.Anatomy.BodyPart",
    })]
    [TestCase(typeof(ModDisguiseBeingAppliedPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.ModDisguise|BeingAppliedBy|System.Boolean|XRL.World.GameObject|XRL.World.GameObject",
    })]
    [TestCase(typeof(StatusScreenPopupTranslationPatch), new[]
    {
        "XRL.UI.StatusScreen|BuyStat|System.Void|XRL.World.GameObject|System.String",
        "XRL.UI.StatusScreen|BuyRandomMutation|System.Boolean|XRL.World.GameObject",
        "XRL.UI.StatusScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
    })]
    [TestCase(typeof(StatusScreenMutationPopupTranslationPatch), new[]
    {
        "XRL.UI.StatusScreen|ShowMutationPopup|System.Void|XRL.World.GameObject|XRL.World.Parts.Mutation.BaseMutation",
    })]
    [TestCase(typeof(CampfirePreserveTranslationPatch), new[]
    {
        "XRL.World.Parts.Campfire|Preserve|System.Boolean",
        "XRL.World.Parts.Campfire|PreserveExotic|System.Boolean",
    })]
    [TestCase(typeof(EnclosingTranslationPatch), new[]
    {
        "XRL.World.Parts.Enclosing|EnterEnclosure|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
        "XRL.World.Parts.Enclosing|ExitEnclosure|System.Boolean|XRL.World.GameObject|XRL.World.IEvent|XRL.World.Effects.Enclosed",
        "XRL.World.Parts.Enclosing|EnclosureExitImpeded|System.Boolean|XRL.World.GameObject|System.Boolean|XRL.World.Effects.Enclosed",
    })]
    [TestCase(typeof(LiquidVolumeTranslationPatch), new[]
    {
        "XRL.World.Parts.LiquidVolume|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.LiquidVolume|Pour|System.Boolean|System.Boolean&|XRL.World.GameObject|XRL.World.Cell|System.Boolean|System.Boolean|System.Int32|System.Boolean",
        "XRL.World.Parts.LiquidVolume|PerformFill|System.Boolean|XRL.World.GameObject|System.Boolean&|System.Boolean",
    })]
    [TestCase(typeof(LiquidLeakMessageTranslationPatch), new[]
    {
        "XRL.World.Parts.LeakWhenBroken|DistributeLiquid|System.Void|XRL.World.Parts.LiquidVolume",
        "XRL.World.Parts.LeaksFluid|DistributeLiquid|System.Boolean",
    })]
    [TestCase(typeof(CombatSkillMessageTranslationPatch), new[]
    {
        "XRL.World.Parts.Skill.Tactics_Kickback|HandleEvent|System.Boolean|XRL.World.BeforeFireMissileWeaponsEvent",
        "XRL.World.Parts.Skill.Axe_Cleave|PerformCleave|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.Int32|System.Int32|System.Nullable`1[[System.Int32]]",
        "XRL.World.Parts.Skill.Endurance_ShakeItOff|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.TenfoldPath_Ret|HandleEvent|System.Boolean|XRL.World.ApplyEffectEvent",
        "XRL.World.Parts.Skill.TenfoldPath_Ret|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
        "XRL.World.Parts.Skill.Cudgel_Backswing|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.Cudgel_SmashUp|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.Discipline_IronMind|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.Rifle_DrawABead|SetMark|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Rifle_DrawABead|ValidateMark|System.Void",
        "XRL.World.Parts.Skill.Shield_Slam|Slam|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Cell|System.Boolean",
        "XRL.World.Parts.Skill.ShortBlades_Rejoinder|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(WaterRitualPopupTranslationPatch), new[]
    {
        "XRL.World.Conversations.Parts.WaterRitualBegin|HandleEvent|System.Boolean|XRL.World.Conversations.EnterElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualSkillPoint|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualTinkeringRecipe|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualBuySecret|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualBuySecret|RevealEntry|System.Void|Qud.API.IBaseJournalEntry",
        "XRL.World.Conversations.Parts.IWaterRitualPart|UseReputation|System.Boolean|System.String",
        "XRL.World.Conversations.Parts.WaterRitual|PerformRitual|System.Void",
        "XRL.World.Conversations.Parts.WaterRitualBuyItem|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualGainMutation|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualRandomMutation|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualJoinParty|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.WaterRitualNephilimPacify|TryGiveCircle|System.Boolean",
        "XRL.World.Conversations.Parts.WaterRitualSellSecret|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
    })]
    [TestCase(typeof(ConversationRewardPopupTranslationPatch), new[]
    {
        "XRL.World.Conversations.Parts.AddSlynthCandidate|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
        "XRL.World.Conversations.Parts.GiveReshephSecret|HandleEvent|System.Boolean|XRL.World.Conversations.EnterElementEvent",
        "XRL.World.Conversations.Parts.LibrarianGiveBook|HandleEvent|System.Boolean|XRL.World.Conversations.EnterElementEvent",
        "XRL.World.Conversations.Parts.PaxInfectLimb|InfectLimb|System.Boolean|System.Collections.Generic.List`1[[XRL.World.Anatomy.BodyPart]]|XRL.World.Anatomy.BodyPart|System.String",
        "XRL.World.Conversations.Parts.ReceiveItem|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
    })]
    [TestCase(typeof(ConversationCheckLostPopupTranslationPatch), new[]
    {
        "XRL.UI.ConversationUI|CheckLost|System.Void",
    })]
    [TestCase(typeof(PointOfInterestNavigationPopupTranslationPatch), new[]
    {
        "XRL.World.PointOfInterest|NavigateTo|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(RunStartRunningPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.Run|StartRunning|System.Boolean",
    })]
    [TestCase(typeof(HistoricEventRegionRevealPopupTranslationPatch), new[]
    {
        "HistoryKit.HistoricEvent|PerformRegionReveal|System.Void",
    })]
    [TestCase(typeof(RequiresPowerToEquipCheckEquipPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.RequiresPowerToEquip|CheckEquip|System.Void",
    })]
    [TestCase(typeof(SurvivalCampAttemptCampPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.Skill.Survival_Camp|AttemptCamp|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(KillMissileWeaponChirpTranslationPatch), new[]
    {
        "XRL.World.AI.GoalHandlers.Kill|TryMissileWeapon|System.Boolean",
    })]
    [TestCase(typeof(AbilityManagerShowTranslationPatch), new[]
    {
        "XRL.UI.AbilityManager|Show|System.String|XRL.World.GameObject",
    })]
    [TestCase(typeof(ActivatedAbilitiesAddAbilityPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.ActivatedAbilities|AddAbility|System.Guid|System.String|System.String|System.String|System.String|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Int32|System.String|ConsoleLib.Console.Renderable|ConsoleLib.Console.Renderable|ConsoleLib.Console.Renderable|ConsoleLib.Console.Renderable",
    })]
    [TestCase(typeof(PopupPickSeveralTranslationPatch), new[]
    {
        "XRL.UI.Popup|PickSeveral|System.Collections.Generic.List`1[[System.ValueTuple`2[[System.Int32],[System.Int32]]]]|System.String|System.String|System.String|System.String|System.Collections.Generic.IReadOnlyList`1[[System.String]]|System.Collections.Generic.IReadOnlyList`1[[System.Char]]|System.Collections.Generic.IReadOnlyList`1[[System.Int32]]|System.Collections.Generic.IReadOnlyList`1[[ConsoleLib.Console.IRenderable]]|XRL.World.GameObject|ConsoleLib.Console.IRenderable|System.Action`1[[System.Int32]]|System.Int32|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(MutationSelfTargetPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.BreatherBase|Cast|System.Boolean|XRL.World.Parts.Mutation.BreatherBase",
        "XRL.World.Parts.Mutation.FlamingRay|Cast|System.Boolean|XRL.World.Parts.Mutation.FlamingRay|System.String",
        "XRL.World.Parts.Mutation.FreezeBreath|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.FreezingRay|Cast|System.Boolean|XRL.World.Parts.Mutation.FreezingRay|System.String",
    })]
    [TestCase(typeof(GameSummaryTombstonePopupTranslationPatch), new[]
    {
        "Qud.UI.GameSummaryScreen|SaveTombstone|System.Void",
        "XRL.UI.GameSummaryUI|Show|System.Void|System.Int32|System.String|System.String|System.String|System.String|System.Boolean",
    })]
    [TestCase(typeof(PoweredFloatingTranslationPatch), new[]
    {
        "XRL.World.Parts.PoweredFloating|CheckFloating|System.Void",
    })]
    [TestCase(typeof(ConversationTakeItemPopupTranslationPatch), new[]
    {
        "XRL.World.Conversations.Parts.TakeItem|Execute|System.Boolean",
    })]
    [TestCase(typeof(MechanicalWingsPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.MechanicalWings|TryStartup|System.Boolean",
        "XRL.World.Parts.MechanicalWings|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.CyberneticsCathedra|HandleEvent|System.Boolean|XRL.World.CommandEvent",
        "XRL.World.Parts.Mutation.Wings|HandleEvent|System.Boolean|XRL.World.CommandEvent",
    })]
    [TestCase(typeof(OldSaveContinueMenuPopupTranslationPatch), new[]
    {
        "Qud.UI.MainMenu|ContinueMenu|System.Threading.Tasks.Task`1[[XRL.XRLGame]]",
        "Qud.UI.SaveManagement|ContinueMenu|System.Threading.Tasks.Task`1[[XRL.XRLGame]]",
        "XRL.Core.XRLCore|SaveManagement|XRL.XRLGame",
    })]
    [TestCase(typeof(GolemQuestSelectionPopupTranslationPatch), new[]
    {
        "XRL.World.Quests.GolemQuest.GolemBodySelection|WishSpec|System.Void|System.String",
    })]
    [TestCase(typeof(LocationFinderPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.LocationFinder|TriggerFind|System.Void",
    })]
    [TestCase(typeof(MapRevealPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.MapReveal|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.FactionDeed|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(SupplyableIntegratedHostPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.SupplyableIntegratedHost|AttemptSupply|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(AutoActTranslationPatch), new[]
    {
        "XRL.World.Capabilities.AutoAct|Interrupt|System.Void|System.String|XRL.World.Cell|XRL.World.GameObject|System.Boolean",
        "XRL.World.Capabilities.AutoAct|Interrupt|System.Void|XRL.World.GameObject|System.Boolean|System.Boolean",
        "XRL.World.Capabilities.AutoAct|ResetAutoexploreProperties|System.Boolean",
    })]
    [TestCase(typeof(ActionManagerRunSegmentTranslationPatch), new[]
    {
        "XRL.Core.ActionManager|RunSegment|System.Void",
    })]
    [TestCase(typeof(MetricsManagerLogErrorTranslationPatch), new[]
    {
        "MetricsManager|LogError|System.Void|System.String",
        "MetricsManager|LogError|System.Void|System.String|System.String",
        "MetricsManager|LogError|System.Void|System.String|System.Exception",
    })]
    [TestCase(typeof(PrefixedOwnerQueueTranslationPatch), new[]
    {
        "XRL.World.AI.GoalHandlers.Flee|TakeAction|System.Void",
        "XRL.World.Parts.Mutation.Infiltrate|performInfiltrate|System.Void|XRL.World.Cell|System.Boolean",
        "XRL.World.Parts.TemperatureController|ConfigureTemperatureController|System.Void|XRL.World.GameObject|System.Boolean",
    })]
    [TestCase(typeof(ModMagnetizedTranslationPatch), new[]
    {
        "XRL.World.Parts.ModMagnetized|CheckFloating|System.Void",
    })]
    [TestCase(typeof(DeployableInfrastructureTranslationPatch), new[]
    {
        "XRL.World.Parts.DeployableInfrastructure|AttemptDeploy|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.DeployableInfrastructure|DeployOne|System.Boolean|XRL.World.GameObject|XRL.World.Cell|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(FireSuppressionDischargeTranslationPatch), new[]
    {
        "XRL.World.Parts.FireSuppressionSystem|CheckFireSuppression|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.CyberneticsFireSuppressionSystem|TurnTick|System.Void|System.Int64|System.Int32",
    })]
    [TestCase(typeof(EffectGeneratedMessageTranslationPatch), new[]
    {
        "XRL.World.Effects.Rusted|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Asleep|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.EmptyTheClips|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Ill|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.LatchedOnto|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.LifeDrain|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
        "XRL.World.Effects.Proselytized|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Effects.Rebuked|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Effects.Running|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.ShatteredArmor|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.ShieldWall|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Effects.Stun|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
        "XRL.World.Effects.StunGasStun|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(GeneratedQueueDoesVerbTranslationPatch), new[]
    {
        "XRL.World.AI.GoalHandlers.DropOffStolenGoods|MoveToDropoff|System.Void",
        "XRL.World.AI.GoalHandlers.PaxKlanqMadness|TakeAction|System.Void",
        "XRL.World.Anatomy.BodyPart|UnequipPartAndChildren|System.Void|System.Boolean|XRL.World.IInventory|System.Boolean",
        "XRL.World.Parts.ExtradimensionalLoot|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.GelatenousPalmProperties|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.GraveMoss|Trigger|System.Void",
        "XRL.World.Parts.Garbage|AttemptRifle|System.Boolean|XRL.World.GameObject|System.Boolean|XRL.World.Cell|System.Collections.Generic.List`1[[XRL.World.GameObject]]",
        "XRL.World.Parts.QuantumRippler|HandleEvent|System.Boolean|XRL.World.RealityStabilizeEvent",
        "XRL.World.Parts.ReclamationCist|PerformReclamationOf|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(GeneratedSubjectQueueTranslationPatch), new[]
    {
        "XRL.World.Parts.HologramInvulnerability|HandleEvent|System.Boolean|XRL.World.BeforeApplyDamageEvent",
        "XRL.World.Parts.Mutation.Decarbonizer|ShutDownTargeting|System.Boolean",
        "XRL.World.Parts.PetEitherOr|trigger|System.Void",
        "XRL.World.Parts.ModPadded|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.MoteProperties|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
    })]
    [TestCase(typeof(GiantClamTeleportTranslationPatch), new[]
    {
        "XRL.World.Parts.GiantClamProperties|TeleportToClamWorld|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.GiantClamProperties|TeleportFromClamWorld|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.GiantClamProperties|TeleportJoppaWorld|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(JournalScreenPopupTranslationPatch), new[]
    {
        "XRL.UI.JournalScreen|HandleDelete|System.Boolean|System.String|Qud.API.IBaseJournalEntry|XRL.World.GameObject",
        "XRL.UI.JournalScreen|Show|XRL.UI.ScreenReturn|XRL.World.GameObject",
    })]
    [TestCase(typeof(ConversationScriptPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.ConversationScript|IsPhysicalConversationPossible|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.Boolean|System.Boolean|System.Boolean|System.Int32",
        "XRL.World.Parts.ConversationScript|IsMentalConversationPossible|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.Boolean|System.Boolean|System.Int32",
    })]
    [TestCase(typeof(InventoryFireEventTranslationPatch), new[]
    {
        "XRL.World.Parts.Inventory|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(TerrainTravelTranslationPatch), new[]
    {
        "XRL.World.Parts.TerrainTravel|HandleEvent|System.Boolean|XRL.World.ObjectEnteredCellEvent",
        "XRL.World.Parts.TerrainTravel|HandleLeavingCell|System.Boolean|XRL.World.GameObject|System.Int32&",
    })]
    [TestCase(typeof(PrecognitionTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Precognition|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Mutation.Precognition|OnBeforeDie|System.Boolean|XRL.World.GameObject|System.Guid|System.Guid|System.Int32&|System.Int32&|System.Int32&|System.Int64&|System.Boolean|System.Boolean|XRL.World.IPart",
    })]
    [TestCase(typeof(WishCommandQueueTranslationPatch), new[]
    {
        "XRL.World.Quests.LandingPadsSystem|SlynthQuestWish|System.Void|System.String",
        "XRL.World.Quests.ReclamationSystem|WishTimer|System.Void",
        "XRL.World.StatWishHandler|ClearStatShifts|System.Void",
        "XRL.World.ZoneBuilders.FindASiteDynamicQuestManager|DynamicQuestWhere|System.Boolean",
    })]
    [TestCase(typeof(BeginBeingUnequippedFailureMessageTranslationPatch), new[]
    {
        "XRL.World.BeginBeingUnequippedEvent|AddFailureMessage|System.Void|System.String",
    })]
    [TestCase(typeof(SingleCallsiteOwnerQueueTranslationPatch), new[]
    {
        "XRL.World.Parts.ActivatedAbilityEntry|TrySendCommandEventOnPlayer|System.Void",
        "XRL.World.Biomes.BiomeManager|DisplaySurfaceDistribution|System.Void|System.String",
        "XRL.World.Parts.ElevatorSwitch|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Fetches|HandleEvent|System.Boolean|XRL.World.AIBoredEvent",
        "XRL.World.Parts.ModMorphogenetic|ApplyMorphicShock|System.Boolean|XRL.World.GameObject|System.Int32|XRL.World.GameObject|System.Int32",
        "XRL.World.Effects.Monochrome|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.Persuasion_RebukeRobot|AttemptRebuke|System.Boolean",
        "XRL.World.Parts.PyroZone|Started|System.Void",
        "XRL.World.Parts.PyroZone|Stopped|System.Void",
        "XRL.World.Parts.CryoZone|Started|System.Void",
        "XRL.World.Parts.CryoZone|Stopped|System.Void",
        "XRL.World.Parts.Skill.Snapjaw_Howl|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Effects.SphynxSalt_Tonic|Apply|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.StairsDown|CheckPullDown|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.ThiefBot|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Tonic|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Quests.WeirdwireConduitSystem|HandleEvent|System.Boolean|XRL.World.TookEvent",
    })]
    [TestCase(typeof(ForceBubbleOwnerTranslationPatch), new[]
    {
        "XRL.World.Parts.ForceEmitter|ActivateForceEmitter|System.Boolean|XRL.World.IEvent",
        "XRL.World.Parts.Stopsvaalinn|ActivateStopsvalinn|System.Boolean|XRL.World.IEvent",
        "XRL.World.Parts.Mutation.ForceBubble|DestroyBubble|System.Void|System.Boolean",
    })]
    [TestCase(typeof(EelSpawnTranslationPatch), new[]
    {
        "XRL.World.Parts.EelSpawn|HandleEvent|System.Boolean|XRL.World.ObjectEnteredCellEvent",
    })]
    [TestCase(typeof(TeleporterPairTranslationPatch), new[]
    {
        "XRL.World.Parts.TeleporterPair|AttemptTeleport|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
    })]
    [TestCase(typeof(ITeleporterTranslationPatch), new[]
    {
        "XRL.World.Parts.ITeleporter|AttemptTeleport|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
    })]
    [TestCase(typeof(ShortBladesHobbleTranslationPatch), new[]
    {
        "XRL.World.Parts.Skill.ShortBlades_Hobble|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(ShortBladesShankTranslationPatch), new[]
    {
        "XRL.World.Parts.Skill.ShortBlades_Shank|Cast|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Skill.ShortBlades_Shank|XRL.World.GameObject",
    })]
    [TestCase(typeof(DecoyHologramEmitterActivateTranslationPatch), new[]
    {
        "XRL.World.Parts.DecoyHologramEmitter|ActivateHologramBracelet|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
    })]
    [TestCase(typeof(FabricateFromSelfTranslationPatch), new[]
    {
        "XRL.World.Parts.FabricateFromSelf|Activate|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(DescriptionDetailReturnTranslationPatch), new[]
    {
        "XRL.CharacterBuilds.Qud.QudCyberneticsModule+CyberneticsChoice|GetDescription|System.String",
        "XRL.CharacterBuilds.Qud.QudCyberneticsModule+CyberneticsChoice|GetLongDescription|System.String",
        "XRL.World.Tinkering.TinkerData|get_UnclippedDescription|System.String",
        "XRL.World.Tinkering.TinkerData|get_Description|System.String",
        "XRL.World.Units.GameObjectCyberneticsUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectSkillUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectRelicUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectGolemQuestRandomUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectMetachromeUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectBodyPartUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectExperienceUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectMutationUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectAttributeUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectPartUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectPlaceholderUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectSaveModifierUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectTieredArmorUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectBaetylUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectCloneUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectReputationUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectSecretUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectUnit|GetDescription|System.String|System.Boolean",
        "XRL.World.Units.GameObjectUnitAggregate|GetDescription|System.String|System.Boolean",
    })]
    [TestCase(typeof(LevelerTranslationPatch), new[]
    {
        "XRL.World.Parts.Leveler|RapidAdvancement|System.Void|System.Int32|XRL.World.GameObject",
    })]
    [TestCase(typeof(AnimateObjectTranslationPatch), new[]
    {
        "XRL.World.Parts.AnimateObject|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(RandomAltarBaetylTranslationPatch), new[]
    {
        "XRL.World.Parts.RandomAltarBaetyl|BaetylWantsSacrifice|System.Void",
    })]
    [TestCase(typeof(VehicleSeatTranslationPatch), new[]
    {
        "XRL.World.Parts.VehicleSeat|AttemptPilot|System.Boolean|XRL.World.GameObject",
    })]
    [TestCase(typeof(ActionEffectDescriptionReturnTranslationPatch), new[]
    {
        "XRL.World.AI.GoalHandlers.Kill|GetDetails|System.String",
        "XRL.World.Tinkering.Disassembly|GetDescription|System.String",
        "XRL.OngoingAction|GetDescription|System.String",
        "XRL.World.Capabilities.AutoAct|GetDescription|System.String|System.String|XRL.OngoingAction",
        "XRL.World.Parts.Mutation.Metamorphed|GetDetails|System.String",
        "XRL.World.Parts.IStingerProperties|GetDescription|System.String",
    })]
    [TestCase(typeof(StomachTranslationPatch), new[]
    {
        "XRL.World.Parts.Stomach|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(CrayonsPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.Crayons|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(CyberneticsLowLevelHackPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.CyberneticsTerminal2|AskLowLevelHack|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(CyberneticsTerminalInterfacePopupTranslationPatch), new[]
    {
        "XRL.World.Parts.CyberneticsTerminal2|AttemptInterface|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
    })]
    [TestCase(typeof(CyberneticsButcherableCyberneticTranslationPatch), new[]
    {
        "XRL.World.Parts.CyberneticsButcherableCybernetic|AttemptButcher|System.Boolean|XRL.World.GameObject|System.Boolean|System.Boolean|System.Boolean|System.Int32|XRL.World.Cell|System.Collections.Generic.List`1[[XRL.World.GameObject]]",
    })]
    [TestCase(typeof(CyberneticsOnboardRecoilerPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.CyberneticsOnboardRecoilerTeleporter|ActuateTeleport|System.Void|XRL.World.GameObject|XRL.World.IEvent",
    })]
    [TestCase(typeof(WorldPartFixedDisplayNameTranslationPatch), new[]
    {
        "XRL.World.Parts.BeyLahTerrain|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.HydroponTerrain|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.MoltingBasilisk|SyncState|System.Void",
    })]
    [TestCase(typeof(GameObjectPossessiveDisplayNameTranslationPatch), new[]
    {
        "XRL.World.GameObject|Poss|System.String|XRL.World.GameObject|System.Boolean|System.Nullable`1[[System.Boolean]]",
        "XRL.World.GameObject|poss|System.String|XRL.World.GameObject|System.Boolean|System.Nullable`1[[System.Boolean]]",
    })]
    [TestCase(typeof(MutationDisplayNameTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.BaseMutation|GetDisplayName|System.String|System.Boolean",
        "XRL.MutationEntry|GetDisplayName|System.String|System.Boolean",
    })]
    [TestCase(typeof(VillageSignatureItemTranslationPatch), new[]
    {
        "XRL.World.ZoneBuilders.VillageBase|generateSignatureItems|System.Void",
        "XRL.World.ZoneBuilders.VillageCodaBase|generateSignatureItems|System.Void",
    })]
    [TestCase(typeof(FirefightingTranslationPatch), new[]
    {
        "XRL.World.Capabilities.Firefighting|AttemptFirefightingCore|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.Int32|System.Boolean|System.Boolean",
    })]
    [TestCase(typeof(TinkerItemTranslationPatch), new[]
    {
        "XRL.World.Parts.TinkerItem|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(DescriptionLookPopupTranslationPatch), new[]
    {
        "XRL.World.Parts.Description|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
    })]
    [TestCase(typeof(ObjectFinderConfigFiltersTranslationPatch), new[]
    {
        "XRL.UI.ObjectFinder|ConfigFilters|System.Void",
    })]
    [TestCase(typeof(AutomatedExternalDefibrillatorTranslationPatch), new[]
    {
        "XRL.World.Parts.AutomatedExternalDefibrillator|AttemptDefibrillate|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
    })]
    [TestCase(typeof(DominationProcessTargetTranslationPatch), new[]
    {
        "XRL.World.Parts.Mutation.Domination|ProcessTarget|System.Boolean|XRL.World.GameObject|System.String&",
    })]
    [TestCase(typeof(CyberneticsPrecisionForceLatheTranslationPatch), new[]
    {
        "XRL.World.Parts.CyberneticsPrecisionForceLathe|ActivatePrecisionForceLathe|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.IEvent",
    })]
    [TestCase(typeof(GolemQuestMoundDisplayOptionsTranslationPatch), new[]
    {
        "XRL.World.Parts.GolemQuestMound|DisplayOptions|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(KeyMappingUiTranslationPatch), new[]
    {
        "XRL.UI.KeyMappingUI|Show|XRL.UI.ScreenReturn",
        "Qud.UI.KeybindsScreen|HandleMenuOption|System.Void|XRL.UI.Framework.FrameworkDataElement",
    })]
    [TestCase(typeof(LegacyOptionsUiTranslationPatch), new[]
    {
        "XRL.UI.OptionsUI|Show|XRL.UI.ScreenReturn",
    })]
    [TestCase(typeof(PsychicGlimmerTranslationPatch), new[]
    {
        "XRL.World.Capabilities.PsychicGlimmer|Update|System.Void|XRL.World.GameObject",
    })]
    [TestCase(typeof(HighScoresDeletePopupTranslationPatch), new[]
    {
        "Qud.UI.HighScoresScreen|HandleDelete|System.Void",
        "XRL.Core.Scores|Show|XRL.XRLGame",
    })]
    [TestCase(typeof(LegacyScoresScreenTranslationPatch), new[]
    {
        "XRL.Core.Scores|Show|XRL.XRLGame",
    })]
    [TestCase(typeof(LongBladesCoreTranslationPatch), new[]
    {
        "XRL.World.Parts.LongBladesCore|FireEvent|System.Boolean|XRL.World.Event",
    })]
    [TestCase(typeof(XrlCorePlayerTurnTranslationPatch), new[]
    {
        "XRL.Core.XRLCore|PlayerTurn|System.Void",
    })]
    [TestCase(typeof(SingleCallsiteOwnerPopupTranslationPatch), new[]
    {
        "XRL.World.Quests.AscensionSystem|BarathrumStartConversation|System.Void|XRL.World.GameObject",
        "XRL.CharacterBuilds.Qud.QudSpecificCharacterInitModule|handleBootEvent|System.Object|System.String|XRL.XRLGame|XRL.CharacterBuilds.EmbarkInfo|System.Object",
        "XRL.World.Biomes.BiomeManager|DisplaySurfaceDistribution|System.Void|System.String",
        "XRL.World.Parts.Container|AttemptOpen|System.Void|XRL.World.GameObject|XRL.World.IEvent",
        "XRL.World.Parts.DecoyHologramEmitter|CreateHolograms|XRL.World.Parts.ActivePartStatus|XRL.World.GameObject",
        "XRL.World.Parts.ElevatorSwitch|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.RandomAltarBaetyl|HandleBaetylRewardWish|System.Boolean|System.String",
        "XRL.World.Parts.Skill.Axe_Dismember|CastForceSuccess|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Skill.Axe_Dismember|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Axe_Dismember|Cast|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Skill.Axe_Dismember|XRL.World.GameObject",
        "XRL.World.Parts.Skill.Axe_HookAndDrag|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.Cudgel_Slam|Cast|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Skill.Cudgel_Slam|System.String|XRL.World.GameObject|System.Boolean|System.Int32|System.String",
        "XRL.World.Parts.Skill.Cudgel_Slam|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.Skill.Submersion|HandleEvent|System.Boolean|XRL.World.CommandEvent",
        "XRL.World.Parts.Skill.Tinkering_Tinker1|Recharge|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
        "XRL.World.DynamicQuestRewardElement_GameObject|award|System.Void",
        "XRL.World.ZoneBuilders.FactionEncounters|HandleFactionEncounterWish|System.Boolean|System.Text.RegularExpressions.Match",
        "XRL.World.Parts.Skill.Persuasion_Proselytize|AttemptProselytization|System.Boolean",
        "XRL.World.Parts.Skill.Persuasion_Proselytize|Proselytize|System.Boolean|XRL.World.MentalAttackEvent",
        "XRL.World.Parts.Skill.Tactics_DeathFromAbove|PerformDeathFromAbove|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.String",
        "XRL.World.Parts.Skill.Tactics_Charge|PerformCharge|System.Boolean",
        "XRL.World.Parts.Skill.Tactics_Juke|HandleEvent|System.Boolean|XRL.World.CommandEvent",
        "XRL.World.Parts.Skill.Tinkering|LearnNewRecipe|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
        "XRL.World.Parts.Skill.Tinkering_Tinker1|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.GameUnique|OnCreated|System.Void|System.String",
        "XRL.World.Parts.GenocideCurio|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.IGrenade|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.IZoneLandmark|WishCurrent|System.Void",
        "XRL.World.Parts.SpiralBorerCurio|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.GritGateMainframeTerminal|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.HindrenMysteryCriticalNPC|HandleEvent|System.Boolean|XRL.World.BeforeDeathRemovalEvent",
        "XRL.World.Parts.IModification|WishModify|System.Void|System.String",
        "XRL.World.Parts.KindrishProperties|ReturnAward|System.Boolean",
        "XRL.World.Parts.LiquidFueledPowerPlant|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
        "XRL.World.Parts.NeutronFluxContainment|HandleEvent|System.Boolean|XRL.World.NeutronFluxPourExplodesEvent",
        "XRL.World.Parts.NeutronFluxContainment|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
        "XRL.World.Parts.Polygel|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.UI.Look|ShowLooker|XRL.World.Cell|System.Int32|System.Int32|System.Int32",
        "XRL.World.Parts.MakeFussOnTaken|MakeFuss|System.Void|XRL.World.GameObject",
        "XRL.World.Parts.MarkovBook|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.MumblesInfection|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.MutationPointsOnEat|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Parts.EngulfingDescends|FireEvent|System.Boolean|XRL.World.Event",
        "XRL.World.Reputation|SetFactionRank|System.Void|System.String|System.String|System.Boolean|System.Boolean",
        "XRL.World.Parts.RecoilOnDeath|HandleEvent|System.Boolean|XRL.World.BeforeDieEvent",
        "XRL.World.Parts.Spraybottle|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.FixitSpray|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.AnimatorSpray|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.SummoningCurio|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Food|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.SpaceTimeVortex|ApplyVortex|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.StairsDown|CheckPullDown|System.Boolean|XRL.World.GameObject",
        "XRL.World.Parts.Physics|ProcessTargetedMove|System.Boolean|XRL.World.Cell|System.String|System.String|System.String|System.Nullable`1[[System.Int32]]|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.String|System.String|XRL.World.GameObject",
        "XRL.World.ZoneParts.ScriptCallToArms|ShowWarning|System.Void",
        "XRL.World.Parts.TrainingBook|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.WaterRitualRecord|HandleEvent|System.Boolean|XRL.World.BeginConversationEvent",
        "XRL.World.Parts.CursedCellSocket|HandleEvent|System.Boolean|XRL.World.CellChangedEvent",
        "XRL.World.QuestManagers.SpreadPax|Finish|System.Void",
        "XRL.World.Parts.Toolbox|HandleBonus|System.Boolean|XRL.World.GetTinkeringBonusEvent|System.Int32|System.Int32",
        "XRL.World.Parts.DestroyOnUnequip|HandleEvent|System.Boolean|XRL.World.BeginBeingUnequippedEvent",
        "XRL.World.Parts.MagnetizedApplicator|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        "XRL.World.Parts.Mutations|WishMutation|System.Void|System.String",
        "XRL.World.Parts.NephalProperties|HandleEvent|System.Boolean|XRL.World.BeforeDeathRemovalEvent",
        "XRL.PopulationManager|WishGenerate|System.Void|System.String",
        "XRL.PopulationManager|WishFindBlueprint|System.Void|System.String",
        "XRL.PopulationManager|RollOneFrom|XRL.PopulationResult|System.String|System.Collections.Generic.Dictionary`2[[System.String],[System.String]]|System.String",
        "XRL.World.GameObjectFactory|HandleBlueprintXML|System.Void|System.String",
        "XRL.XRLGame|LoadGame|XRL.XRLGame|System.String|System.Boolean|System.Boolean|System.Collections.Generic.Dictionary`2[[System.String],[System.Object]]",
        "XRL.World.Parts.ThinWorld|TransitToThinWorld|System.Void|XRL.World.GameObject|System.Boolean",
        "XRL.World.Parts.PlayerMuralController|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
        "XRL.World.Parts.GivesRep|HandleEvent|System.Boolean|XRL.World.BeforeDeathRemovalEvent",
    })]
    public void OwnerProducerTargetMethods_ResolveExpectedFullSignatures(Type patchType, string[] expectedSignatures)
    {
        var targetMethodsMethod = patchType.GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, $"TargetMethods not found for {patchType.FullName}");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, $"TargetMethods returned null for {patchType.FullName}");

        var actualSignatures = new List<string>();
        foreach (var item in result!)
        {
            if (item is MethodBase methodBase)
            {
                actualSignatures.Add(FullMethodSignature(methodBase));
            }
        }

        Assert.That(actualSignatures, Is.EquivalentTo(expectedSignatures));
    }

    [Test]
    public void GolemQuestSelectionPopupTargetMethods_SkipOpenGenericMaterialPick()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(GolemQuestSelectionPopupTranslationPatch));

        Assert.That(signatures, Does.Not.Contain("XRL.World.Quests.GolemQuest.GolemMaterialSelection`2|Pick|System.Void"));
    }

    [Test]
    public void TradeUiVendorPopupProducerMethods_ResolveExpectedFullSignatures()
    {
        var tradeUiType = Type.GetType("XRL.UI.TradeUI, Assembly-CSharp")
            ?? throw new InvalidOperationException("XRL.UI.TradeUI was not found.");
        var gameObjectType = Type.GetType("XRL.World.GameObject, Assembly-CSharp")
            ?? throw new InvalidOperationException("XRL.World.GameObject was not found.");
        var tradeScreenModeType = Type.GetType("XRL.UI.TradeUI+TradeScreenMode, Assembly-CSharp")
            ?? throw new InvalidOperationException("XRL.UI.TradeUI.TradeScreenMode was not found.");
        var listOfGameObjectType = typeof(List<>).MakeGenericType(gameObjectType);

        var tryRemove = tradeUiType.GetMethod(
            "TryRemove",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [gameObjectType, gameObjectType, listOfGameObjectType, listOfGameObjectType, typeof(bool)],
            null);
        var doVendorRepair = tradeUiType.GetMethod(
            "DoVendorRepair",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [gameObjectType, gameObjectType],
            null);
        var showTradeScreen = tradeUiType.GetMethod(
            "ShowTradeScreen",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [gameObjectType, typeof(float), tradeScreenModeType],
            null);
        var doVendorExamine = tradeUiType.GetMethod(
            "DoVendorExamine",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [gameObjectType, gameObjectType],
            null);
        var doVendorRecharge = tradeUiType.GetMethod(
            "DoVendorRecharge",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [gameObjectType, gameObjectType],
            null);
        var showVendorActions = tradeUiType.GetMethod(
            "ShowVendorActions",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [gameObjectType, gameObjectType, typeof(bool)],
            null);

        Assert.Multiple(() =>
        {
            Assert.That(tryRemove, Is.Not.Null, "XRL.UI.TradeUI.TryRemove signature changed.");
            Assert.That(doVendorRepair, Is.Not.Null, "XRL.UI.TradeUI.DoVendorRepair signature changed.");
            Assert.That(showTradeScreen, Is.Not.Null, "XRL.UI.TradeUI.ShowTradeScreen signature changed.");
            Assert.That(doVendorExamine, Is.Not.Null, "XRL.UI.TradeUI.DoVendorExamine signature changed.");
            Assert.That(doVendorRecharge, Is.Not.Null, "XRL.UI.TradeUI.DoVendorRecharge signature changed.");
            Assert.That(showVendorActions, Is.Not.Null, "XRL.UI.TradeUI.ShowVendorActions signature changed.");
        });

        var actualSignatures = new[]
        {
            FullMethodSignature(showTradeScreen!),
            FullMethodSignature(tryRemove!),
            FullMethodSignature(doVendorExamine!),
            FullMethodSignature(doVendorRepair!),
            FullMethodSignature(doVendorRecharge!),
            FullMethodSignature(showVendorActions!),
        };

        Assert.That(actualSignatures, Is.EquivalentTo(new[]
        {
            "XRL.UI.TradeUI|ShowTradeScreen|System.Void|XRL.World.GameObject|System.Single|XRL.UI.TradeUI+TradeScreenMode",
            "XRL.UI.TradeUI|TryRemove|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Boolean",
            "XRL.UI.TradeUI|DoVendorExamine|System.Void|XRL.World.GameObject|XRL.World.GameObject",
            "XRL.UI.TradeUI|DoVendorRepair|System.Void|XRL.World.GameObject|XRL.World.GameObject",
            "XRL.UI.TradeUI|DoVendorRecharge|System.Boolean|XRL.World.GameObject|XRL.World.GameObject",
            "XRL.UI.TradeUI|ShowVendorActions|System.String|XRL.World.GameObject|XRL.World.GameObject|System.Boolean",
        }));
    }

    [Test]
    public void MovementStateOwnerPatches_ResolveExpectedFullSignatures()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(EnclosingTranslationPatch));
        var stairsDown = InvokeTargetMethod(typeof(StairsDownTranslationPatch)) as MethodInfo;
        var stairsUp = InvokeTargetMethod(typeof(StairsUpTranslationPatch)) as MethodInfo;

        Assert.Multiple(() =>
        {
            Assert.That(signatures, Does.Contain("XRL.World.Parts.Enclosing|EnterEnclosure|System.Boolean|XRL.World.GameObject|XRL.World.IEvent"));
            Assert.That(signatures, Does.Contain("XRL.World.Parts.Enclosing|EnclosureExitImpeded|System.Boolean|XRL.World.GameObject|System.Boolean|XRL.World.Effects.Enclosed"));
            Assert.That(stairsDown, Is.Not.Null);
            Assert.That(FullMethodSignature(stairsDown!), Is.EqualTo("XRL.World.Parts.StairsDown|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent"));
            Assert.That(stairsUp, Is.Not.Null);
            Assert.That(FullMethodSignature(stairsUp!), Is.EqualTo("XRL.World.Parts.StairsUp|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent"));
        });
    }

    [Test]
    public void MovementExistingSeamProducerMethods_ResolveExpectedFullSignatures()
    {
        var physicsType = Type.GetType("XRL.World.Parts.Physics, Assembly-CSharp")
            ?? throw new InvalidOperationException("XRL.World.Parts.Physics was not found.");
        var cellType = Type.GetType("XRL.World.Cell, Assembly-CSharp")
            ?? throw new InvalidOperationException("XRL.World.Cell was not found.");
        var zoneManagerType = Type.GetType("XRL.World.ZoneManager, Assembly-CSharp")
            ?? throw new InvalidOperationException("XRL.World.ZoneManager was not found.");
        var zoneType = Type.GetType("XRL.World.Zone, Assembly-CSharp")
            ?? throw new InvalidOperationException("XRL.World.Zone was not found.");

        var physicsEnterCell = physicsType.GetMethod(
            "EnterCell",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [cellType],
            null);
        var zoneManagerSetActiveZone = zoneManagerType.GetMethod(
            "SetActiveZone",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [zoneType],
            null);

        Assert.Multiple(() =>
        {
            Assert.That(physicsEnterCell, Is.Not.Null, "XRL.World.Parts.Physics.EnterCell(Cell) signature changed.");
            Assert.That(zoneManagerSetActiveZone, Is.Not.Null, "XRL.World.ZoneManager.SetActiveZone(Zone) signature changed.");
            Assert.That(FullMethodSignature(physicsEnterCell!), Is.EqualTo("XRL.World.Parts.Physics|EnterCell|System.Boolean|XRL.World.Cell"));
            Assert.That(FullMethodSignature(zoneManagerSetActiveZone!), Is.EqualTo("XRL.World.ZoneManager|SetActiveZone|XRL.World.Zone|XRL.World.Zone"));
        });
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
            Assert.That(setDataTargets, Does.Not.Contain("Qud.UI.LeftSideCategory|setData"));
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
        "XRL.UI.Popup|ShowAsync|System.Threading.Tasks.Task|System.String|System.Boolean|System.Boolean|System.Boolean|System.Boolean|System.Boolean",
        "XRL.UI.Popup|ShowFail|System.Void|System.String|System.Boolean|System.Boolean|System.Boolean",
        "XRL.UI.Popup|ShowKeybindAsync|System.Threading.Tasks.Task|System.String|System.Threading.CancellationToken",
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
    [TestCase(typeof(PopupShowColorPickerTranslationPatch), new[]
    {
        "XRL.UI.Popup|ShowColorPicker|System.String|System.String|System.Int32|System.String|System.Int32|System.Boolean|System.Boolean|System.String|System.String|System.Boolean|System.Boolean|System.Boolean|System.String",
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
            if (item is not MethodBase methodBase)
            {
                continue;
            }

            if (methodBase is MethodInfo methodInfo)
            {
                Assert.That(
                    methodInfo.GetCustomAttribute<ObsoleteAttribute>(),
                    Is.Null,
                    $"{patchType.FullName} resolved obsolete popup method {methodInfo.DeclaringType?.FullName}.{methodInfo.Name}.");
            }

            actualSignatures.Add(FullMethodSignature(methodBase));
        }

        Assert.That(actualSignatures, Is.EquivalentTo(expectedSignatures));
    }

#if HAS_GAME_DLL
    [Test]
    public void MutationActivatedAbilityNameTargetMethods_ResolveExpectedSignatures()
    {
        var signatures = ResolveTargetMethodSignatures(typeof(MutationActivatedAbilityNameTranslationPatch));

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.World.Parts.Mutation.WillForce|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.BurrowingClaws|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.ElectricalGeneration|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.LightManipulation|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.LightManipulation|SyncAbilityName|System.Void",
            "XRL.World.Parts.Mutation.Precognition|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.SlogGlands|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Beguiling|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.AcidSlimeGlands|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.AdrenalControl2|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Burgeoning|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Burrowing|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Carapace|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Clairvoyance|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Confusion|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Decarbonizer|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.DefensiveChromatophores|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Domination|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.ElectromagneticPulse|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.ErosTeleportation|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.ForceWall|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.FreezeBreath|AddAbility|System.Void",
            "XRL.World.Parts.Mutation.FrostWebs|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Infiltrate|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.IrisdualBeam|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Kindle|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.LeyShifting|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.LifeDrain|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.LiquidSpitter|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.MassMind|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.MentalMirror|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Metamorphed|Apply|System.Boolean|XRL.World.GameObject",
            "XRL.World.Parts.Mutation.Metamorphosis|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Phasing|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Serenity|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.SpacetimeVortex|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.SpiderWebs|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Spinnerets|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.StickyTongue|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Stinger|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.StunningForce|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.SunderMind|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.TeleportOther|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.TimeDilation|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.WaveformWorm|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Cryokinesis|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Disintegration|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.FearAura|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.FlamingRay|AddAbility|System.Void",
            "XRL.World.Parts.Mutation.ForceBubble|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.FreezingRay|AddAbility|System.Void",
            "XRL.World.Parts.Mutation.MagneticPulse|AddAbility|System.Void|XRL.World.GameObject",
            "XRL.World.Parts.Mutation.Pyrokinesis|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.RepellingForce|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.SlimeGlands|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Telepathy|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Teleportation|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Belcher|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.BreatherBase|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.GasGeneration|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.IDelayedLineMutation|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.Quills|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
            "XRL.World.Parts.Mutation.TemporalFugue|Mutate|System.Boolean|XRL.World.GameObject|System.Int32",
        }));
    }
#endif

    private static string FullMethodSignature(MethodBase methodBase)
    {
        var returnType = methodBase is MethodInfo methodInfo
            ? NormalizeTypeName(methodInfo.ReturnType.FullName)
            : "System.Void";

        return string.Join(
            "|",
            new[]
            {
                methodBase.DeclaringType?.FullName ?? string.Empty,
                methodBase.Name,
                returnType,
            }.Concat(Array.ConvertAll(
                methodBase.GetParameters(),
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

        var signatures = new List<string>();
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            signatures.Add(FullMethodSignature(methodInfo));
        }

        Assert.That(signatures, Is.EquivalentTo(new[]
        {
            "XRL.CharacterBuilds.Qud.QudAttributesModule|DataWarnings|System.String",
            "XRL.CharacterBuilds.Qud.QudAttributesModule|DataErrors|System.String",
            "XRL.CharacterBuilds.Qud.QudMutationsModule|DataWarnings|System.String",
            "XRL.CharacterBuilds.Qud.QudMutationsModule|DataErrors|System.String",
            "XRL.CharacterBuilds.Qud.QudCyberneticsModule|DataErrors|System.String",
        }));
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

    [Test]
    public void PickGameObjectLineIconRoute_GameObjectRenderForUiSignatureResolves()
    {
        var assembly = EnsureGameAssemblyLoaded();
        var gameObjectType = assembly.GetType("XRL.World.GameObject", throwOnError: false);
        Assert.That(gameObjectType, Is.Not.Null, "Type not found: XRL.World.GameObject");

        var method = gameObjectType!.GetMethod(
            "RenderForUI",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(string), typeof(bool) },
            modifiers: null);

        Assert.Multiple(() =>
        {
            Assert.That(method, Is.Not.Null, "GameObject.RenderForUI(string, bool) not found.");
            Assert.That(method?.ReturnType.FullName, Is.EqualTo("XRL.World.RenderEvent"));
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

    private static HashSet<string> ResolveTargetMethodSignatures(Type patchType)
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
                signatures.Add(FullMethodSignature(methodInfo));
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
            "Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/Managed");

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
