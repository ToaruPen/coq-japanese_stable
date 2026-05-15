using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ShortBladesShankTranslationPatch
{
    private const string Context = nameof(ShortBladesShankTranslationPatch);

    private static readonly Regex ShankAttemptPattern = new(
        "^(?<actor>.+?) (?<verb>attempt|attempts) to take advantage of (?<target>.+?) misfortune and shank (?<pronoun>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SelfConfirmationPattern = new(
        "^Are you sure you want to shank (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var shankType = AccessTools.TypeByName("XRL.World.Parts.Skill.ShortBlades_Shank");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (shankType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var cast = AccessTools.Method(shankType, "Cast", [gameObjectType, shankType, gameObjectType]);
        if (cast is not null)
        {
            yield return cast;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Cast(GameObject, ShortBlades_Shank, GameObject) not found.", Context);
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

        if (!TryTranslateQueuedMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".ShortBladesShankAttempt",
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SelfConfirmationPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = TranslatePopupTarget(match, spans);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{target}の急所を突きますか？",
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".ShortBladesShankSelfConfirmation",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateQueuedMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ShankAttemptPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var actor = TranslateActor(Restore(match, spans, "actor"));
        var target = TranslatePossessiveTarget(Restore(match, spans, "target"));
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{actor}は{target}の不運につけ込んで急所を突こうとした。",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslateActor(string actor)
    {
        return actor == "You" ? "あなた" : actor;
    }

    private static string TranslatePossessiveTarget(string target)
    {
        if (target == "your")
        {
            return "あなた";
        }

        if (target.EndsWith("'s", StringComparison.Ordinal))
        {
            return target.Substring(0, target.Length - 2);
        }

        return target.EndsWith("'", StringComparison.Ordinal)
            ? target.Substring(0, target.Length - 1)
            : target;
    }

    private static string TranslatePopupTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["target"];
        return group.Value == "yourself"
            ? ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
                "自分自身",
                spans,
                group)
            : Restore(match, spans, "target");
    }
}
