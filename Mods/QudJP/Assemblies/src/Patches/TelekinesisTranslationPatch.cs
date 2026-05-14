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
            return targets;
        }

        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Telekinesis");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [eventType]);
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.HandleEvent target not found.", Context, targetType.FullName);
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

    private static string RestoreObject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["object"];
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        return StringHelpers.StripLeadingEnglishArticle(restored, includeCapitalizedDefiniteArticle: true);
    }
}
