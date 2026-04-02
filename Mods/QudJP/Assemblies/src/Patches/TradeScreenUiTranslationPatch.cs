using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TradeScreenUiTranslationPatch
{
    private const string Context = nameof(TradeScreenUiTranslationPatch);

    private static readonly Regex TradeSomePromptPattern = new(
        "^Add how many (?<name>.+) to trade\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SortPrefixPattern = new(
        "^(?<label>sort:\\s)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ByClassPattern = new(
        "(?<label>by class)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new List<MethodBase>();

        AddTradeScreenUpdateMenuBars(methods);
        AddPopupAskNumberAsync(methods);
        AddLegacyTradeUpdateTotals(methods);

        if (methods.Count == 0)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch failed to resolve any target methods.");
        }

        return methods;
    }

    public static void Prefix(MethodBase __originalMethod, object? __instance, object[] __args)
    {
        try
        {
            if (__originalMethod?.Name == "UpdateMenuBars")
            {
                TranslateModernMenuOptions(__instance?.GetType() ?? __originalMethod.DeclaringType);
                return;
            }

            if (__originalMethod?.Name == "AskNumberAsync")
            {
                TranslateAskNumberArgs(__args);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(MethodBase __originalMethod)
    {
        try
        {
            if (__originalMethod?.Name == "UpdateTotals")
            {
                TranslateLegacyReadout(__originalMethod.DeclaringType);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void AddTradeScreenUpdateMenuBars(List<MethodBase> methods)
    {
        var tradeScreenType = GameTypeResolver.FindType("Qud.UI.TradeScreen", "TradeScreen");
        if (tradeScreenType is null)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch could not resolve type Qud.UI.TradeScreen / TradeScreen.");
            return;
        }

        var method = AccessTools.Method(tradeScreenType, "UpdateMenuBars");
        if (method is null)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch could not resolve {0}.UpdateMenuBars.", tradeScreenType.FullName);
            return;
        }

        methods.Add(method);
    }

    private static void AddPopupAskNumberAsync(List<MethodBase> methods)
    {
        var popupType = AccessTools.TypeByName("XRL.UI.Popup");
        if (popupType is null)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch could not resolve type XRL.UI.Popup.");
            return;
        }

        var method = AccessTools.Method(
            popupType,
            "AskNumberAsync",
            new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch could not resolve XRL.UI.Popup.AskNumberAsync(string, int, int, int, string, bool).");
            return;
        }

        methods.Add(method);
    }

    private static void AddLegacyTradeUpdateTotals(List<MethodBase> methods)
    {
        var tradeUiType = AccessTools.TypeByName("XRL.UI.TradeUI");
        if (tradeUiType is null)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch could not resolve type XRL.UI.TradeUI.");
            return;
        }

        var tradeEntryType = AccessTools.TypeByName("XRL.UI.TradeEntry");
        if (tradeEntryType is null)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch could not resolve type XRL.UI.TradeEntry.");
            return;
        }

        var method = AccessTools.Method(
            tradeUiType,
            "UpdateTotals",
            new[]
            {
                typeof(double[]),
                typeof(int[]),
                typeof(List<>).MakeGenericType(tradeEntryType).MakeArrayType(),
                typeof(int[][]),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: TradeScreenUiTranslationPatch could not resolve XRL.UI.TradeUI.UpdateTotals with expected signature.");
            return;
        }

        methods.Add(method);
    }

    private static void TranslateModernMenuOptions(Type? targetType)
    {
        if (targetType is null)
        {
            return;
        }

        TranslateMenuOptionsCollection(GetStaticMemberValue(targetType, "defaultMenuOptions"), "defaultMenuOptions");
        TranslateMenuOptionsCollection(GetStaticMemberValue(targetType, "getItemMenuOptions"), "getItemMenuOptions");
    }

    private static void TranslateMenuOptionsCollection(object? maybeCollection, string routeSuffix)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        var index = 0;
        foreach (var item in enumerable)
        {
            TranslateMenuOption(item, routeSuffix + "[" + index + "]");
            index++;
        }
    }

    private static void TranslateMenuOption(object? menuOption, string routeSuffix)
    {
        if (menuOption is null)
        {
            return;
        }

        TranslateMenuOptionMember(menuOption, "Description", routeSuffix + ".Description");
        TranslateMenuOptionMember(menuOption, "KeyDescription", routeSuffix + ".KeyDescription");
    }

    private static void TranslateMenuOptionMember(object menuOption, string memberName, string routeSuffix)
    {
        var current = UiBindingTranslationHelpers.GetStringMemberValue(menuOption, memberName);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + routeSuffix);
        var translated = TranslateMenuOptionText(current!, memberName, route);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(menuOption, memberName, translated);
        }
    }

    private static string TranslateMenuOptionText(string source, string memberName, string route)
    {
        if (string.Equals(memberName, "Description", StringComparison.Ordinal)
            && TryTranslateSortDescription(source, route, out var translatedSortDescription))
        {
            return translatedSortDescription;
        }

        return UiBindingTranslationHelpers.TranslateVisibleText(source, route, "TradeScreenUi.MenuOption");
    }

    private static bool TryTranslateSortDescription(string source, string route, out string translated)
    {
        var changed = false;
        translated = ReplaceLiteralSegment(source, SortPrefixPattern, ref changed);
        translated = ReplaceLiteralSegment(translated, ByClassPattern, ref changed);

        if (changed)
        {
            DynamicTextObservability.RecordTransform(route, "TradeScreenUi.MenuOption", source, translated);
        }

        return changed;
    }

    private static string ReplaceLiteralSegment(string source, Regex pattern, ref bool changed)
    {
        var localChanged = changed;
        var translated = pattern.Replace(
            source,
            match =>
            {
                var label = match.Groups["label"].Value;
                var translatedLabel = StringHelpers.TranslateExactOrLowerAscii(label);
                if (translatedLabel is null || string.Equals(translatedLabel, label, StringComparison.Ordinal))
                {
                    return match.Value;
                }

                localChanged = true;
                return ReplaceFirstOrdinal(match.Value, label, translatedLabel);
            });

        changed = localChanged;
        return translated;
    }

    private static string ReplaceFirstOrdinal(string source, string oldValue, string newValue)
    {
        var index = source.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0)
        {
            return source;
        }

        return source.Substring(0, index) + newValue + source.Substring(index + oldValue.Length);
    }

    private static void TranslateAskNumberArgs(object[] args)
    {
        if (args.Length == 0 || args[0] is not string message)
        {
            return;
        }

        if (TryTranslateTradeSomePrompt(message, out var translated))
        {
            args[0] = translated;
        }
    }

    private static bool TryTranslateTradeSomePrompt(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = TradeSomePromptPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate("Add how many {0} to trade.");
        if (string.Equals(template, "Add how many {0} to trade.", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var name = spans.Count == 0
            ? match.Groups["name"].Value
            : ColorAwareTranslationComposer.RestoreCapture(match.Groups["name"].Value, spans, match.Groups["name"]);
        translated = string.Format(CultureInfo.InvariantCulture, template, name);
        DynamicTextObservability.RecordTransform(Context, "TradeScreenUi.AskNumber", source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static void TranslateLegacyReadout(Type? targetType)
    {
        if (targetType is null)
        {
            return;
        }

        var current = GetStaticStringMemberValue(targetType, "sReadout");
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translatedToken = Translator.Translate(" drams");
        if (string.Equals(translatedToken, " drams", StringComparison.Ordinal))
        {
            return;
        }

        var translated = current!.Replace(" drams", translatedToken);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, "TradeUI.Readout", current, translated);
        SetStaticMemberValue(targetType, "sReadout", translated);
    }

    private static object? GetStaticMemberValue(Type targetType, string memberName)
    {
        var property = AccessTools.Property(targetType, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(null);
        }

        var field = AccessTools.Field(targetType, memberName);
        return field?.GetValue(null);
    }

    private static string? GetStaticStringMemberValue(Type targetType, string memberName)
    {
        return GetStaticMemberValue(targetType, memberName) as string;
    }

    private static void SetStaticMemberValue(Type targetType, string memberName, object? value)
    {
        var property = AccessTools.Property(targetType, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(null, value);
            return;
        }

        var field = AccessTools.Field(targetType, memberName);
        field?.SetValue(null, value);
    }
}
