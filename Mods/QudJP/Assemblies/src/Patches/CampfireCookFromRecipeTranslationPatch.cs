using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfireCookFromRecipeTranslationPatch
{
    private const string Context = nameof(CampfireCookFromRecipeTranslationPatch);

    private static readonly Regex HiddenRecipesPattern = new(
        "^Show (?<count>\\d+) hidden recipes missing ingredients$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HiddenRecipesIntroPattern = new(
        "^< (?<count>\\d+) hidden for missing ingredients >$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MissingServingsPattern = new(
        "^You don't have enough servings of (?<ingredient>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MissingIngredientPattern = new(
        "^You don't have enough (?<ingredient>.+?)\\.$",
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

        var method = AccessTools.Method(targetType, "CookFromRecipe", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.CookFromRecipe() target not found.", Context);
        }

        return method;
    }

    public static void Prefix(out string? __state)
    {
        try
        {
            __state = directMarkerPassThroughText;
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            __state = null;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception, string? __state)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
            directMarkerPassThroughText = __state;
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

        if (CampfireCookingPopupTextTranslator.TryTranslateAteMealPopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".AteMeal", source, translated);
            return true;
        }

        if (TryTranslateMissingIngredientPopup(source, out translated, out var detail))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
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

        if (TryTranslateFixedMenuLabel(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".MenuLabel", source, translated);
            return true;
        }

        if (TryTranslateHiddenRecipesRow(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".HiddenRecipesRow", source, translated);
            return true;
        }

        if (TryTranslateHiddenRecipesIntro(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".HiddenRecipesIntro", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateFixedMenuLabel(string source, out string translated)
    {
        translated = source switch
        {
            "Cook" => "料理する",
            "Add to favorite recipes" => "お気に入りレシピに追加",
            "Remove from favorite recipes" => "お気に入りレシピから外す",
            "Forget" => "忘れる",
            "Back" => "戻る",
            _ => source,
        };
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateHiddenRecipesRow(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = HiddenRecipesPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var count = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["count"].Value,
            spans,
            match.Groups["count"]).Trim();
        var translatedCore = "材料不足の非表示レシピを" + count + "件表示";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateHiddenRecipesIntro(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = HiddenRecipesIntroPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var count = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["count"].Value,
            spans,
            match.Groups["count"]).Trim();
        var translatedCore = "< 材料不足のため非表示: " + count + "件 >";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateMissingIngredientPopup(
        string source,
        out string translated,
        out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = MissingServingsPattern.Match(stripped);
        if (match.Success)
        {
            var ingredient = TranslateIngredientCapture(match, spans);
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                ingredient + "の食分が足りない。",
                spans,
                stripped.Length,
                source);
            detail = "MissingIngredientServings";
            return true;
        }

        match = MissingIngredientPattern.Match(stripped);
        if (match.Success)
        {
            var ingredient = TranslateIngredientCapture(match, spans);
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                ingredient + "が足りない。",
                spans,
                stripped.Length,
                source);
            detail = "MissingIngredient";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string TranslateIngredientCapture(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var ingredient = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["ingredient"].Value,
            spans,
            match.Groups["ingredient"]).Trim();
        return CookingIngredientFragmentTranslator.TryTranslate(ingredient, out var translatedIngredient)
            ? translatedIngredient
            : ingredient;
    }
}
