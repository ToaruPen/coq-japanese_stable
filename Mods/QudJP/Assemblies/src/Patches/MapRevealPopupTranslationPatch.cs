using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MapRevealPopupTranslationPatch
{
    private const string Context = nameof(MapRevealPopupTranslationPatch);
    private const string MapRevealOwner = "XRL.World.Parts.MapReveal|HandleEvent";
    private const string FactionDeedOwner = "XRL.World.Parts.FactionDeed|HandleEvent";

    private static readonly Regex OwnerConsumptionWarningPattern = new(
        "^(?<owner>.+?) (?:is|are) not owned by you, and using (?<target>.+?) will consume (?<consumed>.+?)\\. Are you sure you want to do so\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OrdinaryPaperPattern = new(
        "^(?<subject>.+?) seems? to be behaving as nothing more than an ordinary piece of paper\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MapOfSurroundingsPattern = new(
        "^(?:It's|They're|You're|It is|They are|You are|(?<subject>.+?) (?:is|are)) a map of your surroundings!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FactionDeedAnnalsEntryPattern = new(
        "^You add the following entry into the Annals of Qud\\.\\n\\n\""
        + "On the (?<day>.+?) of (?<month>.+?), (?<player>.+?) became "
        + "(?<standing>admired|despised) by (?<faction>.+?) for (?<reason>.+?)\\.\"$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<string>? ownerStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var mapRevealType = GameTypeResolver.FindType("XRL.World.Parts.MapReveal", "MapReveal");
        var factionDeedType = GameTypeResolver.FindType("XRL.World.Parts.FactionDeed", "FactionDeed");
        var inventoryActionEventType = GameTypeResolver.FindType("XRL.World.InventoryActionEvent", "InventoryActionEvent");
        if (mapRevealType is null || factionDeedType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve MapReveal, FactionDeed, or InventoryActionEvent.", Context);
            yield break;
        }

        var mapRevealMethod = AccessTools.Method(mapRevealType, "HandleEvent", [inventoryActionEventType]);
        if (mapRevealMethod is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) target not found.", Context);
        }
        else
        {
            yield return mapRevealMethod;
        }

        var factionDeedMethod = AccessTools.Method(factionDeedType, "HandleEvent", [inventoryActionEventType]);
        if (factionDeedMethod is null)
        {
            Trace.TraceError("QudJP: {0}.FactionDeed.HandleEvent(InventoryActionEvent) target not found.", Context);
        }
        else
        {
            yield return factionDeedMethod;
        }
    }

    public static void Prefix(MethodBase __originalMethod)
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
            ownerStack ??= new Stack<string>();
            ownerStack.Push(FormatOwnerKey(__originalMethod));
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
            if (ownerStack is { Count: > 0 })
            {
                _ = ownerStack.Pop();
            }

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

        return TryTranslatePopupMessageForOwnerKey(source, CurrentOwnerKey(), route, family, out translated);
    }

    internal static bool TryTranslatePopupMessageForOwnerKey(
        string source,
        string? ownerKey,
        string route,
        string family,
        out string translated)
    {
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

        try
        {
            var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
            translated = source;
            return (IsDocumentOwner(ownerKey)
                    && (TryTranslateOwnerConsumptionWarning(source, stripped, spans, route, family, out translated)
                        || TryTranslateOrdinaryPaper(source, stripped, spans, route, family, out translated)))
                || (IsFactionDeedOwner(ownerKey)
                    && TryTranslateFactionDeedAnnalsEntry(source, stripped, spans, route, family, out translated))
                || (IsMapRevealOwner(ownerKey)
                    && TryTranslateMapOfSurroundings(source, stripped, spans, route, family, out translated));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TryTranslatePopupMessage failed: {1}", Context, ex);
            translated = source;
            return false;
        }
    }

    private static bool TryTranslateOwnerConsumptionWarning(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = OwnerConsumptionWarningPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(
                RestoreCapture(match, spans, "owner"),
                "はあなたのものではなく、",
                RestoreCapture(match, spans, "target"),
                "を使うと",
                RestoreCapture(match, spans, "consumed"),
                "は消費される。本当に行いますか？"),
            stripped,
            spans,
            source);
        Record(route, family, "OwnerConsumptionWarning", source, translated);
        return true;
    }

    private static bool TryTranslateOrdinaryPaper(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = OrdinaryPaperPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            RestoreCapture(match, spans, "subject") + "は普通の紙切れとしてしか振る舞っていないようだ。",
            stripped,
            spans,
            source);
        Record(route, family, "OrdinaryPaper", source, translated);
        return true;
    }

    private static bool TryTranslateMapOfSurroundings(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = MapOfSurroundingsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = match.Groups["subject"];
        translated = RestoreWholeSourceBoundary(
            subject.Success
                ? RestoreCapture(match, spans, "subject") + "は周囲の地図だ！"
                : "周囲の地図だ！",
            stripped,
            spans,
            source);
        Record(route, family, "MapOfSurroundings", source, translated);
        return true;
    }

    private static bool TryTranslateFactionDeedAnnalsEntry(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = FactionDeedAnnalsEntryPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var standing = string.Equals(match.Groups["standing"].Value, "admired", StringComparison.Ordinal)
            ? "敬愛"
            : "嫌悪";
        translated = RestoreWholeSourceBoundary(
            string.Concat(
                "{{K|クッド年代記}}に次の項目を追加した。\n\n「",
                RestoreCapture(match, spans, "month"),
                "の",
                RestoreCapture(match, spans, "day"),
                "、",
                RestoreCapture(match, spans, "player"),
                "は",
                RestoreCapture(match, spans, "reason"),
                "により",
                RestoreCapture(match, spans, "faction"),
                "から",
                standing,
                "されるようになった。」"),
            stripped,
            spans,
            source);
        Record(route, family, "FactionDeedAnnalsEntry", source, translated);
        return true;
    }

    private static string? CurrentOwnerKey()
    {
        return ownerStack is { Count: > 0 } ? ownerStack.Peek() : null;
    }

    private static bool IsDocumentOwner(string? ownerKey)
    {
        return OwnerMatches(ownerKey, MapRevealOwner, FactionDeedOwner);
    }

    private static bool IsMapRevealOwner(string? ownerKey)
    {
        return OwnerMatches(ownerKey, MapRevealOwner);
    }

    private static bool IsFactionDeedOwner(string? ownerKey)
    {
        return OwnerMatches(ownerKey, FactionDeedOwner);
    }

    private static bool OwnerMatches(string? actual, params string[] expected)
    {
        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        for (var index = 0; index < expected.Length; index++)
        {
            if (string.Equals(actual, expected[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatOwnerKey(MethodBase method)
    {
        return (method.DeclaringType?.FullName ?? string.Empty) + "|" + method.Name;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWholeSourceBoundary(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        _ = family;
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
    }
}
