using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TenfoldPathInitiatoryTranslationPatch
{
    private const string Context = nameof(TenfoldPathInitiatoryTranslationPatch);
    private const string InfiniteGraceMessage =
        "You feel a sense of infinite grace flow through your being as you are brought from the brink of death to miraculous health.";
    private const string InfiniteGraceTranslation =
        "無限の恩寵が身を満たし、死の淵から奇跡的な回復へと引き戻された。";

    private static readonly Regex SupernalLightPattern = new(
        "^(?<subject>.+?)(?:shines?|shine) with a supernal light as (?<possessive>.+?) injuries disappear\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttackInhibitionPattern = new(
        "^You cannot bring yourself to attack (?<target>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SkillPointGainPattern = new(
        "^You gain (?<points>.+?) skill points?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var beforeDieEventType = AccessTools.TypeByName("XRL.World.BeforeDieEvent");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (beforeDieEventType is null || eventType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Parts.Skill.TenfoldPath_Ket", "HandleEvent", new[] { beforeDieEventType });
        AddTarget(targets, "XRL.World.Parts.Skill.TenfoldPath_Vur", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Parts.Skill.TenfoldPath_Yis", "AddSkill", new[] { gameObjectType });
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

        if (!TryTranslateInitiatoryMessage(message, out var translated))
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

        if (TryTranslateInitiatoryMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateInitiatoryMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (string.Equals(source, InfiniteGraceMessage, StringComparison.Ordinal))
        {
            translated = InfiniteGraceTranslation;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SupernalLightPattern.Match(stripped);
        if (match.Success)
        {
            var subject = ColorAwareTranslationComposer.RestoreCapture(
                match.Groups["subject"].Value,
                spans,
                match.Groups["subject"]);
            translated = $"{subject}は{{{{white|超越的な光を放ち}}}}、傷が消えた。";
            return true;
        }

        match = AttackInhibitionPattern.Match(stripped);
        if (match.Success)
        {
            var target = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
                match.Groups["target"].Value,
                spans,
                match.Groups["target"]);
            translated = $"{target}を攻撃する勇気が出ない。";
            return true;
        }

        match = SkillPointGainPattern.Match(stripped);
        if (match.Success)
        {
            var points = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
                match.Groups["points"].Value,
                spans,
                match.Groups["points"]);
            translated = $"スキルポイントを{points}獲得した。";
            return true;
        }

        translated = source;
        return false;
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
