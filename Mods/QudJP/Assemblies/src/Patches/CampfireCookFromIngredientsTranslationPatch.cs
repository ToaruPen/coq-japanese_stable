using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfireCookFromIngredientsTranslationPatch
{
    private const string Context = nameof(CampfireCookFromIngredientsTranslationPatch);

    private static readonly Regex RecipeCreatedPattern = new(
        "^You create a new recipe for (?<recipe>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SelectedIngredientsPattern = new(
        "^Cook with the (?<count>\\d+) selected ingredients\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RemainingIngredientsPattern = new(
        "^\\[(?:up to (?<remaining>\\d+) remaining|0 remaining)\\]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Campfire");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "CookFromIngredients", [typeof(bool)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.CookFromIngredients(bool) target not found.", Context);
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
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
            OwnerTranslationScope.Exit(ref activeDepth);
            if (!OwnerTranslationScope.IsActive(activeDepth))
            {
                directMarkerPassThroughText = null;
            }
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

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(source, ref directMarkerPassThroughText, out translated))
        {
            return true;
        }

        var ownerFamily = family + "." + Context;
        if (CampfireCookingPopupTextTranslator.TryTranslateMealDescriptionPopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, ownerFamily + ".MealDescription", source, translated);
            return true;
        }

        if (CampfireCookingPopupTextTranslator.TryTranslateAteMealPopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, ownerFamily + ".AteMeal", source, translated);
            return true;
        }

        if (TryTranslateRecipeCreated(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, ownerFamily + ".RecipeCreated", source, translated);
            return true;
        }

        if (CookingRuntimeTranslationPatch.TryTranslateMetabolizeMealPopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, ownerFamily + ".MetabolizeMeal", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslatePopupProducerText(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (!TryTranslateSelectedIngredientsMenuRow(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            family + "." + Context + ".SelectedIngredientsMenuRow",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateRecipeCreated(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = RecipeCreatedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedCore = RestoreCapture(match, spans, "recipe") + "の新しいレシピを作った！";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateSelectedIngredientsMenuRow(string source, out string translated)
    {
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length is < 1 or > 2)
        {
            translated = source;
            return false;
        }

        if (!TryTranslateSelectedIngredientsLine(lines[0], out var selectedLine))
        {
            translated = source;
            return false;
        }

        if (lines.Length == 1)
        {
            translated = selectedLine;
            return true;
        }

        if (!TryTranslateRemainingIngredientsLine(lines[1], out var remainingLine))
        {
            translated = source;
            return false;
        }

        translated = selectedLine + "\n" + remainingLine;
        return true;
    }

    private static bool TryTranslateSelectedIngredientsLine(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SelectedIngredientsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var count = RestoreCapture(match, spans, "count");
        var translatedCore = "選択した材料" + count + "個で料理する。";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateRemainingIngredientsLine(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = RemainingIngredientsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var remaining = match.Groups["remaining"];
        var translatedCore = remaining.Success
            ? "[あと" + ColorAwareTranslationComposer.MarkupAwareRestoreCapture(remaining.Value, spans, remaining).Trim() + "個まで]"
            : "[残り0個]";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
