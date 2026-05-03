using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryLineTranslationPatch
{
    private const string Context = nameof(InventoryLineTranslationPatch);
    private const string WeightUnit = "lbs.";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.InventoryLine", "InventoryLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: InventoryLineTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: InventoryLineTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static bool Prefix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return true;
            }

            var categoryMember = GetMemberValue(data, "category");
            if (categoryMember is null)
            {
                return true;
            }

            var category = Convert.ToBoolean(categoryMember, CultureInfo.InvariantCulture);
            var context = GetMemberValue(__instance, "context");
            var previousData = context is null ? null : GetMemberValue(context, "data");
            if (context is not null)
            {
                SetMemberValue(context, "data", data);
            }

            if (!ReferenceEquals(previousData, data))
            {
                _ = UITextSkinReflectionAccessor.SetCurrentText(GetMemberValue(__instance, "hotkeyText"), string.Empty, Context);
            }

            TrySetEnabled(GetMemberValue(__instance, "dotImage"), category);
            TryResetHotkey(__instance);

            if (category)
            {
                ApplyCategoryMode(__instance, data);
            }
            else
            {
                ApplyItemMode(__instance, data);
            }

            TryInvokeParameterless(__instance, "UpdateHotkey");
            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryLineTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static void ApplyCategoryMode(object instance, object data)
    {
        TrySetActive(GetMemberValue(instance, "hotkeySpacer"), active: false);
        TrySetActive(GetMemberValue(instance, "categoryMode"), active: true);
        TrySetActive(GetMemberValue(instance, "itemMode"), active: false);
        SetMemberValue(instance, "tooltipGo", null);
        SetMemberValue(instance, "tooltipCompareGo", null);

        var categoryName = GetStringMemberValue(data, "categoryName");
        if (categoryName is null) { categoryName = string.Empty; }
        var categoryRoute = ObservabilityHelpers.ComposeContext(Context, "field=categoryLabel");
        var translatedCategoryName = TranslateVisibleText(categoryName, categoryRoute, "InventoryLine.CategoryName");
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "categoryLabel"),
            categoryName,
            translatedCategoryName,
            Context,
            typeof(InventoryLineTranslationPatch));

        var categoryExpanded = GetBoolMemberValue(data, "categoryExpanded");
        var expanderText = categoryExpanded ? "[-]" : "[+]";
        _ = UITextSkinReflectionAccessor.SetCurrentText(GetMemberValue(instance, "categoryExpandLabel"), expanderText, Context);

        var amount = GetIntMemberValue(data, "categoryAmount");
        var weight = GetIntMemberValue(data, "categoryWeight");
        var showItemCount = ShouldShowNumberOfItems();
        var weightSource = showItemCount
            ? $"|{amount.ToString(CultureInfo.InvariantCulture)} items|{weight.ToString(CultureInfo.InvariantCulture)} lbs.|"
            : $"|{weight.ToString(CultureInfo.InvariantCulture)} lbs.|";
        var weightRoute = ObservabilityHelpers.ComposeContext(Context, "field=categoryWeightText");
        var translatedWeight = TranslateCategoryWeightText(weightSource, amount, weight, showItemCount, weightRoute);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "categoryWeightText"),
            weightSource,
            translatedWeight,
            Context,
            typeof(InventoryLineTranslationPatch));

        _ = UITextSkinReflectionAccessor.SetCurrentText(GetMemberValue(instance, "itemWeightText"), string.Empty, Context);
    }

    private static void ApplyItemMode(object instance, object data)
    {
        TrySetActive(GetMemberValue(instance, "hotkeySpacer"), active: true);
        TrySetActive(GetMemberValue(instance, "categoryMode"), active: false);
        TrySetActive(GetMemberValue(instance, "itemMode"), active: true);

        var go = GetMemberValue(data, "go");
        SetMemberValue(instance, "tooltipGo", go);

        _ = UITextSkinReflectionAccessor.SetCurrentText(GetMemberValue(instance, "categoryWeightText"), string.Empty, Context);

        var displayName = GetStringMemberValue(data, "displayName");
        if (displayName is null)
        {
            var displayNameTarget = go ?? data;
            displayName = GetStringMemberValue(displayNameTarget, "DisplayName");
        }
        if (displayName is null) { displayName = string.Empty; }
        var itemRoute = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translatedDisplayName = TranslateVisibleText(displayName, itemRoute, "InventoryLine.ItemName");
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "text"),
            displayName,
            translatedDisplayName,
            Context,
            typeof(InventoryLineTranslationPatch));

        var weight = go is null ? 0 : GetIntMemberValue(go, "Weight");
        var weightSource = $"[{weight.ToString(CultureInfo.InvariantCulture)} lbs.]";
        var weightRoute = ObservabilityHelpers.ComposeContext(Context, "field=itemWeightText");
        var translatedWeight = TranslateItemWeightText(weightSource, weight, weightRoute);
        OwnerTextSetter.SetTranslatedText(
            GetMemberValue(instance, "itemWeightText"),
            weightSource,
            translatedWeight,
            Context,
            typeof(InventoryLineTranslationPatch));

        var renderable = go is null ? null : InvokeMethod(go, "RenderForUI", "Inventory");
        var icon = GetMemberValue(instance, "icon");
        if (icon is not null)
        {
            _ = AccessTools.Method(icon.GetType(), "FromRenderable")?.Invoke(icon, new[] { renderable });
        }
    }

    private static string TranslateVisibleText(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var candidate)
                ? candidate
                : visible);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    private static string TranslateCategoryWeightText(string source, int amount, int weight, bool showItemCount, string route)
    {
        var translatedItems = Translator.Translate("items");
        var translated = showItemCount
            ? $"|{amount.ToString(CultureInfo.InvariantCulture)} {translatedItems}|{weight.ToString(CultureInfo.InvariantCulture)} {WeightUnit}|"
            : $"|{weight.ToString(CultureInfo.InvariantCulture)} {WeightUnit}|";
        DynamicTextObservability.RecordTransform(
            route,
            "InventoryLine.WeightSummary",
            source,
            translated,
            logWhenUnchanged: true);

        return translated;
    }

    private static string TranslateItemWeightText(string source, int weight, string route)
    {
        var translated = $"[{weight.ToString(CultureInfo.InvariantCulture)} {WeightUnit}]";
        DynamicTextObservability.RecordTransform(
            route,
            "InventoryLine.WeightLabel",
            source,
            translated,
            logWhenUnchanged: true);

        return translated;
    }

    private static bool ShouldShowNumberOfItems()
    {
        var optionsType = AccessTools.TypeByName("XRL.UI.Options");
        if (optionsType is null) { optionsType = AccessTools.TypeByName("Options"); }
        var value = optionsType is null ? null : GetStaticMemberValue(optionsType, "ShowNumberOfItems");
        return value is not bool showNumberOfItems || showNumberOfItems;
    }

    private static object? GetStaticMemberValue(Type type, string memberName)
    {
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(null);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(null);
    }

    private static void TryResetHotkey(object instance)
    {
        var field = AccessTools.Field(instance.GetType(), "hotkey");
        if (field is null)
        {
            return;
        }

        var fieldType = field.FieldType;
        var defaultValue = fieldType.IsValueType ? Activator.CreateInstance(fieldType) : null;
        field.SetValue(instance, defaultValue);
    }

    private static void TrySetActive(object? target, bool active)
    {
        if (target is null)
        {
            return;
        }

        if (AccessTools.Method(target.GetType(), "SetActive", new[] { typeof(bool) }) is MethodInfo method)
        {
            _ = method.Invoke(target, new object[] { active });
            return;
        }

        SetMemberValue(target, "activeSelf", active);
        SetMemberValue(target, "Active", active);
    }

    private static void TrySetEnabled(object? target, bool enabled)
    {
        if (target is null)
        {
            return;
        }

        SetMemberValue(target, "enabled", enabled);
        SetMemberValue(target, "Enabled", enabled);
    }

    private static void TryInvokeParameterless(object instance, string methodName)
    {
        _ = AccessTools.Method(instance.GetType(), methodName)?.Invoke(instance, null);
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[]? args)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        return method?.Invoke(instance, args);
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        return GetMemberValue(instance, memberName) as string;
    }

    private static bool GetBoolMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static int GetIntMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static void SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        field?.SetValue(instance, value);
    }
}
