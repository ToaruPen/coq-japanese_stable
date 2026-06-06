using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class IExamineEventProcessIdentifyTranslationPatch
{
    private const string Context = nameof(IExamineEventProcessIdentifyTranslationPatch);
    private static readonly Regex IdentifyRealizationPattern = new(
        "^(?:You realize )(?:The |the |A |a |An |an )?(?<prior>.+?) (?<verb>is|are|was|were) (?:a |an |the )?(?<known>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.IExamineEvent");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "ProcessIdentify", Type.EmptyTypes);
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.ProcessIdentify target not found.", Context, targetType.FullName);
        return targets;
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

    public static void Postfix(bool __result)
    {
        try
        {
            if (__result)
            {
                _ = InventoryScreenRefreshAfterIdentify.TryRefresh();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
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

    internal static void SetInventoryScreenRefreshHooksForTests(
        Func<object?>? screenProvider,
        Action<object>? screenRefresher)
    {
        InventoryScreenRefreshAfterIdentify.SetInventoryScreenRefreshHooksForTests(screenProvider, screenRefresher);
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = IdentifyRealizationPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = BuildTranslation(match, spans);
        DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
        return true;
    }

    private static string BuildTranslation(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var prior = RestoreCapture(match, spans, "prior");
        var known = RestoreCapture(match, spans, "known");
        var copula = IsPastVerb(match.Groups["verb"].Value) ? "だった" : "だ";
        return $"{prior}は{known}{copula}とわかった！";
    }

    private static bool IsPastVerb(string verb)
    {
        return string.Equals(verb, "was", StringComparison.Ordinal)
            || string.Equals(verb, "were", StringComparison.Ordinal);
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
