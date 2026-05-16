using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GeneratedQueueDoesVerbTranslationPatch
{
    private const string Context = nameof(GeneratedQueueDoesVerbTranslationPatch);

    private static readonly Regex DropDownPattern = new(
        "^(?<subject>.+?) drops? (?<item>.+?) down (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PaxKlanqPattern = new(
        "^(?<subject>.+?) shouts? shouts? (?<cry>KLANQ)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExtradimensionalLootPattern = new(
        "^(?<subject>.+?) drops? (?<item>.+?), and by sheer chance (?:it|he|she|they) quantum tunnel(?:s)? and fully materialize(?:s)? in this dimension\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SomebodyRiflesThroughPattern = new(
        "^Somebody rifles through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoLimbsPattern = new(
        "^(?<subject>.+?) (?:has|have) no limbs\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryType = AccessTools.TypeByName("XRL.World.IInventory");
        var realityStabilizeEventType = AccessTools.TypeByName("XRL.World.RealityStabilizeEvent");
        if (eventType is null || cellType is null || gameObjectType is null || inventoryType is null || realityStabilizeEventType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            yield break;
        }

        foreach (var target in ResolveTarget("XRL.World.AI.GoalHandlers.DropOffStolenGoods", "MoveToDropoff", Type.EmptyTypes))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.AI.GoalHandlers.PaxKlanqMadness", "TakeAction", Type.EmptyTypes))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget(
                     "XRL.World.Anatomy.BodyPart",
                     "UnequipPartAndChildren",
                     new[] { typeof(bool), inventoryType, typeof(bool) }))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.ExtradimensionalLoot", "FireEvent", new[] { eventType }))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.GelatenousPalmProperties", "FireEvent", new[] { eventType }))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.GraveMoss", "Trigger", Type.EmptyTypes))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget(
                     "XRL.World.Parts.Garbage",
                     "AttemptRifle",
                     new[] { gameObjectType, typeof(bool), cellType, typeof(List<>).MakeGenericType(gameObjectType) }))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.QuantumRippler", "HandleEvent", new[] { realityStabilizeEventType }))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.ReclamationCist", "PerformReclamationOf", new[] { gameObjectType }))
        {
            yield return target;
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        var sourceWithoutLeadingDoesMarker = StripLeadingDoesMarker(message);
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(sourceWithoutLeadingDoesMarker);
        if (TryTranslateDropDown(stripped, spans, sourceWithoutLeadingDoesMarker, out var generatedTranslated)
            || TryTranslatePaxKlanq(stripped, spans, sourceWithoutLeadingDoesMarker, out generatedTranslated)
            || TryTranslateExtradimensionalLoot(stripped, spans, sourceWithoutLeadingDoesMarker, out generatedTranslated)
            || TryTranslateSomebodyRiflesThrough(stripped, spans, sourceWithoutLeadingDoesMarker, out generatedTranslated)
            || TryTranslateNoLimbs(stripped, spans, sourceWithoutLeadingDoesMarker, out generatedTranslated))
        {
            DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, generatedTranslated);
            message = generatedTranslated;
            return true;
        }

        if (!DoesVerbRouteTranslator.TryTranslateMarkedMessage(message, out var translated)
            && !DoesVerbRouteTranslator.TryTranslatePlainSentence(message, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}.", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static string StripLeadingDoesMarker(string source)
    {
        if (string.IsNullOrEmpty(source) || source[0] != '\x02')
        {
            return source;
        }

        var markerEnd = source.IndexOf("\x03", StringComparison.Ordinal);
        return markerEnd < 0 || markerEnd == source.Length - 1
            ? source
            : source.Substring(markerEnd + 1);
    }

    private static bool TryTranslateDropDown(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = DropDownPattern.Match(stripped);
        if (!match.Success)
        {
            translated = string.Empty;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            $"{StripLeadingArticle(RestoreCapture(match, spans, "item"))}を"
            + $"{StripLeadingArticle(RestoreCapture(match, spans, "target"))}に落とした。",
            spans,
            stripped,
            source);
        return true;
    }

    private static bool TryTranslatePaxKlanq(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = PaxKlanqPattern.Match(stripped);
        if (!match.Success)
        {
            translated = string.Empty;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            $"{NormalizeSubject(RestoreCapture(match, spans, "subject"))}は{RestoreCapture(match, spans, "cry")}と叫んだ！",
            spans,
            stripped,
            source);
        return true;
    }

    private static bool TryTranslateExtradimensionalLoot(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = ExtradimensionalLootPattern.Match(stripped);
        if (!match.Success)
        {
            translated = string.Empty;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            $"{NormalizeSubject(RestoreCapture(match, spans, "subject"))}は"
            + $"{StripLeadingArticle(RestoreCapture(match, spans, "item"))}を落とし、"
            + "偶然にもそれは量子トンネルを通ってこの次元に完全実体化した。",
            spans,
            stripped,
            source);
        return true;
    }

    private static bool TryTranslateSomebodyRiflesThrough(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = SomebodyRiflesThroughPattern.Match(stripped);
        if (!match.Success)
        {
            translated = string.Empty;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            $"誰かが{StripLeadingArticle(RestoreCapture(match, spans, "target"))}を漁った",
            spans,
            stripped,
            source);
        return true;
    }

    private static bool TryTranslateNoLimbs(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = NoLimbsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = string.Empty;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            $"{NormalizeSubject(RestoreCapture(match, spans, "subject"))}には四肢がない",
            spans,
            stripped,
            source);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            group.Value,
            ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, match.Value.Length),
            group).Trim();
    }

    private static string RestoreWholeSourceBoundary(
        string visible,
        IReadOnlyList<ColorSpan> spans,
        string stripped,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            visible,
            spans,
            stripped.Length,
            source);
    }

    private static string NormalizeSubject(string value)
    {
        var subject = StripLeadingArticle(value);
        return string.Equals(subject, "You", StringComparison.OrdinalIgnoreCase)
            ? "あなた"
            : subject;
    }

    private static string StripLeadingArticle(string value)
    {
        return StringHelpers.StripLeadingEnglishArticle(
            value.Trim(),
            includeCapitalizedDefiniteArticle: true);
    }
}
