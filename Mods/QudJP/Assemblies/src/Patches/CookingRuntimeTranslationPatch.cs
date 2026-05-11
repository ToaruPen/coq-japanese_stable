using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CookingRuntimeTranslationPatch
{
    private const string Context = nameof(CookingRuntimeTranslationPatch);

    private static readonly Regex WellFedPopupPattern = new(
        "^(?<intro>.+?)\\n\\n(?<effect>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HitPointsForDayPattern = new(
        "^\\+(?<value>\\d+) hit points? for the rest of the day$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MoveSpeedForDayPattern = new(
        "^(?<value>\\d+) move speed for the rest of the day$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QuicknessForDayPattern = new(
        "^\\+(?<value>\\d+) quickness for the rest of the day$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RandomStatForDayPattern = new(
        "^(?<value>\\d+) (?<stat>Strength|Intelligence|Willpower|Agility|Toughness|Ego) for the rest of the day$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReflectDamageAtTargetPattern = new(
        "^You reflect (?<amount>\\d+) damage back at (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubjectReflectDamageAtYouPattern = new(
        "^(?<subject>.+?) reflects? (?<amount>\\d+) damage back at you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubjectReflectDamageAtTargetPattern = new(
        "^(?<subject>.+?) reflects? (?<amount>\\d+) damage back at (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FateIntervenesPattern = new(
        "^Fate intervenes and you deal no damage to (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RecipeSharedPattern = new(
        "^(?<speaker>.+?) shares? the recipe for (?<recipe>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MetabolizeMealPattern = new(
        "^You start to metabolize the meal, gaining the following effect for the rest of the day:\\n\\n(?<effect>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }
        var enteredElementEventType = AccessTools.TypeByName("XRL.World.Conversations.EnteredElementEvent");

        foreach (var typeName in new[]
        {
            "XRL.World.Effects.BasicCookingEffect_Hitpoints",
            "XRL.World.Effects.BasicCookingEffect_MA",
            "XRL.World.Effects.BasicCookingEffect_MS",
            "XRL.World.Effects.BasicCookingEffect_Quickness",
            "XRL.World.Effects.BasicCookingEffect_ToHit",
            "XRL.World.Effects.BasicCookingEffect_XP",
            "XRL.World.Effects.BasicCookingEffect_Regeneration",
            "XRL.World.Effects.BasicCookingEffect_RandomStat",
        })
        {
            AddTarget(targets, typeName, "ApplyEffect", new[] { gameObjectType });
        }

        AddTarget(targets, "XRL.World.Effects.CookingDomainSpecial_UnitCrystalTransform", "ApplyTo", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.CookingDomainSpecial_UnitSlogTransform", "ApplyTo", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.CookingDomainReflect_UnitReflectDamage", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.CookingDomainTeleport_UnitBlink", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.NoPhase_ProceduralCookingTriggeredAction_Effect", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Skills.Cooking.CookingRecipe", "ApplyEffectsTo", new[] { gameObjectType, typeof(bool) });
        if (enteredElementEventType is not null)
        {
            AddTarget(targets, "XRL.World.Conversations.Parts.WaterRitualCookingRecipe", "HandleEvent", new[] { enteredElementEventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0} failed to resolve EnteredElementEvent.", Context);
        }

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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
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

        if (!TryTranslatePopupCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
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

        if (!TryTranslateQueuedCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslatePopupCore(string source, out string translated)
    {
        translated = source switch
        {
            "You feel an uncomfortable pressure across the length of your body." => "全身に不快な圧迫感を覚える。",
            "Your limbs suddenly feel perversely round and bumpy before they shrink back into your body." => "手足が突然いびつに丸く凹凸を帯びたかと思うと、体の中へ縮み込んだ。",
            "An abrading force cuts the surfaces of your torso until you feel polished and perfectly smooth." => "研磨する力が胴体の表面を削り、やがて磨き上げられたように完全になめらかになった。",
            "A quartet of prisms shatter out of you and grow. Each quinfurcates at the tip into five finger prisms." => "4つのプリズムが体を突き破って成長し、それぞれの先端が5本の指のプリズムへと五分岐した。",
            "You gained the mutation {{C|Crystallinity}}!" => "変異{{C|結晶性}}を得た！",
            "Feelers rip through your scalp and shudder with curiosity." => "触角が頭皮を裂いて生え、好奇心に震えた。",
            "Your arms shrink into your torso." => "腕が胴体の中へ縮み込んだ。",
            "A bilge hose painted with mucus undulates out of your lower body. It spews the amniotic broth of its birth from its sputtering mouth." => "粘液にまみれた排水ホースのようなものが下半身からうねり出て、ぱちぱち鳴る口から生まれた時の羊水めいた汁を吐き散らした。",
            "Your genome has already undergone this transformation." => "あなたのゲノムはすでにこの変化を経ている。",
            "You bounce." => "あなたは跳ねた。",
            "True kin cannot digest this meal." => "トゥルーキンはこの食事を消化できない。",
            "Only true kin can digest this meal." => "この食事を消化できるのはトゥルーキンだけだ。",
            "Mutants cannot digest this meal." => "ミュータントはこの食事を消化できない。",
            "Only mutants can digest this meal." => "この食事を消化できるのはミュータントだけだ。",
            _ => string.Empty,
        };
        if (translated.Length > 0)
        {
            return true;
        }

        return TryTranslateWellFedPopup(source, out translated)
            || TryTranslateRecipeSharedPopup(source, out translated)
            || TryTranslateMetabolizeMealPopup(source, out translated);
    }

    private static bool TryTranslateWellFedPopup(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = WellFedPopupPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var intro = Restore(match, spans, "intro");
        var effect = Restore(match, spans, "effect");
        if (!TryTranslateWellFedIntro(intro, out var translatedIntro)
            || !TryTranslateWellFedEffect(effect, out var translatedEffect))
        {
            translated = source;
            return false;
        }

        translated = translatedIntro + "\n\n" + translatedEffect;
        return true;
    }

    private static bool TryTranslateRecipeSharedPopup(string source, out string translated)
    {
        return TryTranslatePattern(
            RecipeSharedPattern,
            source,
            (match, spans) => Restore(match, spans, "speaker") + "が" + Restore(match, spans, "recipe") + "のレシピを教えてくれた！",
            out translated);
    }

    private static bool TryTranslateMetabolizeMealPopup(string source, out string translated)
    {
        return TryTranslatePattern(
            MetabolizeMealPattern,
            source,
            (match, spans) =>
            {
                var effect = Restore(match, spans, "effect");
                return "食事の代謝が始まり、一日中次の効果を得る:\n\n" + TranslateCookingEffectLines(effect);
            },
            out translated);
    }

    private static string TranslateCookingEffectLines(string source)
    {
        if (source.IndexOf('\n') < 0)
        {
            var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
            return CookingEffectFragmentTranslator.TryTranslate(stripped, Context, "CookingRuntime.ApplyEffectsTo", out var translated)
                ? ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                    translated,
                    spans,
                    stripped.Length,
                    source)
                : source;
        }

        var changed = false;
        var lines = source.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var (stripped, spans) = ColorAwareTranslationComposer.Strip(lines[index]);
            if (CookingEffectFragmentTranslator.TryTranslate(stripped, Context, "CookingRuntime.ApplyEffectsTo", out var translatedLine))
            {
                lines[index] = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                    translatedLine,
                    spans,
                    stripped.Length,
                    lines[index]);
                changed = true;
            }
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static bool TryTranslateWellFedIntro(string source, out string translated)
    {
        translated = source switch
        {
            "You eat the meal. It's tastier than usual." => "食事を食べた。いつもよりおいしい。",
            _ => source,
        };
        return true;
    }

    private static bool TryTranslateWellFedEffect(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslatePattern(
                HitPointsForDayPattern,
                stripped,
                spans,
                match => "一日中、HP+" + match.Groups["value"].Value,
                out translated)
            || TryTranslatePattern(
                MoveSpeedForDayPattern,
                stripped,
                spans,
                match => "一日中、移動速度+" + match.Groups["value"].Value,
                out translated)
            || TryTranslatePattern(
                QuicknessForDayPattern,
                stripped,
                spans,
                match => "一日中、クイックネス+" + match.Groups["value"].Value,
                out translated)
            || TryTranslatePattern(
                RandomStatForDayPattern,
                stripped,
                spans,
                match => "一日中、" + TranslateStat(match.Groups["stat"].Value) + "+" + match.Groups["value"].Value,
                out translated))
        {
            return true;
        }

        translated = stripped switch
        {
            "+1 MA for the rest of the day" => "一日中、MA+1",
            "+1 to hit for the rest of the day" => "一日中、命中+1",
            "+5% XP gained for the rest of the day" => "一日中、獲得XP+5%",
            "+10% to natural healing rate for the rest of the day" => "一日中、自然治癒速度+10%",
            _ => string.Empty,
        };
        if (translated.Length == 0)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateQueuedCore(string source, out string translated)
    {
        if (source == "Your phase remains stable.")
        {
            translated = "あなたの位相は安定したままだ。";
            return true;
        }

        return TryTranslatePattern(
            ReflectDamageAtTargetPattern,
            source,
            (match, spans) => Restore(match, spans, "amount") + "ダメージを" + Restore(match, spans, "target") + "へ反射した。",
            out translated)
            || TryTranslatePattern(
                SubjectReflectDamageAtYouPattern,
                source,
                (match, spans) => Restore(match, spans, "subject") + "は" + Restore(match, spans, "amount") + "ダメージをあなたへ反射した。",
                out translated)
            || TryTranslatePattern(
                SubjectReflectDamageAtTargetPattern,
                source,
                (match, spans) => Restore(match, spans, "subject") + "は" + Restore(match, spans, "amount") + "ダメージを" + Restore(match, spans, "target") + "へ反射した。",
                out translated)
            || TryTranslatePattern(
                FateIntervenesPattern,
                source,
                (match, spans) => "運命が介入し、あなたは" + Restore(match, spans, "target") + "にダメージを与えられなかった。",
                out translated);
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

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Func<Match, string> translate,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = stripped;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match),
            spans,
            stripped.Length);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslateStat(string stat)
    {
        return stat switch
        {
            "Strength" => "筋力",
            "Intelligence" => "知性",
            "Willpower" => "意志力",
            "Agility" => "敏捷性",
            "Toughness" => "耐久力",
            "Ego" => "自我",
            _ => stat,
        };
    }
}
