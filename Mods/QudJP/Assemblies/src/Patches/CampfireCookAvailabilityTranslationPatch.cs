using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfireCookAvailabilityTranslationPatch
{
    private const string Context = nameof(CampfireCookAvailabilityTranslationPatch);

    private static readonly Regex TurnedOffPattern = new(
        "^(?<subject>.+?) (?:is|are) turned off\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NotEnoughChargePattern = new(
        "^(?<subject>.+?) (?:do|does) not have enough charge to operate\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MustBeHungPattern = new(
        "^(?<subject>.+?) needs? to be hung up first\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NotWorkingPattern = new(
        "^(?<subject>.+?) (?:do|does) not seem to be working\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Campfire");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "Cook", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Campfire.Cook target not found.", Context);
            yield break;
        }

        yield return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
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
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslatePattern(
                TurnedOffPattern,
                source,
                subject => $"{subject}はオフになっている。",
                out translated)
            && !TryTranslatePattern(
                NotEnoughChargePattern,
                source,
                subject => $"{subject}には動作に必要な充電が足りない。",
                out translated)
            && !TryTranslatePattern(
                MustBeHungPattern,
                source,
                subject => $"{subject}は先につり下げる必要がある。",
                out translated)
            && !TryTranslatePattern(
                NotWorkingPattern,
                source,
                subject => $"{subject}は動作していないようだ。",
                out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<string, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var group = match.Groups["subject"];
        var subject = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(subject),
            spans,
            stripped.Length,
            source);
        return true;
    }
}
