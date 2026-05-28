using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TelekinesisTranslationPatch
{
    private const string Context = nameof(TelekinesisTranslationPatch);

    private static readonly Regex ObjectNotBudgePattern = new(
        "^(?<object>.+?) (?:does|do) not budge\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} InventoryActionEvent type not found.", Context);
        }

        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Telekinesis");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = eventType is null ? null : AccessTools.Method(targetType, "HandleEvent", [eventType]);
        if (method is not null)
        {
            targets.Add(method);
        }
        else
        {
            Trace.TraceError("QudJP: {0}.{1}.HandleEvent target not found.", Context, targetType.FullName);
        }

        var activateMethod = AccessTools.Method(targetType, "Activate", [typeof(bool)]);
        if (activateMethod is not null)
        {
            targets.Add(activateMethod);
        }
        else
        {
            Trace.TraceError("QudJP: {0}.{1}.Activate target not found.", Context, targetType.FullName);
        }

        var attemptMethod = AccessTools.Method(targetType, "AttemptTelekinesis");
        if (attemptMethod is not null)
        {
            targets.Add(attemptMethod);
        }
        else
        {
            Trace.TraceError("QudJP: {0}.{1}.AttemptTelekinesis target not found.", Context, targetType.FullName);
        }

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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
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
        if (TryTranslateExactPopup(stripped, spans, source, route, family, out translated))
        {
            return true;
        }

        var match = ObjectNotBudgePattern.Match(stripped);
        if (!match.Success || string.Equals(match.Groups["object"].Value, "You", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var subject = RestoreObject(match, spans);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{subject}はびくともしない。",
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslateExactPopup(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        string route,
        string family,
        out string translated)
    {
        var replacement = stripped switch
        {
            "Your psyche is too exhausted." => "精神が疲弊しすぎている。",
            "There is nothing you can telekinetically manipulate there." => "そこには念動力で操作できるものがない。",
            "You do not budge." => "あなたはびくともしない。",
            _ => null,
        };

        if (replacement is null)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            replacement,
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static string RestoreObject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["object"];
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        return StringHelpers.StripLeadingEnglishArticle(restored, includeCapitalizedDefiniteArticle: true);
    }
}
