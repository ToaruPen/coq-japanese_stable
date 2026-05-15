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

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
