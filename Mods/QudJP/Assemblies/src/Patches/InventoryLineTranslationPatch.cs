using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryLineTranslationPatch
{
    private const string Context = nameof(InventoryLineTranslationPatch);
    private const int MaxTranslationCacheEntries = 4096;
    private const string WeightUnit = "lbs.";
    private static readonly ConcurrentDictionary<InventoryTranslationCacheKey, string> TranslationCache = new();
    private static readonly ConcurrentQueue<InventoryTranslationCacheKey> TranslationCacheOrder = new();

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
        var displayName = go is null
            ? GetStringMemberValue(data, "displayName")
            : GetStringMemberValue(go, "DisplayName");
        var producerContext = go is null
            ? "InventoryLineData.displayName"
            : "InventoryLine.GameObjectDisplayName";
        if (displayName is null && go is not null)
        {
            displayName = GetStringMemberValue(data, "displayName");
            producerContext = "InventoryLineData.displayName";
        }
        if (displayName is null) { displayName = string.Empty; }
        var itemRoute = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translatedDisplayName = TranslateItemDisplayName(displayName, itemRoute);
        ColorShapeCaptureObservability.Record(
            itemRoute,
            producerContext,
            displayName,
            translatedDisplayName);
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
        _ = InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFontForSetData(
            instance,
            itemTextSkin,
            translatedDisplayName);
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

        return GetOrAddTranslationCache(family, source, () => TranslateVisibleTextUncached(source, route, family));
    }

    private static string TranslateVisibleTextUncached(string source, string route, string family)
    {
        var sanitizedSource = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            sanitizedSource,
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
        return GetOrAddTranslationCache("InventoryLine.ItemName", source, () =>
            TranslateItemDisplayNameUncached(source, route));
    }

    private static string TranslateItemDisplayNameUncached(string source, string route)
    {
        if (MerchantAdvertisementTextTranslator.TryTranslateBookTitle(source, out var translatedBookTitle))
        {
            translatedBookTitle = MessageFrameTranslator.StripAllDirectTranslationMarkers(translatedBookTitle);
            DynamicTextObservability.RecordTransform(
                route,
                "InventoryLine.MerchantAdvertisementTitle",
                source,
                translatedBookTitle);
            return translatedBookTitle;
        }

        var sanitizedSource = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
        var translated = TranslateVisibleTextUncached(source, route, "InventoryLine.ItemName");
        var displayNameRouteTranslation = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            sanitizedSource,
            ObservabilityHelpers.ComposeContext(route, "segment=displayName"));
        if (string.Equals(displayNameRouteTranslation, sanitizedSource, StringComparison.Ordinal)
            && !string.Equals(translated, source, StringComparison.Ordinal))
        {
            displayNameRouteTranslation = GetDisplayNameRouteTranslator.TranslatePreservingColors(
                translated,
                ObservabilityHelpers.ComposeContext(route, "segment=displayName"));
        }

        if (!string.Equals(displayNameRouteTranslation, sanitizedSource, StringComparison.Ordinal))
        {
            if (!string.Equals(displayNameRouteTranslation, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(route, "InventoryLine.ItemName", source, displayNameRouteTranslation);
            }

            translated = displayNameRouteTranslation;
        }

        return MessageFrameTranslator.StripAllDirectTranslationMarkers(translated);
    }

    internal static string TranslateItemDisplayNameForQudTest(string source)
    {
        return TranslateItemDisplayName(
            source,
            ObservabilityHelpers.ComposeContext(Context, "field=text"));
    }

    internal static void ClearTranslationCachesForTests()
    {
        TranslationCache.Clear();
        while (TranslationCacheOrder.TryDequeue(out var ignored))
        {
            _ = ignored;
        }
    }

    internal static int GetTranslationCacheCountForTests()
    {
        return TranslationCache.Count;
    }

    private static string GetOrAddTranslationCache(
        string family,
        string source,
        Func<string> translate)
    {
        var key = new InventoryTranslationCacheKey(family, source);
        if (TranslationCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var translated = translate();
        if (TranslationCache.TryAdd(key, translated))
        {
            TranslationCacheOrder.Enqueue(key);
            while (TranslationCache.Count > MaxTranslationCacheEntries
                && TranslationCacheOrder.TryDequeue(out var oldest))
            {
                TranslationCache.TryRemove(oldest, out _);
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
        return ReflectionUtils.GetPropertyOrFieldValue(instance, memberName);
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

    private readonly struct InventoryTranslationCacheKey : IEquatable<InventoryTranslationCacheKey>
    {
        internal InventoryTranslationCacheKey(string family, string source)
        {
            Family = family;
            Source = source;
        }

        internal string Family { get; }

        internal string Source { get; }

        public bool Equals(InventoryTranslationCacheKey other)
        {
            return string.Equals(Family, other.Family, StringComparison.Ordinal)
                && string.Equals(Source, other.Source, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is InventoryTranslationCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Family) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(Source);
            }
        }
    }

}
