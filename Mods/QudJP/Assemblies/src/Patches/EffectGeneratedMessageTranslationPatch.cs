using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EffectGeneratedMessageTranslationPatch
{
    private const string Context = nameof(EffectGeneratedMessageTranslationPatch);

    private static readonly Regex PossessiveLifeDrainPattern = new(
        "^You resist (?<drainer>.+?)'s life drain!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LatchedHeldPattern = new(
        "^(?<subject>.+?) (?:are|is) held in place by (?<holder>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DismissFromServicePattern = new(
        "^(?<subject>.+?) (?:dismiss|dismisses) (?<target>.+?) from (?<owner>.+?) service(?<end>[.!?])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShieldWallPattern = new(
        "^(?<subject>.+?) (?:raise|raises) (?<owner>.+?) shield in wall formation(?<end>[.!?])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmptyTheClipsClaspPattern = new(
        "^(?<subject>.+?) (?:clasp|clasps) (?<owner>your|its|his|her|their|Your|Its|His|Her|Their) (?<target>.+?) eagerly(?<end>[.!?])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IllRecoveryPattern = new(
        "^(?<subject>.+?) (?:are|is) no longer ill(?<end>[.!?])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var beginTakeActionEventType = AccessTools.TypeByName("XRL.World.BeginTakeActionEvent");
        var endTurnEventType = AccessTools.TypeByName("XRL.World.EndTurnEvent");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (eventType is null
            || gameObjectType is null
            || beginTakeActionEventType is null
            || endTurnEventType is null
            || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Effects.Rusted", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.Asleep", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.EmptyTheClips", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.Ill", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.LatchedOnto", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.LifeDrain", "HandleEvent", new[] { endTurnEventType });
        AddTarget(targets, "XRL.World.Effects.Proselytized", "HandleEvent", new[] { inventoryActionEventType });
        AddTarget(targets, "XRL.World.Effects.Rebuked", "HandleEvent", new[] { inventoryActionEventType });
        AddTarget(targets, "XRL.World.Effects.Running", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.ShatteredArmor", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.ShieldWall", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.Stun", "HandleEvent", new[] { beginTakeActionEventType });
        AddTarget(targets, "XRL.World.Effects.StunGasStun", "FireEvent", new[] { eventType });
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateGeneratedEffectMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static bool TryTranslateGeneratedEffectMessage(string source, out string translated)
    {
        if (TryTranslateDismissFromServiceMessage(source, out translated)
            || TryTranslateEmptyTheClipsClaspMessage(source, out translated)
            || TryTranslateIllRecoveryMessage(source, out translated)
            || TryTranslateShieldWallMessage(source, out translated))
        {
            return true;
        }

        if (TryTranslateLatchedHeldMessage(source, out translated))
        {
            return true;
        }

        if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out translated)
            || DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated))
        {
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PossessiveLifeDrainPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"あなたは{Restore(match, spans, "drainer")}の生命吸収に抵抗した！",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateDismissFromServiceMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = DismissFromServicePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = NormalizeActor(Restore(match, spans, "subject"));
        var target = NormalizeActor(Restore(match, spans, "target"));
        var owner = NormalizePossessive(Restore(match, spans, "owner"));
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{subject}は{target}を{BuildPossessiveNoun(owner, "配下")}から解放した{TranslateEndMark(match.Groups["end"].Value)}",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateEmptyTheClipsClaspMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = EmptyTheClipsClaspPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = NormalizeActor(Restore(match, spans, "subject"));
        var owner = NormalizePossessive(Restore(match, spans, "owner"));
        var target = Restore(match, spans, "target");
        var translatedTarget = string.Equals(target, "pistols", StringComparison.Ordinal)
            ? BuildPossessiveNoun(owner, "ピストル")
            : BuildPossessiveNoun(owner, target);
        var predicate = string.Equals(target, "pistols", StringComparison.Ordinal)
            ? $"{translatedTarget}を熱心に握りしめた"
            : $"{translatedTarget}を嬉々として握りしめた";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            IsSecondPersonSubject(subject)
                ? predicate + TranslateEndMark(match.Groups["end"].Value)
                : $"{subject}は{predicate}{TranslateEndMark(match.Groups["end"].Value)}",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateIllRecoveryMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = IllRecoveryPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = NormalizeActor(Restore(match, spans, "subject"));
        var predicate = "病気が治った" + TranslateEndMark(match.Groups["end"].Value);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            IsSecondPersonSubject(subject) ? predicate : $"{subject}は{predicate}",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateShieldWallMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ShieldWallPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = NormalizeActor(Restore(match, spans, "subject"));
        var owner = NormalizePossessive(Restore(match, spans, "owner"));
        var shield = BuildPossessiveNoun(owner, "盾");
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            IsSecondPersonSubject(subject)
                ? $"{shield}を壁陣形に構えた{TranslateEndMark(match.Groups["end"].Value)}"
                : $"{subject}は{shield}を壁陣形に構えた{TranslateEndMark(match.Groups["end"].Value)}",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateLatchedHeldMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = LatchedHeldPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = NormalizeActor(Restore(match, spans, "subject"));
        var holder = NormalizeActor(Restore(match, spans, "holder"));
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{subject}は{holder}に押さえつけられている！",
            spans,
            stripped.Length,
            source);
        return true;
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

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string NormalizeActor(string source)
    {
        var trimmed = source.Trim();
        return trimmed switch
        {
            "You" or "you" => "あなた",
            "Your" or "your" => "あなたの",
            _ => StringHelpers.StripLeadingEnglishArticle(
                trimmed,
                includeCapitalizedDefiniteArticle: true,
                includeCapitalizedIndefiniteArticle: true),
        };
    }

    private static string NormalizePossessive(string source)
    {
        return source.Trim() switch
        {
            "your" or "Your" => "あなた",
            "its" or "his" or "her" or "their" or "Its" or "His" or "Her" or "Their" => "その",
            var value => NormalizeActor(value),
        };
    }

    private static bool IsSecondPersonSubject(string source)
    {
        return string.Equals(source, "あなた", StringComparison.Ordinal);
    }

    private static string BuildPossessiveNoun(string owner, string noun)
    {
        return string.Equals(owner, "その", StringComparison.Ordinal)
            ? owner + noun
            : owner + "の" + noun;
    }

    private static string TranslateEndMark(string source)
    {
        return source switch
        {
            "!" => "！",
            "?" => "？",
            _ => "。",
        };
    }
}
