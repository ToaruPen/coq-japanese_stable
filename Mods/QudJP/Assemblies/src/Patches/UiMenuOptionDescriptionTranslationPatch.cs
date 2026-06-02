using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class UiMenuOptionDescriptionTranslationPatch
{
    internal const string Context = nameof(UiMenuOptionDescriptionTranslationPatch);
    internal const string FactionsFamily = "Qud.UI.FactionsStatusScreen.ShowScreen.MenuOptionDescription";
    internal const string HighScoresFamily = "Qud.UI.HighScoresScreen.UpdateMenuBars.MenuOptionDescription";
    internal const string KeybindsFamily = "Qud.UI.KeybindsScreen.UpdateMenuBars.MenuOptionDescription";
    internal const string CharacterAttributeLineFamily = "Qud.UI.CharacterAttributeLine.StaticMenuOptionDescription";
    internal const string AskNumberFamily = "Qud.UI.AskNumberScreen.StaticMenuOptionDescription";
    internal const string SaveManagementFamily = "Qud.UI.SaveManagement.UpdateMenuBars.MenuOptionDescription";
    internal const string CharacterEffectLineFamily = "Qud.UI.CharacterEffectLine.StaticMenuOptionDescription";
    internal const string CharacterMutationLineFamily = "Qud.UI.CharacterMutationLine.StaticMenuOptionDescription";
    internal const string EquipmentLineFamily = "Qud.UI.EquipmentLine.StaticMenuOptionDescription";
    internal const string ButtonBarButtonFamily = "Qud.UI.ButtonBarButton.StaticMenuOptionDescription";
    internal const string FactionsLineFamily = "Qud.UI.FactionsLine.StaticMenuOptionDescription";
    internal const string InventoryLineFamily = "Qud.UI.InventoryLine.StaticMenuOptionDescription";
    internal const string JournalSultanStatueLineFamily = "Qud.UI.JournalSultanStatueLine.StaticMenuOptionDescription";
    internal const string SkillsAndPowersLineFamily = "Qud.UI.SkillsAndPowersLine.StaticMenuOptionDescription";
    internal const string TinkeringBitsLineFamily = "Qud.UI.TinkeringBitsLine.StaticMenuOptionDescription";
    internal const string TinkeringDetailsLineFamily = "Qud.UI.TinkeringDetailsLine.StaticMenuOptionDescription";
    internal const string TinkeringLineFamily = "Qud.UI.TinkeringLine.StaticMenuOptionDescription";
    internal const string TradeLineFamily = "Qud.UI.TradeLine.StaticMenuOptionDescription";
    internal const string OptionsCategoryControlFamily = "Qud.UI.OptionsCategoryControl.StaticMenuOptionDescription";
    internal const string OptionsCheckboxControlFamily = "Qud.UI.OptionsCheckboxControl.StaticMenuOptionDescription";
    internal const string OptionsSliderControlFamily = "Qud.UI.OptionsSliderControl.StaticMenuOptionDescription";
    internal const string OptionsComboBoxControlFamily = "Qud.UI.OptionsComboBoxControl.Render.DisplayOptionDescription";

    private const string OptionsDictionaryFile = "ui-options.ja.json";

    private static readonly HashSet<string> FactionsDescriptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "Expand All",
        "Collapse All",
        "Sort Options",
        "Filter",
    };

    private static readonly HashSet<string> NavigationDescriptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "Accept",
        "Cancel",
        "navigate",
        "select",
    };

    private static readonly HashSet<string> CharacterAttributeLineDescriptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "Expand",
        "Collapse",
    };

    private static readonly HashSet<string> SelectDescriptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "Select",
    };

    private static readonly HashSet<string> ExpandDescriptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "Expand",
    };

    private static readonly OwnerDispatch[] OwnerDispatches =
    {
        new OwnerDispatch(
            "Qud.UI.FactionsStatusScreen",
            "DummyFactionsStatusScreenTarget",
            (_, instance) => TranslateFactionsMenuOptions(instance)),
        new OwnerDispatch(
            "Qud.UI.HighScoresScreen",
            "DummyHighScoresScreenTarget",
            (_, instance) => TranslateHotkeyBarChoices(instance, HighScoresFamily, NavigationDescriptions)),
        new OwnerDispatch(
            "Qud.UI.KeybindsScreen",
            "DummyKeybindsScreenTarget",
            (_, instance) => TranslateHotkeyBarChoices(instance, KeybindsFamily, NavigationDescriptions)),
        new OwnerDispatch(
            "Qud.UI.AskNumberScreen",
            "DummyAskNumberScreenTarget",
            (ownerType, _) => TranslateStaticCollectionField(ownerType, "getItemMenuOptions", AskNumberFamily, NavigationDescriptions)),
        new OwnerDispatch(
            "Qud.UI.SaveManagement",
            "DummySaveManagementTarget",
            (_, instance) => TranslateHotkeyBarChoices(instance, SaveManagementFamily, NavigationDescriptions)),
        new OwnerDispatch(
            "Qud.UI.CharacterAttributeLine",
            "DummyCharacterAttributeLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, CharacterAttributeLineFamily)),
        new OwnerDispatch(
            "Qud.UI.CharacterEffectLine",
            "DummyCharacterEffectLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, CharacterEffectLineFamily)),
        new OwnerDispatch(
            "Qud.UI.CharacterMutationLine",
            "DummyCharacterMutationLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, CharacterMutationLineFamily)),
        new OwnerDispatch(
            "Qud.UI.EquipmentLine",
            "DummyEquipmentLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, EquipmentLineFamily)),
        new OwnerDispatch(
            "Qud.UI.ButtonBarButton",
            "DummyButtonBarButtonTarget",
            (ownerType, _) => TranslateStaticCollectionField(ownerType, "itemOptions", ButtonBarButtonFamily, SelectDescriptions)),
        new OwnerDispatch(
            "Qud.UI.FactionsLine",
            "DummyFactionsLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, FactionsLineFamily)),
        new OwnerDispatch(
            "Qud.UI.InventoryLine",
            "DummyInventoryLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, InventoryLineFamily)),
        new OwnerDispatch(
            "Qud.UI.JournalSultanStatueLine",
            "DummyJournalSultanStatueLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, JournalSultanStatueLineFamily)),
        new OwnerDispatch(
            "Qud.UI.SkillsAndPowersLine",
            "DummySkillsAndPowersLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, SkillsAndPowersLineFamily)),
        new OwnerDispatch(
            "Qud.UI.TinkeringBitsLine",
            "DummyTinkeringBitsLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, TinkeringBitsLineFamily)),
        new OwnerDispatch(
            "Qud.UI.TinkeringDetailsLine",
            "DummyTinkeringDetailsLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, TinkeringDetailsLineFamily)),
        new OwnerDispatch(
            "Qud.UI.TinkeringLine",
            "DummyTinkeringLineTarget",
            (ownerType, _) => TranslateExpandCollapseStaticCollectionFields(ownerType, TinkeringLineFamily)),
        new OwnerDispatch(
            "Qud.UI.TradeLine",
            "DummyTradeLineTarget",
            (ownerType, _) => TranslateTradeLineStaticFields(ownerType)),
        new OwnerDispatch(
            "Qud.UI.OptionsCategoryControl",
            "DummyOptionsCategoryControlTarget",
            (ownerType, _) => TranslateStaticMenuOptionField(
                ownerType,
                "TOGGLE_OPTION",
                OptionsCategoryControlFamily,
                "Qud.UI.OptionsCategoryControl.TOGGLE_OPTION.Description")),
        new OwnerDispatch(
            "Qud.UI.OptionsCheckboxControl",
            "DummyOptionsCheckboxControlTarget",
            (ownerType, _) => TranslateStaticMenuOptionField(
                ownerType,
                "TOGGLE_OPTION",
                OptionsCheckboxControlFamily,
                "Qud.UI.OptionsCheckboxControl.TOGGLE_OPTION.Description")),
        new OwnerDispatch(
            "Qud.UI.OptionsSliderControl",
            "DummyOptionsSliderControlTarget",
            (ownerType, _) => TranslateOptionsSliderControlFields(ownerType)),
        new OwnerDispatch(
            "Qud.UI.OptionsComboBoxControl",
            "DummyOptionsComboBoxControlTarget",
            (_, instance) => TranslateOptionsComboBoxRenderOptions(instance)),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var factionsType = GameTypeResolver.FindType("Qud.UI.FactionsStatusScreen", "FactionsStatusScreen");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var statusScreensScreenType = GameTypeResolver.FindType("Qud.UI.StatusScreensScreen", "StatusScreensScreen");
        var factionsShowScreen = ResolveMethod(
            factionsType,
            "ShowScreen",
            gameObjectType is null || statusScreensScreenType is null
                ? null
                : new[] { gameObjectType, statusScreensScreenType });
        if (factionsShowScreen is not null)
        {
            yield return factionsShowScreen;
        }

        var highScoresType = GameTypeResolver.FindType("Qud.UI.HighScoresScreen", "HighScoresScreen");
        var highScoresUpdateMenuBars = ResolveMethod(highScoresType, "UpdateMenuBars", Type.EmptyTypes);
        if (highScoresUpdateMenuBars is not null)
        {
            yield return highScoresUpdateMenuBars;
        }

        var keybindsType = GameTypeResolver.FindType("Qud.UI.KeybindsScreen", "KeybindsScreen");
        var keybindsUpdateMenuBars = ResolveMethod(keybindsType, "UpdateMenuBars", Type.EmptyTypes);
        if (keybindsUpdateMenuBars is not null)
        {
            yield return keybindsUpdateMenuBars;
        }

        var askNumberType = GameTypeResolver.FindType("Qud.UI.AskNumberScreen", "AskNumberScreen");
        var askNumberSetupContext = ResolveMethod(askNumberType, "SetupContext", Type.EmptyTypes);
        if (askNumberSetupContext is not null)
        {
            yield return askNumberSetupContext;
        }

        var saveManagementType = GameTypeResolver.FindType("Qud.UI.SaveManagement", "SaveManagement");
        var saveManagementUpdateMenuBars = ResolveMethod(saveManagementType, "UpdateMenuBars", Type.EmptyTypes);
        if (saveManagementUpdateMenuBars is not null)
        {
            yield return saveManagementUpdateMenuBars;
        }

        var characterAttributeLineType = GameTypeResolver.FindType("Qud.UI.CharacterAttributeLine", "CharacterAttributeLine");
        var scrollChildContextType = AccessTools.TypeByName("XRL.UI.Framework.ScrollChildContext");
        var frameworkDataElementType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement");
        var setupContexts = ResolveMethod(
            characterAttributeLineType,
            "SetupContexts",
            scrollChildContextType is null ? null : new[] { scrollChildContextType });
        if (setupContexts is not null)
        {
            yield return setupContexts;
        }

        foreach (var lineTypeName in new[]
                 {
                     "Qud.UI.CharacterEffectLine",
                     "Qud.UI.CharacterMutationLine",
                     "Qud.UI.EquipmentLine",
                 })
        {
            var simpleName = lineTypeName.Substring(lineTypeName.LastIndexOf('.') + 1);
            var lineType = GameTypeResolver.FindType(lineTypeName, simpleName);
            var lineSetupContexts = ResolveMethod(
                lineType,
                "SetupContexts",
                scrollChildContextType is null ? null : new[] { scrollChildContextType });
            if (lineSetupContexts is not null)
            {
                yield return lineSetupContexts;
            }
        }

        var buttonBarButtonType = GameTypeResolver.FindType("Qud.UI.ButtonBarButton", "ButtonBarButton");
        var buttonBarButtonSetData = ResolveMethod(
            buttonBarButtonType,
            "setData",
            frameworkDataElementType is null ? null : new[] { frameworkDataElementType });
        if (buttonBarButtonSetData is not null)
        {
            yield return buttonBarButtonSetData;
        }

        foreach (var lineTypeName in new[]
                 {
                     "Qud.UI.FactionsLine",
                     "Qud.UI.InventoryLine",
                     "Qud.UI.JournalSultanStatueLine",
                     "Qud.UI.SkillsAndPowersLine",
                     "Qud.UI.TinkeringBitsLine",
                     "Qud.UI.TinkeringDetailsLine",
                     "Qud.UI.TinkeringLine",
                     "Qud.UI.TradeLine",
                 })
        {
            var simpleName = lineTypeName.Substring(lineTypeName.LastIndexOf('.') + 1);
            var lineType = GameTypeResolver.FindType(lineTypeName, simpleName);
            var lineSetupContexts = ResolveMethod(
                lineType,
                "SetupContexts",
                scrollChildContextType is null ? null : new[] { scrollChildContextType });
            if (lineSetupContexts is not null)
            {
                yield return lineSetupContexts;
            }
        }

        foreach (var controlTypeName in new[]
                 {
                     "Qud.UI.OptionsCategoryControl",
                     "Qud.UI.OptionsCheckboxControl",
                     "Qud.UI.OptionsSliderControl",
                 })
        {
            var simpleName = controlTypeName.Substring(controlTypeName.LastIndexOf('.') + 1);
            var controlType = GameTypeResolver.FindType(controlTypeName, simpleName);
            var controlSetupContexts = ResolveMethod(
                controlType,
                "SetupContexts",
                scrollChildContextType is null ? null : new[] { scrollChildContextType });
            if (controlSetupContexts is not null)
            {
                yield return controlSetupContexts;
            }
        }

        var optionsComboBoxControlType = GameTypeResolver.FindType(
            "Qud.UI.OptionsComboBoxControl",
            "OptionsComboBoxControl");
        var optionsComboBoxRender = ResolveMethod(optionsComboBoxControlType, "Render", Type.EmptyTypes);
        if (optionsComboBoxRender is not null)
        {
            yield return optionsComboBoxRender;
        }
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var instanceType = __instance.GetType();
            DispatchOwner(instanceType, __instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: UiMenuOptionDescriptionTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static MethodBase? ResolveMethod(Type? targetType, string methodName, Type[]? parameterTypes)
    {
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0}: target type not found for method '{1}'.", Context, methodName);
            return null;
        }

        var method = parameterTypes is null
            ? AccessTools.Method(targetType, methodName)
            : AccessTools.Method(targetType, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}: method '{1}.{2}' not found.", Context, targetType.FullName, methodName);
        }

        return method;
    }

    private static void DispatchOwner(Type instanceType, object instance)
    {
        foreach (var dispatch in OwnerDispatches)
        {
            if (!dispatch.Matches(instanceType))
            {
                continue;
            }

            dispatch.Translate(instanceType, instance);
            return;
        }
    }

    private static void TranslateFactionsMenuOptions(object instance)
    {
        var context = UiBindingTranslationHelpers.GetMemberValue(instance, "context");
        if (context is null)
        {
            return;
        }

        var menuOptionDescriptions = UiBindingTranslationHelpers.GetMemberValue(context, "menuOptionDescriptions");
        TranslateCollection(menuOptionDescriptions, FactionsFamily, FactionsDescriptions);
    }

    private static void TranslateHotkeyBarChoices(
        object instance,
        string family,
        ISet<string> supportedDescriptions)
    {
        var hotkeyBar = UiBindingTranslationHelpers.GetMemberValue(instance, "hotkeyBar");
        if (hotkeyBar is null)
        {
            hotkeyBar = UiBindingTranslationHelpers.GetMemberValue(instance, "HotkeyBar");
        }
        if (hotkeyBar is null)
        {
            return;
        }

        var choices = UiBindingTranslationHelpers.GetMemberValue(hotkeyBar, "choices");
        TranslateCollection(choices, family, supportedDescriptions);
    }

    private static void TranslateStaticCollectionField(
        Type ownerType,
        string fieldName,
        string family,
        ISet<string> supportedDescriptions)
    {
        var field = AccessTools.Field(ownerType, fieldName);
        if (field is null)
        {
            return;
        }

        TranslateCollection(field.GetValue(null), family, supportedDescriptions);
    }

    private static void TranslateExpandCollapseStaticCollectionFields(Type ownerType, string family)
    {
        TranslateStaticCollectionField(ownerType, "categoryExpandOptions", family, CharacterAttributeLineDescriptions);
        TranslateStaticCollectionField(ownerType, "categoryCollapseOptions", family, CharacterAttributeLineDescriptions);
    }

    private static void TranslateTradeLineStaticFields(Type ownerType)
    {
        TranslateStaticCollectionField(ownerType, "categoryExpandOptions", TradeLineFamily, ExpandDescriptions);
        TranslateStaticCollectionField(ownerType, "categoryCollapseOptions", TradeLineFamily, CharacterAttributeLineDescriptions);
        TranslateStaticCollectionField(ownerType, "itemOptions", TradeLineFamily, SelectDescriptions);
    }

    private static void TranslateOptionsSliderControlFields(Type ownerType)
    {
        TranslateStaticMenuOptionField(
            ownerType,
            "CHANGE_VALUE",
            OptionsSliderControlFamily,
            "Qud.UI.OptionsSliderControl.CHANGE_VALUE.Description");
        TranslateStaticMenuOptionField(
            ownerType,
            "ARROWS_CHANGE_VALUE",
            OptionsSliderControlFamily,
            "Qud.UI.OptionsSliderControl.ARROWS_CHANGE_VALUE.Description");
        TranslateStaticMenuOptionField(
            ownerType,
            "SAVE_VALUE",
            OptionsSliderControlFamily,
            "Qud.UI.OptionsSliderControl.SAVE_VALUE.Description");
        TranslateStaticMenuOptionField(
            ownerType,
            "CANCEL_VALUE",
            OptionsSliderControlFamily,
            "Qud.UI.OptionsSliderControl.CANCEL_VALUE.Description");
    }

    private static void TranslateStaticMenuOptionField(
        Type ownerType,
        string fieldName,
        string family,
        string context)
    {
        var field = AccessTools.Field(ownerType, fieldName);
        var menuOption = field?.GetValue(null);
        if (menuOption is null)
        {
            return;
        }

        TranslateDescriptionWithContext(menuOption, family, context, fieldName);
    }

    private static void TranslateOptionsComboBoxRenderOptions(object instance)
    {
        var renderOptions = UiBindingTranslationHelpers.GetMemberValue(instance, "RenderOptions");
        if (renderOptions is null || renderOptions is string || renderOptions is not IEnumerable collection)
        {
            return;
        }

        var index = 0;
        foreach (var item in collection)
        {
            TranslateDescription(
                item,
                OptionsComboBoxControlFamily,
                "Qud.UI.OptionsComboBoxControl.RenderOptions[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
            index++;
        }
    }

    private static void TranslateCollection(
        object? maybeCollection,
        string family,
        ISet<string> supportedDescriptions)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable collection)
        {
            return;
        }

        var index = 0;
        foreach (var item in collection)
        {
            TranslateDescription(item, family, supportedDescriptions, index);
            index++;
        }
    }

    private static void TranslateDescription(
        object? menuOption,
        string family,
        ISet<string> supportedDescriptions,
        int index)
    {
        if (menuOption is null)
        {
            return;
        }

        var current = UiBindingTranslationHelpers.GetStringMemberValue(menuOption, "Description");
        if (string.IsNullOrEmpty(current) || !supportedDescriptions.Contains(current!))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "family=" + family + " > field=Description[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
        var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, family);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(menuOption, "Description", translated);
        }
    }

    private static void TranslateDescription(object? menuOption, string family, string routeSuffix)
    {
        if (menuOption is null)
        {
            return;
        }

        var current = UiBindingTranslationHelpers.GetStringMemberValue(menuOption, "Description");
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, routeSuffix + " > field=Description");
        var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, family);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(menuOption, "Description", translated);
        }
    }

    private static void TranslateDescriptionWithContext(
        object menuOption,
        string family,
        string context,
        string routeSuffix)
    {
        var current = UiBindingTranslationHelpers.GetStringMemberValue(menuOption, "Description");
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, routeSuffix + " > field=Description");
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            current!,
            visible => TranslateVisibleDescriptionWithFallback(visible, context));
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(route, family, current!, translated);
        UiBindingTranslationHelpers.SetMemberValue(menuOption, "Description", translated);
    }

    private static string TranslateVisibleDescriptionWithFallback(string visible, string context)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            visible,
            context,
            OptionsDictionaryFile);
        if (scoped is not null)
        {
            return scoped;
        }

        Trace.TraceWarning(
            "QudJP: {0} scoped option description lookup missed for context '{1}'; falling back to global ASCII lookup.",
            Context,
            context);
        var global = StringHelpers.TranslateExactOrLowerAscii(visible);
        if (global is not null)
        {
            return global;
        }

        Trace.TraceWarning(
            "QudJP: {0} global ASCII lookup missed for option description '{1}'; preserving source text.",
            Context,
            visible);
        return visible;
    }

    private readonly struct OwnerDispatch
    {
        private readonly string fullName;
        private readonly string testName;
        private readonly Action<Type, object> translate;

        public OwnerDispatch(string fullName, string testName, Action<Type, object> translate)
        {
            this.fullName = fullName;
            this.testName = testName;
            this.translate = translate;
        }

        public bool Matches(Type type)
        {
            return string.Equals(type.FullName, fullName, StringComparison.Ordinal)
                || string.Equals(type.Name, testName, StringComparison.Ordinal);
        }

        public void Translate(Type ownerType, object instance)
        {
            translate(ownerType, instance);
        }
    }
}
