using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ObjectFinderConfigFiltersTranslationPatch
{
    private const string Context = nameof(ObjectFinderConfigFiltersTranslationPatch);

    private static readonly Regex ColoredStateSuffixPattern = new(
        "^(?<prefix>.+?)\\{\\{(?<color>[^|]+)\\| \\[(?<state>Disabled|Hide|Show)\\]\\}\\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlainStateSuffixPattern = new(
        "^(?<prefix>.+?) \\[(?<state>Disabled|Hide|Show)\\]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, (string Text, string Family)> FixedTranslations =
        new Dictionary<string, (string Text, string Family)>(StringComparer.Ordinal)
        {
            ["Pick a filter to change"] = ("変更するフィルターを選択", "ObjectFinder.ConfigFilters.Title"),
            ["Show Items"] = ("アイテムを表示", "ObjectFinder.ConfigFilters.Action"),
            ["Hide Items"] = ("アイテムを非表示", "ObjectFinder.ConfigFilters.Action"),
            ["Ignore Rule"] = ("ルールを無視", "ObjectFinder.ConfigFilters.Action"),
            ["Move Up"] = ("上へ移動", "ObjectFinder.ConfigFilters.Action"),
            ["Move Down"] = ("下へ移動", "ObjectFinder.ConfigFilters.Action"),
        };

    private static readonly IReadOnlyDictionary<string, string> StateTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Disabled"] = "無効",
            ["Hide"] = "非表示",
            ["Show"] = "表示",
        };

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.UI.ObjectFinder");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRL.UI.ObjectFinder.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "ConfigFilters", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ConfigFilters() not found.", Context);
            yield break;
        }

        yield return method;
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
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool ShouldClaimPopupMessagePassthrough()
    {
        return OwnerTranslationScope.IsActive(activeDepth);
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;

        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (TryTranslateFixedPopupText(source, out translated))
        {
            return true;
        }

        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            translated = source;
            return false;
        }

        if (TryTranslateStateSuffix(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateFixedPopupText(string source, out string translated)
    {
        if (FixedTranslations.TryGetValue(source, out var fixedTranslation))
        {
            translated = fixedTranslation.Text;
            DynamicTextObservability.RecordTransform(Context, fixedTranslation.Family, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateStateSuffix(string source, out string translated)
    {
        var match = ColoredStateSuffixPattern.Match(source);
        if (match.Success && StateTranslations.TryGetValue(match.Groups["state"].Value, out var coloredState))
        {
            translated = match.Groups["prefix"].Value
                + "{{"
                + match.Groups["color"].Value
                + "| ["
                + coloredState
                + "]}}";
            DynamicTextObservability.RecordTransform(Context, "ObjectFinder.ConfigFilters.State", source, translated);
            return true;
        }

        match = PlainStateSuffixPattern.Match(source);
        if (match.Success && StateTranslations.TryGetValue(match.Groups["state"].Value, out var plainState))
        {
            translated = match.Groups["prefix"].Value + " [" + plainState + "]";
            DynamicTextObservability.RecordTransform(Context, "ObjectFinder.ConfigFilters.State", source, translated);
            return true;
        }

        translated = source;
        return false;
    }
}
