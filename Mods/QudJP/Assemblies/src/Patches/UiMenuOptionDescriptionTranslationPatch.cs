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
            if (IsType(instanceType, "Qud.UI.FactionsStatusScreen", "DummyFactionsStatusScreenTarget"))
            {
                TranslateFactionsMenuOptions(__instance);
                return;
            }

            if (IsType(instanceType, "Qud.UI.HighScoresScreen", "DummyHighScoresScreenTarget"))
            {
                TranslateHotkeyBarChoices(__instance, HighScoresFamily, NavigationDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.KeybindsScreen", "DummyKeybindsScreenTarget"))
            {
                TranslateHotkeyBarChoices(__instance, KeybindsFamily, NavigationDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.AskNumberScreen", "DummyAskNumberScreenTarget"))
            {
                TranslateStaticCollectionField(instanceType, "getItemMenuOptions", AskNumberFamily, NavigationDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.SaveManagement", "DummySaveManagementTarget"))
            {
                TranslateHotkeyBarChoices(__instance, SaveManagementFamily, NavigationDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.CharacterAttributeLine", "DummyCharacterAttributeLineTarget"))
            {
                TranslateStaticCollectionField(instanceType, "categoryExpandOptions", CharacterAttributeLineFamily, CharacterAttributeLineDescriptions);
                TranslateStaticCollectionField(instanceType, "categoryCollapseOptions", CharacterAttributeLineFamily, CharacterAttributeLineDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.CharacterEffectLine", "DummyCharacterEffectLineTarget"))
            {
                TranslateStaticCollectionField(instanceType, "categoryExpandOptions", CharacterEffectLineFamily, CharacterAttributeLineDescriptions);
                TranslateStaticCollectionField(instanceType, "categoryCollapseOptions", CharacterEffectLineFamily, CharacterAttributeLineDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.CharacterMutationLine", "DummyCharacterMutationLineTarget"))
            {
                TranslateStaticCollectionField(instanceType, "categoryExpandOptions", CharacterMutationLineFamily, CharacterAttributeLineDescriptions);
                TranslateStaticCollectionField(instanceType, "categoryCollapseOptions", CharacterMutationLineFamily, CharacterAttributeLineDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.EquipmentLine", "DummyEquipmentLineTarget"))
            {
                TranslateStaticCollectionField(instanceType, "categoryExpandOptions", EquipmentLineFamily, CharacterAttributeLineDescriptions);
                TranslateStaticCollectionField(instanceType, "categoryCollapseOptions", EquipmentLineFamily, CharacterAttributeLineDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.ButtonBarButton", "DummyButtonBarButtonTarget"))
            {
                TranslateStaticCollectionField(instanceType, "itemOptions", ButtonBarButtonFamily, SelectDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.FactionsLine", "DummyFactionsLineTarget"))
            {
                TranslateExpandCollapseStaticCollectionFields(instanceType, FactionsLineFamily);
                return;
            }

            if (IsType(instanceType, "Qud.UI.InventoryLine", "DummyInventoryLineTarget"))
            {
                TranslateExpandCollapseStaticCollectionFields(instanceType, InventoryLineFamily);
                return;
            }

            if (IsType(instanceType, "Qud.UI.JournalSultanStatueLine", "DummyJournalSultanStatueLineTarget"))
            {
                TranslateExpandCollapseStaticCollectionFields(instanceType, JournalSultanStatueLineFamily);
                return;
            }

            if (IsType(instanceType, "Qud.UI.SkillsAndPowersLine", "DummySkillsAndPowersLineTarget"))
            {
                TranslateExpandCollapseStaticCollectionFields(instanceType, SkillsAndPowersLineFamily);
                return;
            }

            if (IsType(instanceType, "Qud.UI.TinkeringBitsLine", "DummyTinkeringBitsLineTarget"))
            {
                TranslateExpandCollapseStaticCollectionFields(instanceType, TinkeringBitsLineFamily);
                return;
            }

            if (IsType(instanceType, "Qud.UI.TinkeringDetailsLine", "DummyTinkeringDetailsLineTarget"))
            {
                TranslateExpandCollapseStaticCollectionFields(instanceType, TinkeringDetailsLineFamily);
                return;
            }

            if (IsType(instanceType, "Qud.UI.TinkeringLine", "DummyTinkeringLineTarget"))
            {
                TranslateExpandCollapseStaticCollectionFields(instanceType, TinkeringLineFamily);
                return;
            }

            if (IsType(instanceType, "Qud.UI.TradeLine", "DummyTradeLineTarget"))
            {
                TranslateStaticCollectionField(instanceType, "categoryExpandOptions", TradeLineFamily, ExpandDescriptions);
                TranslateStaticCollectionField(instanceType, "categoryCollapseOptions", TradeLineFamily, CharacterAttributeLineDescriptions);
                TranslateStaticCollectionField(instanceType, "itemOptions", TradeLineFamily, SelectDescriptions);
                return;
            }

            if (IsType(instanceType, "Qud.UI.OptionsCategoryControl", "DummyOptionsCategoryControlTarget"))
            {
                TranslateStaticMenuOptionField(
                    instanceType,
                    "TOGGLE_OPTION",
                    OptionsCategoryControlFamily,
                    "Qud.UI.OptionsCategoryControl.TOGGLE_OPTION.Description");
                return;
            }

            if (IsType(instanceType, "Qud.UI.OptionsCheckboxControl", "DummyOptionsCheckboxControlTarget"))
            {
                TranslateStaticMenuOptionField(
                    instanceType,
                    "TOGGLE_OPTION",
                    OptionsCheckboxControlFamily,
                    "Qud.UI.OptionsCheckboxControl.TOGGLE_OPTION.Description");
                return;
            }

            if (IsType(instanceType, "Qud.UI.OptionsSliderControl", "DummyOptionsSliderControlTarget"))
            {
                TranslateStaticMenuOptionField(
                    instanceType,
                    "CHANGE_VALUE",
                    OptionsSliderControlFamily,
                    "Qud.UI.OptionsSliderControl.CHANGE_VALUE.Description");
                TranslateStaticMenuOptionField(
                    instanceType,
                    "ARROWS_CHANGE_VALUE",
                    OptionsSliderControlFamily,
                    "Qud.UI.OptionsSliderControl.ARROWS_CHANGE_VALUE.Description");
                TranslateStaticMenuOptionField(
                    instanceType,
                    "SAVE_VALUE",
                    OptionsSliderControlFamily,
                    "Qud.UI.OptionsSliderControl.SAVE_VALUE.Description");
                TranslateStaticMenuOptionField(
                    instanceType,
                    "CANCEL_VALUE",
                    OptionsSliderControlFamily,
                    "Qud.UI.OptionsSliderControl.CANCEL_VALUE.Description");
                return;
            }

            if (IsType(instanceType, "Qud.UI.OptionsComboBoxControl", "DummyOptionsComboBoxControlTarget"))
            {
                TranslateOptionsComboBoxRenderOptions(__instance);
            }
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

    private static bool IsType(Type type, string fullName, string testName)
    {
        return string.Equals(type.FullName, fullName, StringComparison.Ordinal)
            || string.Equals(type.Name, testName, StringComparison.Ordinal);
    }
}
