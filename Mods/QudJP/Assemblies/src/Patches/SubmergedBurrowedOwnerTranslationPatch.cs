using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SubmergedBurrowedOwnerTranslationPatch
{
    internal const string Family = "SubmergedBurrowedOwner";

    private const string Context = nameof(SubmergedBurrowedOwnerTranslationPatch);
    private const string SubmergedOwner = "Submerged";
    private const string BurrowedOwner = "Burrowed";

    private static readonly Regex SubmergePattern = new(
        "^(?<subject>.+?) submerges?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BurrowIntoGroundPattern = new(
        "^(?<subject>.+?) burrows? into the ground\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmergeFromGroundPattern = new(
        "^(?<subject>.+?) emerges? from the ground\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmergeFromTargetPattern = new(
        "^(?<subject>.+?) emerges? from (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ForcedToSurfacePattern = new(
        "^(?<subject>.+?) (?:is|are) forced to the surface\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? activeOwner;

    [ThreadStatic]
    private static Stack<string?>? ownerStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var canChangeMovementModeEventType = AccessTools.TypeByName("XRL.World.CanChangeMovementModeEvent");
        var canTravelEventType = AccessTools.TypeByName("XRL.World.CanTravelEvent");

        AddTarget(targets, "XRL.World.Effects.Submerged", "Apply", gameObjectType);
        AddTarget(targets, "XRL.World.Effects.Submerged", "FireEvent", eventType);
        AddTarget(targets, "XRL.World.Effects.Submerged", "HandleEvent", canChangeMovementModeEventType);
        AddTarget(targets, "XRL.World.Effects.Submerged", "Remove", gameObjectType);

        AddTarget(targets, "XRL.World.Effects.Burrowed", "Apply", gameObjectType);
        AddTarget(targets, "XRL.World.Effects.Burrowed", "FireEvent", eventType);
        AddTarget(targets, "XRL.World.Effects.Burrowed", "HandleEvent", canChangeMovementModeEventType);
        AddTarget(targets, "XRL.World.Effects.Burrowed", "HandleEvent", canTravelEventType);
        AddTarget(targets, "XRL.World.Effects.Burrowed", "Remove", gameObjectType);
        AddTarget(targets, "XRL.World.Effects.Burrowed", "Emerge");
        return targets;
    }

    public static void Prefix(MethodBase __originalMethod)
    {
        try
        {
            ownerStack ??= new Stack<string?>();
            ownerStack.Push(activeOwner);
            activeOwner = ResolveOwner(__originalMethod);
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
            activeOwner = ownerStack is { Count: > 0 } ? ownerStack.Pop() : null;
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

        var owner = activeOwner;
        if (!TryTranslateCore(message, owner, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Family + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

        var owner = activeOwner;
        if (!TryTranslateCore(source, owner, out translated, out var detail))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, Family + "." + detail, source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, string? owner, out string translated, out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateExact(stripped, out translated, out detail))
        {
            translated = RestoreWholeSourceBoundary(translated, source, stripped, spans);
            return true;
        }

        return TryTranslateQueuedFrame(source, stripped, spans, owner, out translated, out detail);
    }

    private static bool TryTranslateExact(string stripped, out string translated, out string detail)
    {
        switch (stripped)
        {
            case "You cannot do that while submerged.":
                translated = "水中ではそんなことはできない。";
                detail = "Submerged.CannotDoThat";
                return true;
            case "You cannot do that while burrowed.":
                translated = "潜伏中はそれはできない。";
                detail = "Burrowed.CannotDoThat";
                return true;
            case "You cannot travel long distances while burrowed.":
                translated = "潜伏中は長距離を移動できない。";
                detail = "Burrowed.CannotTravel";
                return true;
            default:
                translated = string.Empty;
                detail = string.Empty;
                return false;
        }
    }

    private static bool TryTranslateQueuedFrame(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string? owner,
        out string translated,
        out string detail)
    {
        if (OwnerMatches(owner, SubmergedOwner)
            && TryTranslateSubjectOnlyFrame(
                SubmergePattern,
                source,
                stripped,
                spans,
                subject => SubjectPrefix(subject, "が") + "潜った。",
                "Submerged.Submerge",
                out translated,
                out detail))
        {
            return true;
        }

        if (OwnerMatches(owner, BurrowedOwner)
            && TryTranslateSubjectOnlyFrame(
                BurrowIntoGroundPattern,
                source,
                stripped,
                spans,
                subject => SubjectPrefix(subject, "が") + "地面に潜った。",
                "Burrowed.BurrowIntoGround",
                out translated,
                out detail))
        {
            return true;
        }

        if (OwnerMatches(owner, BurrowedOwner)
            && TryTranslateSubjectOnlyFrame(
                EmergeFromGroundPattern,
                source,
                stripped,
                spans,
                subject => SubjectPrefix(subject, "が") + "地面から現れた。",
                "Burrowed.EmergeFromGround",
                out translated,
                out detail))
        {
            return true;
        }

        if (OwnerMatches(owner, SubmergedOwner)
            && TryTranslateSubmergedEmergeFrom(source, stripped, spans, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateForcedToSurface(source, stripped, spans, owner, out translated, out detail))
        {
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateSubjectOnlyFrame(
        Regex pattern,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Func<string, string> translate,
        string matchedDetail,
        out string translated,
        out string detail)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            translate(RestoreNormalizedCapture(match, spans, "subject")),
            source,
            stripped,
            spans);
        detail = matchedDetail;
        return true;
    }

    private static bool TryTranslateSubmergedEmergeFrom(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = EmergeFromTargetPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var subject = RestoreNormalizedCapture(match, spans, "subject");
        var target = RestoreNormalizedCapture(match, spans, "target");
        translated = RestoreWholeSourceBoundary(
            SubjectPrefix(subject, "が") + target + "から浮上した。",
            source,
            stripped,
            spans);
        detail = "Submerged.EmergeFrom";
        return true;
    }

    private static bool TryTranslateForcedToSurface(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string? owner,
        out string translated,
        out string detail)
    {
        var match = ForcedToSurfacePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var subject = RestoreNormalizedCapture(match, spans, "subject");
        var destination = OwnerMatches(owner, SubmergedOwner) ? "水面" : "地表";
        var ownerDetail = OwnerMatches(owner, SubmergedOwner) ? "Submerged" : "Burrowed";
        translated = RestoreWholeSourceBoundary(
            SubjectPrefix(subject, "は") + destination + "に押し出された。",
            source,
            stripped,
            spans);
        detail = ownerDetail + ".ForcedToSurface";
        return true;
    }

    private static string RestoreNormalizedCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        return StripLeadingEnglishArticle(restored);
    }

    private static string SubjectPrefix(string subject, string particle)
    {
        return IsSecondPerson(subject) ? string.Empty : subject + particle;
    }

    private static bool IsSecondPerson(string subject)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(subject).Trim();
        return string.Equals(visible, "You", StringComparison.Ordinal)
            || string.Equals(visible, "you", StringComparison.Ordinal);
    }

    private static string StripLeadingEnglishArticle(string source)
    {
        var trimmed = source.Trim();
        if (StartsWithArticle(trimmed, "The ")
            || StartsWithArticle(trimmed, "the ")
            || StartsWithArticle(trimmed, "A ")
            || StartsWithArticle(trimmed, "a ")
            || StartsWithArticle(trimmed, "An ")
            || StartsWithArticle(trimmed, "an "))
        {
            return trimmed.Substring(trimmed.IndexOf(' ') + 1);
        }

        return trimmed;
    }

    private static bool StartsWithArticle(string source, string article)
    {
        return source.StartsWith(article, StringComparison.Ordinal);
    }

    private static string RestoreWholeSourceBoundary(
        string translated,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static bool OwnerMatches(string? owner, string expected)
    {
        return string.Equals(owner, expected, StringComparison.Ordinal);
    }

    private static string? ResolveOwner(MethodBase? method)
    {
        var declaringTypeName = method?.DeclaringType?.FullName ?? string.Empty;
        var methodName = method?.Name ?? string.Empty;
        if (methodName.StartsWith(SubmergedOwner, StringComparison.Ordinal)
            || string.Equals(declaringTypeName, "XRL.World.Effects.Submerged", StringComparison.Ordinal))
        {
            return SubmergedOwner;
        }

        if (methodName.StartsWith(BurrowedOwner, StringComparison.Ordinal)
            || string.Equals(declaringTypeName, "XRL.World.Effects.Burrowed", StringComparison.Ordinal))
        {
            return BurrowedOwner;
        }

        return null;
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, params Type?[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var resolvedParameterTypes = new Type[parameterTypes.Length];
        for (var index = 0; index < parameterTypes.Length; index++)
        {
            if (parameterTypes[index] is null)
            {
                Trace.TraceError("QudJP: {0}.{1}.{2} parameter type {3} not found.", Context, typeName, methodName, index);
                return;
            }

            resolvedParameterTypes[index] = parameterTypes[index]!;
        }

        var method = AccessTools.Method(targetType, methodName, resolvedParameterTypes);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
