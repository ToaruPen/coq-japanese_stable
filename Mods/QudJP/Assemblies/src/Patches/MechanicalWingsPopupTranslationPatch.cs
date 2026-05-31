using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MechanicalWingsPopupTranslationPatch
{
    private const string Context = nameof(MechanicalWingsPopupTranslationPatch);
    private const string StartupFamily = "MechanicalWingsStartup";
    private const string UnresponsiveFamily = "MechanicalWingsUnresponsive";
    private const string LongFallWarningFamily = "MechanicalWingsLongFallWarning";
    private const string WingsWillNotMoveFamily = "WingsWillNotMove";

    private static readonly Regex StatusPattern = new(
        "^(?:The |the |A |a |An |an )?(?<subject>.+?) (?:is|are) (?<extra>still starting up|unresponsive)(?<endmark>[.!])?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LongFallWarningPattern = new(
        "^It looks like a long way down (?:the |a |an )?(?<subject>.+?) you're above\\. Are you sure you want to stop flying\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WingsWillNotMovePattern = new(
        "^(?<subject>.+?) will not move!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.MechanicalWings");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var tryStartup = AccessTools.Method(targetType, "TryStartup", Type.EmptyTypes);
        if (tryStartup is null)
        {
            Trace.TraceError("QudJP: {0}.TryStartup target not found.", Context);
        }
        else
        {
            targets.Add(tryStartup);
        }

        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var fireEvent = eventType is null ? null : AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (fireEvent is null)
        {
            Trace.TraceError("QudJP: {0}.FireEvent target not found.", Context);
        }
        else
        {
            targets.Add(fireEvent);
        }

        var cathedraType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsCathedra");
        var commandEventType = AccessTools.TypeByName("XRL.World.CommandEvent");
        var cathedraHandleEvent = cathedraType is null || commandEventType is null
            ? null
            : AccessTools.Method(cathedraType, "HandleEvent", [commandEventType]);
        if (cathedraHandleEvent is null)
        {
            Trace.TraceError("QudJP: {0}.CyberneticsCathedra.HandleEvent target not found.", Context);
        }
        else
        {
            targets.Add(cathedraHandleEvent);
        }

        var wingsType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Wings");
        var wingsHandleEvent = wingsType is null || commandEventType is null
            ? null
            : AccessTools.Method(wingsType, "HandleEvent", [commandEventType]);
        if (wingsHandleEvent is null)
        {
            Trace.TraceError("QudJP: {0}.Wings.HandleEvent target not found.", Context);
        }
        else
        {
            targets.Add(wingsHandleEvent);
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

        if (!TryTranslateCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + GetFamilySuffix(source), source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var longFallMatch = LongFallWarningPattern.Match(stripped);
        if (longFallMatch.Success)
        {
            var longFallSubject = ColorAwareTranslationComposer.RestoreCapture(
                longFallMatch.Groups["subject"].Value,
                spans,
                longFallMatch.Groups["subject"]).Trim();
            var core = $"あなたがいる{longFallSubject}の下はかなり深そうだ。飛行をやめてもよいですか？";
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                core,
                spans,
                stripped.Length,
                source);
            return true;
        }

        var willNotMoveMatch = WingsWillNotMovePattern.Match(stripped);
        if (willNotMoveMatch.Success)
        {
            var wingsSubject = ColorAwareTranslationComposer.RestoreCapture(
                willNotMoveMatch.Groups["subject"].Value,
                spans,
                willNotMoveMatch.Groups["subject"]).Trim();
            if (wingsSubject.StartsWith("Your ", StringComparison.Ordinal))
            {
                wingsSubject = "あなたの" + wingsSubject.Substring("Your ".Length).Trim();
            }

            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                wingsSubject + "は動かない！",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var match = StatusPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["subject"].Value,
            spans,
            match.Groups["subject"]).Trim();
        var extra = match.Groups["extra"].Value;
        var endmark = match.Groups["endmark"].Success ? match.Groups["endmark"].Value : null;
        return MessageFrameTranslator.TryTranslateXDidY(subject, "are", extra, endmark, out translated);
    }

    private static string GetFamilySuffix(string source)
    {
        var stripped = ColorAwareTranslationComposer.GetVisibleText(source);
        if (stripped.Contains("long way down"))
        {
            return LongFallWarningFamily;
        }

        if (stripped.Contains("will not move"))
        {
            return WingsWillNotMoveFamily;
        }

        return stripped.Contains("still starting up")
            ? StartupFamily
            : UnresponsiveFamily;
    }
}
