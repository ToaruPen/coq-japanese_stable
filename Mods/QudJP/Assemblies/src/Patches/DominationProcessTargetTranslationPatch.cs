using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DominationProcessTargetTranslationPatch
{
    private const string Context = nameof(DominationProcessTargetTranslationPatch);

    private static readonly Regex NoMindPattern = new(
        "^There seems to be no mind in (?<target>.+?) to dominate\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SelfTargetPattern = new(
        "^You can't dominate (?<self>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoConsciousnessPattern = new(
        "^(?<target>.+?) (?:does|do) not have a consciousness you can make psychic contact with\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Domination");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target types.", Context);
            yield break;
        }

        var method = AccessTools.Method(
            targetType,
            "ProcessTarget",
            [gameObjectType, typeof(string).MakeByRefType()]);
        if (method is not null)
        {
            yield return method;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ProcessTarget(GameObject,ref string) target not found.", Context);
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

        if (!TryTranslateMessage(message, nameof(CombatAndLogMessageQueuePatch), "MessageQueue", out var translated))
        {
            return false;
        }

        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateMessage(string source, string route, string family, out string translated)
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

        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            translated = source;
            return false;
        }

        if (!TryTranslateCore(source, out translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        if (source == "You can't dominate someone you are already dominating.")
        {
            translated = "すでに支配している相手は支配できない。";
            detail = "Domination.AlreadyDominating";
            return true;
        }

        if (source == "You can't do that.")
        {
            translated = "それはできない。";
            detail = "Domination.CannotDoThat";
            return true;
        }

        if (source == "Nothing happens.")
        {
            translated = "何も起こらない。";
            detail = "Domination.NothingHappens";
            return true;
        }

        return TryTranslatePattern(
            NoMindPattern,
            source,
            (match, spans) => $"{Restore(match, spans, "target")}には支配する心がないようだ。",
            "Domination.NoMind",
            out translated,
            out detail)
            || TryTranslatePattern(
                SelfTargetPattern,
                source,
                (match, spans) => $"{TranslateSelfReference(Restore(match, spans, "self"))}は支配できない！",
                "Domination.SelfTarget",
                out translated,
                out detail)
            || TryTranslatePattern(
                NoConsciousnessPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}には精神接触できる意識がない。",
                "Domination.NoConsciousness",
                out translated,
                out detail);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        string patternDetail,
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
        detail = patternDetail;
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslateSelfReference(string source)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        return string.Equals(stripped, "yourself", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stripped, "itself", StringComparison.OrdinalIgnoreCase)
                ? ColorAwareTranslationComposer.Restore("自分自身", spans)
                : source;
    }
}
