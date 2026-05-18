using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CookingRecipeDisplayNameTranslationPatch
{
    internal const string Context = nameof(CookingRecipeDisplayNameTranslationPatch);
    internal const string Family = Context + ".HistoricSpiceGeneratedName";

    private const string WhiteMarkupPrefix = "{{W|";
    private const string MarkupSuffix = "}}";

    [ThreadStatic]
    private static int generateRecipeTileSuppressionDepth;

    internal static bool IsGenerateRecipeTileSuppressed => generateRecipeTileSuppressionDepth > 0;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Skills.Cooking.CookingRecipe");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "GetDisplayName", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.GetDisplayName() target not found.", Context);
        }

        return method;
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
        if (!TryExtractWhiteMarkup(source, out var inner)
            || !TryTranslateDisplayNameInner(inner, out var translatedInner))
        {
            translated = source;
            return false;
        }

        translated = WhiteMarkupPrefix + translatedInner + MarkupSuffix;
        return true;
    }

    private static bool TryExtractWhiteMarkup(string source, out string inner)
    {
        if (!source.StartsWith(WhiteMarkupPrefix, StringComparison.Ordinal)
            || !source.EndsWith(MarkupSuffix, StringComparison.Ordinal))
        {
            inner = source;
            return false;
        }

        inner = source.Substring(
            WhiteMarkupPrefix.Length,
            source.Length - WhiteMarkupPrefix.Length - MarkupSuffix.Length);
        return true;
    }

    private static bool TryTranslateDisplayNameInner(string source, out string translated)
    {
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
