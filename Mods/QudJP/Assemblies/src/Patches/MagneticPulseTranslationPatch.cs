using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MagneticPulseTranslationPatch
{
    private const string Context = nameof(MagneticPulseTranslationPatch);
    private static readonly Regex CompanionRippedPattern = new(
        "^Your companion, (?<companion>.+?),(?:have|has) had (?:the |a |an )?(?<item>.+?) ripped from (?<possessive>.+?) body!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RippedFromPlayerPattern = new(
        "^(?:The |the |A |a |An |an )?(?<subject>.+?) (?:is|are) ripped from your body!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PulledTowardPattern = new(
        "^(?:The |the |A |a |An |an )?(?<subject>.+?) (?:is|are) pulled toward (?<target>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} GameObject type not found.", Context);
            return targets;
        }

        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.MagneticPulse");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "EmitMagneticPulse", new[] { gameObjectType, typeof(int) });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.EmitMagneticPulse target not found.", Context, targetType.FullName);
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (!TryTranslateMagneticPulseMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateMagneticPulseMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateMagneticPulseMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        return TryTranslateCompanionRippedMessage(source, out translated)
            || TryTranslateRippedFromPlayerMessage(source, out translated)
            || TryTranslatePulledTowardMessage(source, out translated);
    }

    private static bool TryTranslateCompanionRippedMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = CompanionRippedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var companion = RestoreCapture(match, spans, "companion");
        var item = RestoreCapture(match, spans, "item");
        translated = $"{companion}の体から{item}が引き剥がされた！";
        return true;
    }

    private static bool TryTranslateRippedFromPlayerMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = RippedFromPlayerPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "subject")}があなたの体から引き剥がされた！";
        return true;
    }

    private static bool TryTranslatePulledTowardMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PulledTowardPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = string.Equals(match.Groups["target"].Value, "something", StringComparison.Ordinal)
            ? "何か"
            : RestoreCapture(match, spans, "target");
        translated = $"{RestoreCapture(match, spans, "subject")}は{target}に引き寄せられた。";
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
