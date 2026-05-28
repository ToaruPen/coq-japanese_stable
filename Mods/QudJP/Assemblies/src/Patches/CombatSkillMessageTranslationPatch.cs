using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CombatSkillMessageTranslationPatch
{
    private const string Context = nameof(CombatSkillMessageTranslationPatch);

    private static readonly IReadOnlyDictionary<string, string> CombatSkillCaptureTranslations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["armor"] = "装甲",
            ["shield slam"] = "シールドスラム",
        };

    private static readonly Regex KickPassesThroughYouPattern = new(
        "^(?<actor>.+?) kicks? at you, but the kick passes through you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YouKickPassesThroughPattern = new(
        "^You kick at (?<target>.+?), but the kick passes through (?<pronoun>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorKickPassesThroughPattern = new(
        "^(?<actor>.+?) kicks? at (?<target>.+?), but the kick passes through (?<pronoun>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex KickAtYouHoldGroundPattern = new(
        "^(?<actor>.+?) kicks? at you, but you hold your ground\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YouKickTargetHoldsGroundPattern = new(
        "^You kick at (?<target>.+?), but (?<holder>.+?) holds? (?<possessive>.+?) ground\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorKickTargetHoldsGroundPattern = new(
        "^(?<actor>.+?) kicks? at (?<target>.+?), but (?<holder>.+?) holds? (?<possessive>.+?) ground\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YouKickBackPattern = new(
        "^You kick (?<target>.+?) backwards\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorKicksYouBackPattern = new(
        "^(?<actor>.+?) kicks? you backwards\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorKicksTargetBackPattern = new(
        "^(?<actor>.+?) kicks? (?<target>.+?) backwards\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ChargeCleavePattern = new(
        "^The momentum from your charge causes your (?<weapon>.+?) to cleave deeper through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YouCleavePattern = new(
        "^You cleave through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorCleavesYourPattern = new(
        "^(?<actor>.+?) cleaves? through your (?<part>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorCleavesTargetPattern = new(
        "^(?<actor>.+?) cleaves? through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShookOffStunPattern = new(
        "^(?<actor>.+?) shook off the stun\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShookOffDazingPattern = new(
        "^(?<actor>.+?) shook off the dazing\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SupernalStatePattern = new(
        "^A supernal force helps you shake off being (?<state>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BackswingPattern = new(
        "^You backswing with (?<weapon>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CudgelSmashUpPreparePattern = new(
        "^You prepare (?<weapon>.+?) for demolition\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorBackswingPattern = new(
        "^(?<actor>.+?) backswings? with (?<weapon>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorResistsShieldSlamPattern = new(
        "^(?<actor>.+?) resists? your shield slam\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YouResistShieldSlamPattern = new(
        "^You resist (?<slam>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RejoinderPattern = new(
        "^You rejoinder with (?<weapon>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorRejoinderPattern = new(
        "^(?<actor>.+?) rejoinders? with (?<weapon>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorDrawsBeadOnYouMarkedPattern = new(
        "^The (?<actor>.+?) draws? a bead on you\\. (?:.+?)?You are marked\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var beforeFireMissileWeaponsEventType = AccessTools.TypeByName("XRL.World.BeforeFireMissileWeaponsEvent");
        var applyEffectEventType = AccessTools.TypeByName("XRL.World.ApplyEffectEvent");
        var endTurnEventType = AccessTools.TypeByName("XRL.World.EndTurnEvent");
        if (eventType is null
            || gameObjectType is null
            || cellType is null
            || beforeFireMissileWeaponsEventType is null
            || applyEffectEventType is null
            || endTurnEventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve required event or GameObject types.", Context);
            yield break;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Tactics_Kickback",
                     "HandleEvent",
                     [beforeFireMissileWeaponsEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Axe_Cleave",
                     "PerformCleave",
                     [gameObjectType, gameObjectType, gameObjectType, typeof(string), typeof(string), typeof(int), typeof(int), typeof(int?)]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Endurance_ShakeItOff",
                     "FireEvent",
                     [eventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Cudgel_Backswing",
                     "FireEvent",
                     [eventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Cudgel_SmashUp",
                     "FireEvent",
                     [eventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Discipline_IronMind",
                     "FireEvent",
                     [eventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Rifle_DrawABead",
                     "ValidateMark",
                     Type.EmptyTypes))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Rifle_DrawABead",
                     "SetMark",
                     [gameObjectType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.Shield_Slam",
                     "Slam",
                     [gameObjectType, gameObjectType, cellType, typeof(bool)]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.ShortBlades_Rejoinder",
                     "FireEvent",
                     [eventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.TenfoldPath_Ret",
                     "HandleEvent",
                     [applyEffectEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTargets(
                     "XRL.World.Parts.Skill.TenfoldPath_Ret",
                     "HandleEvent",
                     [endTurnEventType]))
        {
            yield return method;
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

        if (!TryTranslateCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTargets(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        if (TryTranslateFixed(source, out translated))
        {
            return true;
        }

        return TryTranslatePattern(
            KickPassesThroughYouPattern,
            source,
            (match, spans) => $"{Restore(match, spans, "actor")}があなたを蹴ろうとしたが、蹴りはあなたを通り抜けた。",
            out translated)
            || TryTranslatePattern(
                YouKickPassesThroughPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}を蹴ろうとしたが、蹴りは{Restore(match, spans, "pronoun")}を通り抜けた。",
                out translated)
            || TryTranslatePattern(
                ActorKickPassesThroughPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}が{Restore(match, spans, "target")}を蹴ろうとしたが、蹴りは{Restore(match, spans, "pronoun")}を通り抜けた。",
                out translated)
            || TryTranslatePattern(
                KickAtYouHoldGroundPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}があなたを蹴ろうとしたが、あなたは踏みとどまった。",
                out translated)
            || TryTranslatePattern(
                YouKickTargetHoldsGroundPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}を蹴ろうとしたが、{Restore(match, spans, "holder")}は踏みとどまった。",
                out translated)
            || TryTranslatePattern(
                ActorKickTargetHoldsGroundPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}が{Restore(match, spans, "target")}を蹴ろうとしたが、{Restore(match, spans, "holder")}は踏みとどまった。",
                out translated)
            || TryTranslatePattern(
                YouKickBackPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}を後ろへ蹴り飛ばした。",
                out translated)
            || TryTranslatePattern(
                ActorKicksYouBackPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}があなたを後ろへ蹴り飛ばした。",
                out translated)
            || TryTranslatePattern(
                ActorKicksTargetBackPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}が{Restore(match, spans, "target")}を後ろへ蹴り飛ばした。",
                out translated)
            || TryTranslatePattern(
                ChargeCleavePattern,
                source,
                (match, spans) => $"突撃の勢いで{Restore(match, spans, "weapon")}が{Restore(match, spans, "target")}をさらに深く切り裂いた。",
                out translated)
            || TryTranslatePattern(
                YouCleavePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}を切り裂いた。",
                out translated)
            || TryTranslatePattern(
                ActorCleavesYourPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}があなたの{Restore(match, spans, "part")}を切り裂いた。",
                out translated)
            || TryTranslatePattern(
                ActorCleavesTargetPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}が{Restore(match, spans, "target")}を切り裂いた。",
                out translated)
            || TryTranslatePattern(
                ShookOffStunPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}はスタンを振り払った。",
                out translated)
            || TryTranslatePattern(
                ShookOffDazingPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}は朦朧を振り払った。",
                out translated)
            || TryTranslatePattern(
                SupernalStatePattern,
                source,
                (match, spans) => $"超自然的な力が{Restore(match, spans, "state")}状態を振り払う助けとなった！",
                out translated)
            || TryTranslatePattern(
                BackswingPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "weapon")}で返し打ちした。",
                out translated)
            || TryTranslatePattern(
                CudgelSmashUpPreparePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "weapon")}を破壊のために構えた。",
                out translated)
            || TryTranslatePattern(
                ActorBackswingPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}が{Restore(match, spans, "weapon")}で返し打ちした。",
                out translated)
            || TryTranslatePattern(
                ActorResistsShieldSlamPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}はあなたのシールドスラムに抵抗した。",
                out translated)
            || TryTranslatePattern(
                YouResistShieldSlamPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "slam")}に抵抗した。",
                out translated)
            || TryTranslatePattern(
                RejoinderPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "weapon")}で反撃した。",
                out translated)
            || TryTranslatePattern(
                ActorRejoinderPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}が{Restore(match, spans, "weapon")}で反撃した。",
                out translated)
            || TryTranslatePattern(
                ActorDrawsBeadOnYouMarkedPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "actor")}があなたに照準を合わせた。あなたはマークされた。",
                out translated);
    }

    private static bool TryTranslateFixed(string source, out string translated)
    {
        translated = source switch
        {
            "You shook off the stun." => "スタンを振り払った。",
            "You shook off the dazing." => "朦朧を振り払った。",
            "A supernal force helps you shake off the effect!" => "超自然的な力が効果を振り払う助けとなった！",
            "A supernal force helps you shake off a mental state!" => "超自然的な力が精神状態を振り払う助けとなった！",
            "You muster your will and shake off some of your confusion." => "意志の力で混乱の一部を振り払った。",
            "You muster your will and shake off your confusion." => "意志の力で混乱を振り払った。",
            "You lose sight of your mark." => "標的を見失った。",
            "Your tracking of your mark has been disrupted." => "印付けの追跡が乱された。",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        return ColorAwareTranslationComposer.TranslatePreservingColors(restored, NormalizeCombatLabel);
    }

    private static string NormalizeCombatLabel(string label)
    {
        var normalized = StringHelpers.StripLeadingEnglishArticle(
            label,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);

        normalized = ReplaceLeadingPossessive(normalized, "your ", "あなたの");
        normalized = ReplaceLeadingPossessive(normalized, "Your ", "あなたの");
        normalized = ReplaceLeadingPossessive(normalized, "its ", "その");
        normalized = ReplaceLeadingPossessive(normalized, "Its ", "その");
        normalized = ReplaceLeadingPossessive(normalized, "their ", "その");
        normalized = ReplaceLeadingPossessive(normalized, "Their ", "その");
        normalized = ReplaceLeadingPossessive(normalized, "his ", "その");
        normalized = ReplaceLeadingPossessive(normalized, "His ", "その");
        normalized = ReplaceLeadingPossessive(normalized, "her ", "その");
        normalized = ReplaceLeadingPossessive(normalized, "Her ", "その");

        var possessiveIndex = normalized.IndexOf("'s ", StringComparison.Ordinal);
        if (possessiveIndex > 0)
        {
            var owner = normalized.Substring(0, possessiveIndex);
            var owned = normalized.Substring(possessiveIndex + 3);
            if (owner.Length > 0 && owned.Length > 0)
            {
                normalized = owner + "の" + owned;
            }
        }

        return TranslateKnownCombatSkillCapture(normalized);
    }

    private static string TranslateKnownCombatSkillCapture(string source)
    {
        if (CombatSkillCaptureTranslations.TryGetValue(source, out var exact))
        {
            return exact;
        }

        var possessiveIndex = source.LastIndexOf("の", StringComparison.Ordinal);
        if (possessiveIndex <= 0 || possessiveIndex == source.Length - 1)
        {
            return source;
        }

        var owner = source.Substring(0, possessiveIndex + 1);
        var owned = source.Substring(possessiveIndex + 1);
        return CombatSkillCaptureTranslations.TryGetValue(owned, out var translatedOwned)
            ? owner + translatedOwned
            : source;
    }

    private static string ReplaceLeadingPossessive(string source, string possessive, string replacement)
    {
        return source.StartsWith(possessive, StringComparison.Ordinal)
            ? replacement + source.Substring(possessive.Length)
            : source;
    }
}
