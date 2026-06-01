using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CookingRecipeDisplayNameTranslationPatch
{
    internal const string Context = nameof(CookingRecipeDisplayNameTranslationPatch);
    internal const string Family = Context + ".HistoricSpiceGeneratedName";

    private const string PresetMealNameDictionaryFile = "Scoped/ui-popup-campfire-preset-meals.ja.json";
    private const string MarkupSuffix = "}}";

    [ThreadStatic]
    private static int generateRecipeTileSuppressionDepth;

    internal static bool IsGenerateRecipeTileSuppressed => generateRecipeTileSuppressionDepth > 0;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var typeName in new[]
                 {
                     "XRL.World.Skills.Cooking.CookingRecipe",
                     "XRL.World.Skills.Cooking.AppleMatz",
                     "XRL.World.Skills.Cooking.BoneBabka",
                     "XRL.World.Skills.Cooking.CloacaSurprise",
                     "XRL.World.Skills.Cooking.CrystalDelight",
                     "XRL.World.Skills.Cooking.GoatAndSweetLeaf",
                     "XRL.World.Skills.Cooking.HotandSpiny",
                     "XRL.World.Skills.Cooking.MahLahSoup",
                     "XRL.World.Skills.Cooking.MushroomCider",
                     "XRL.World.Skills.Cooking.ThePorridge",
                     "XRL.World.Skills.Cooking.TongueAndCheek",
                 })
        {
            var targetType = AccessTools.TypeByName(typeName);
            if (targetType is null)
            {
                Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
                continue;
            }

            var method = AccessTools.Method(targetType, "GetDisplayName", Type.EmptyTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}.GetDisplayName() target not found.", Context, typeName);
            }

        }
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (IsGenerateRecipeTileSuppressed)
            {
                return;
            }

            var source = __result;
            if (!TryProcessDisplayName(source, out var translated, out var actualTranslation))
            {
                return;
            }

            if (actualTranslation)
            {
                DynamicTextObservability.RecordTransform(Context, Family, source, translated);
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void EnterGenerateRecipeTileScope(out int previousDepth)
    {
        previousDepth = generateRecipeTileSuppressionDepth;
        generateRecipeTileSuppressionDepth++;
    }

    internal static void ExitGenerateRecipeTileScope(int previousDepth)
    {
        generateRecipeTileSuppressionDepth = previousDepth;
    }

    internal static bool TryProcessDisplayName(
        string? source,
        out string translated,
        out bool actualTranslation)
    {
        actualTranslation = false;
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        var nonNullSource = source!;
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(nonNullSource, out var withoutMarker))
        {
            translated = withoutMarker;
            return !string.Equals(nonNullSource, translated, StringComparison.Ordinal);
        }

        if (!TryTranslateDisplayName(nonNullSource, out translated))
        {
            translated = nonNullSource;
            return false;
        }

        actualTranslation = true;
        return true;
    }

    internal static bool TryTranslateDisplayName(string source, out string translated)
    {
        if (!TryExtractColorMarkup(source, out var markupPrefix, out var inner)
            || !TryTranslateDisplayNameInner(inner, out var translatedInner))
        {
            translated = source;
            return false;
        }

        translated = markupPrefix + translatedInner + MarkupSuffix;
        return true;
    }

    private static bool TryExtractColorMarkup(string source, out string markupPrefix, out string inner)
    {
        if (!source.StartsWith("{{", StringComparison.Ordinal)
            || !source.EndsWith(MarkupSuffix, StringComparison.Ordinal))
        {
            markupPrefix = string.Empty;
            inner = source;
            return false;
        }

        var separatorIndex = source.IndexOf('|', startIndex: 2);
        if (separatorIndex < 0)
        {
            markupPrefix = string.Empty;
            inner = source;
            return false;
        }

        markupPrefix = source.Substring(0, separatorIndex + 1);
        inner = source.Substring(
            separatorIndex + 1,
            source.Length - separatorIndex - 1 - MarkupSuffix.Length);
        return true;
    }

    private static bool TryTranslateDisplayNameInner(string source, out string translated)
    {
        var presetMeal = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, PresetMealNameDictionaryFile);
        if (presetMeal is not null)
        {
            translated = presetMeal;
            return true;
        }

        if (HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(source, out translated))
        {
            return true;
        }

        return TryTranslatePossessiveDishName(source, out translated);
    }

    private static bool TryTranslatePossessiveDishName(string source, out string translated)
    {
        if (TryTranslatePossessiveDishName(source, "'s ", out translated)
            || TryTranslatePossessiveDishName(source, "' ", out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePossessiveDishName(
        string source,
        string separator,
        out string translated)
    {
        var separatorIndex = source.LastIndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            translated = source;
            return false;
        }

        var dishStart = separatorIndex + separator.Length;
        if (dishStart >= source.Length)
        {
            translated = source;
            return false;
        }

        var dish = source.Substring(dishStart);
        if (!HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(dish, out var translatedDish))
        {
            translated = source;
            return false;
        }

        translated = source.Substring(0, separatorIndex) + "の" + translatedDish;
        return true;
    }
}
