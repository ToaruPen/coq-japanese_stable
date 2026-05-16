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

    public static void Postfix(object? __instance, object? data)
    {
        try
        {
            if (__instance is null || data is null)
            {
                return;
            }

            var categoryMember = GetMemberValue(data, "category");
            if (categoryMember is null)
            {
                return;
            }

            var category = Convert.ToBoolean(categoryMember, CultureInfo.InvariantCulture);
            if (category)
            {
                ApplyCategoryTranslations(__instance, data);
            }
            else
            {
                ApplyItemTranslations(__instance, data);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryLineTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void ApplyCategoryTranslations(object instance, object data)
    {
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
    }

    private static void ApplyItemTranslations(object instance, object data)
    {
        var go = GetMemberValue(data, "go");
        var displayName = GetStringMemberValue(data, "displayName");
        if (displayName is null)
        {
            var displayNameTarget = go ?? data;
            displayName = GetStringMemberValue(displayNameTarget, "DisplayName");
        }
        if (displayName is null) { displayName = string.Empty; }
        var itemRoute = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translatedDisplayName = TranslateItemDisplayName(displayName, itemRoute);
        var itemTextSkin = GetMemberValue(instance, "text");
        OwnerTextSetter.SetTranslatedText(
            itemTextSkin,
            displayName,
            translatedDisplayName,
            Context,
            typeof(InventoryLineTranslationPatch));
#if HAS_TMP && QUDJP_DEV_BUILD
        if (RuntimeDiagnostics.VerboseProbesEnabled)
        {
            InventoryLineTmpLifecycleObservability.LogOriginalTmpLifecycle(
                instance,
                "translation-after-owner-set",
                displayName,
                translatedDisplayName,
                forceMesh: false);
        }
#endif
#if HAS_TMP
        _ = InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFont(itemTextSkin, translatedDisplayName);
#endif
#if HAS_TMP && QUDJP_DEV_BUILD
        if (RuntimeDiagnostics.VerboseProbesEnabled)
        {
            InventoryLineTmpLifecycleObservability.LogOriginalTmpLifecycle(
                instance,
                "translation-after-font-refresh",
                displayName,
                translatedDisplayName,
                forceMesh: false);
        }
#endif

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

    private static string TranslateItemDisplayName(string source, string route)
    {
        var translated = TranslateVisibleText(source, route, "InventoryLine.ItemName");
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
                source,
                ObservabilityHelpers.ComposeContext(route, "segment=displayName"));
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(route, "InventoryLine.ItemName", source, translated);
            }
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

    private static int GetIntMemberValue(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

}
