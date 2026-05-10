using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TradeLineTranslationPatch
{
    private const string Context = nameof(TradeLineTranslationPatch);

    private static readonly Regex CategoryTextPattern = new Regex(
        @"^(?<prefix>\[[+-]\]\s)(?<category>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(Context, "Qud.UI.TradeLine", "TradeLine");
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateCategoryText(__instance);
            TranslateItemText(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void TranslateCategoryText(object instance)
    {
        var textSkin = UiBindingTranslationHelpers.GetMemberValue(instance, "categoryText");
        var current = UITextSkinReflectionAccessor.GetCurrentText(textSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var match = CategoryTextPattern.Match(current!);
        if (!match.Success)
        {
            return;
        }

        var category = match.Groups["category"].Value;
        var route = ObservabilityHelpers.ComposeContext(Context, "field=categoryText");
        var translatedCategory = TranslateVisibleText(category, route, "TradeLine.CategoryText");
        if (string.Equals(translatedCategory, category, StringComparison.Ordinal))
        {
            return;
        }

        var translated = match.Groups["prefix"].Value + translatedCategory;
        OwnerTextSetter.SetTranslatedText(textSkin, current!, translated, Context, typeof(TradeLineTranslationPatch));
    }

    private static void TranslateItemText(object instance)
    {
        var textSkin = UiBindingTranslationHelpers.GetMemberValue(instance, "text");
        var current = UITextSkinReflectionAccessor.GetCurrentText(textSkin, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=text");
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(current, route);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(textSkin, current!, translated, Context, typeof(TradeLineTranslationPatch));
    }

    private static string TranslateVisibleText(string source, string route, string family)
    {
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
}
