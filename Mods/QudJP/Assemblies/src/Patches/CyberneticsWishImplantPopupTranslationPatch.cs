using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsWishImplantPopupTranslationPatch
{
    private const string Context = nameof(CyberneticsWishImplantPopupTranslationPatch);

    private static readonly Regex MissingBlueprintPattern = new(
        "^No blueprint by the name '(?<name>.+?)' could be found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NotCyberneticPattern = new(
        "^The blueprint '(?<blueprint>.+?)' is not a cybernetic\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MissingBodyPartPattern = new(
        "^No body part by the name '(?<part>.+?)' could be found\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ImplantedPattern = new(
        "^Your (?<part>.+?) (?:is|are) implanted with (?<implant>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Capabilities.Cybernetics");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "WishImplant", [typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.WishImplant(string) not found.", Context);
        }

        return method;
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

        if (!TryTranslateCore(source, out translated, out var detail))
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

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        if (TryTranslatePattern(
                MissingBlueprintPattern,
                source,
                "MissingBlueprint",
                (match, spans) => $"'{Restore(match, spans, "name")}'というブループリントは見つからない。",
                out translated,
                out detail))
        {
            return true;
        }

        if (TryTranslatePattern(
                NotCyberneticPattern,
                source,
                "NotCybernetic",
                (match, spans) => $"ブループリント'{Restore(match, spans, "blueprint")}'はサイバネではない。",
                out translated,
                out detail))
        {
            return true;
        }

        if (TryTranslatePattern(
                MissingBodyPartPattern,
                source,
                "MissingBodyPart",
                (match, spans) => $"'{Restore(match, spans, "part")}'という身体部位は見つからない。",
                out translated,
                out detail))
        {
            return true;
        }

        return TryTranslatePattern(
            ImplantedPattern,
            source,
            "Implanted",
            (match, spans) => $"{Restore(match, spans, "part")}に{Restore(match, spans, "implant")}を埋め込んだ！",
            out translated,
            out detail);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        string matchedDetail,
        Func<Match, System.Collections.Generic.IReadOnlyList<ColorSpan>, string> translate,
        out string translated,
        out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        detail = matchedDetail;
        return true;
    }

    private static string Restore(Match match, System.Collections.Generic.IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
