using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AnimateObjectTranslationPatch
{
    private const string Context = nameof(AnimateObjectTranslationPatch);

    private static readonly Regex UnresponsivePattern = new(
        "^(?:The |the |A |a |An |an )?(?<object>.+?) (?:is|are) unresponsive\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ImbueLifePattern = new(
        "^You imbue (?<object>.+?) with life\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WholeMarkupArticlePattern = new(
        "^(?<open>\\{\\{[^|{}]+\\|)(?<article>a |an |the |The )(?<value>.+?)(?<close>\\}\\})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var animateObjectType = AccessTools.TypeByName("XRL.World.Parts.AnimateObject");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (animateObjectType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var handleEvent = AccessTools.Method(animateObjectType, "HandleEvent", [inventoryActionEventType]);
        if (handleEvent is not null)
        {
            yield return handleEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) not found.", Context);
        }
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

        if (!TryTranslate(source, out translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
        return true;
    }

    private static bool TryTranslate(string source, out string translated, out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var unresponsiveMatch = UnresponsivePattern.Match(stripped);
        if (unresponsiveMatch.Success)
        {
            translated = RestoreWhole(
                $"{RestoreCapture(unresponsiveMatch, spans, "object")}は反応しない。",
                spans,
                stripped.Length,
                source);
            detail = "AnimateObjectUnresponsive";
            return true;
        }

        var imbueMatch = ImbueLifePattern.Match(stripped);
        if (imbueMatch.Success)
        {
            translated = RestoreWhole(
                $"{RestoreArticleStrippedCapture(imbueMatch, spans, "object")}に生命を吹き込んだ。",
                spans,
                stripped.Length,
                source);
            detail = "AnimateObjectImbueLife";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string RestoreWhole(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int strippedLength,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            strippedLength,
            source);
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreArticleStrippedCapture(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        var markupMatch = WholeMarkupArticlePattern.Match(restored);
        if (markupMatch.Success)
        {
            return markupMatch.Groups["open"].Value
                   + markupMatch.Groups["value"].Value
                   + markupMatch.Groups["close"].Value;
        }

        return StringHelpers.StripLeadingEnglishArticle(
            restored,
            includeCapitalizedDefiniteArticle: true);
    }
}
