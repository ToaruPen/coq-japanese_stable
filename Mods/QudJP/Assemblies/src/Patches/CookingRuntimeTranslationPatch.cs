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

    private static readonly Regex ProceduralCookingSubjectNotificationPattern = new(
        "^(?<subject>You|It|They|He|She) (?<body>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProceduralCookingPossessiveNotificationPattern = new(
        "^(?<possessive>Your|Its|Their|His|Her) (?<body>wounds heal significantly\\.|wounds heal a bit\\.|muscles bulge\\.)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProceduralCookingPlantsBurgeonPattern = new(
        "^Plants burgeon around (?<target>you|it|them|him|her)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ProceduralCookingSubjectPredicates = new(StringComparer.Ordinal)
    {
        ["assume a limber pose."] = "しなやかな構えを取った。",
        ["assumes a limber pose."] = "しなやかな構えを取った。",
        ["perform an act of nimble violence."] = "俊敏な暴力行為を行った。",
        ["performs an act of nimble violence."] = "俊敏な暴力行為を行った。",
        ["stiffen."] = "硬直した。",
        ["stiffens."] = "硬直した。",
        ["become fortified against the cold."] = "冷気への耐性が高まった。",
        ["becomes fortified against the cold."] = "冷気への耐性が高まった。",
        ["freeze an area with cryokinesis."] = "冷却念力で一帯を凍らせた。",
        ["freezes an area with cryokinesis."] = "冷却念力で一帯を凍らせた。",
        ["emit a powerful ray of frost."] = "強力な冷気の光線を放った。",
        ["emits a powerful ray of frost."] = "強力な冷気の光線を放った。",
        ["become impervious to cold."] = "冷気が効かなくなった。",
        ["becomes impervious to cold."] = "冷気が効かなくなった。",
        ["release a powerful electrical discharge."] = "強力な放電を放った。",
        ["releases a powerful electrical discharge."] = "強力な放電を放った。",
        ["release a powerful electromagnetic pulse."] = "強力な電磁パルスを放った。",
        ["releases a powerful electromagnetic pulse."] = "強力な電磁パルスを放った。",
        ["become impervious to electrical damage."] = "電撃ダメージが効かなくなった。",
        ["becomes impervious to electrical damage."] = "電撃ダメージが効かなくなった。",
        ["become fortified against electrical damage."] = "電撃ダメージへの耐性が高まった。",
        ["becomes fortified against electrical damage."] = "電撃ダメージへの耐性が高まった。",
        ["become immune to fear."] = "恐怖に免疫を得た。",
        ["becomes immune to fear."] = "恐怖に免疫を得た。",
        ["intimidate everyone around you."] = "周囲の全員を威圧した。",
        ["intimidates everyone around it."] = "周囲の全員を威圧した。",
        ["intimidate everyone around them."] = "周囲の全員を威圧した。",
        ["intimidates everyone around him."] = "周囲の全員を威圧した。",
        ["intimidates everyone around her."] = "周囲の全員を威圧した。",
        ["become impervious to fungal spores."] = "真菌の胞子が効かなくなった。",
        ["becomes impervious to fungal spores."] = "真菌の胞子が効かなくなった。",
        ["heal to full."] = "完全に回復した。",
        ["heals to full."] = "完全に回復した。",
        ["become heartier."] = "より頑健になった。",
        ["becomes heartier."] = "より頑健になった。",
        ["emit a powerful ray of flame."] = "強力な火炎の光線を放った。",
        ["emits a powerful ray of flame."] = "強力な火炎の光線を放った。",
        ["become fortified against the heat."] = "熱への耐性が高まった。",
        ["becomes fortified against the heat."] = "熱への耐性が高まった。",
        ["become impervious to the heat."] = "熱が効かなくなった。",
        ["becomes impervious to the heat."] = "熱が効かなくなった。",
        ["toast an area with pyrokinesis."] = "発火念力で一帯を焼いた。",
        ["toasts an area with pyrokinesis."] = "発火念力で一帯を焼いた。",
        ["feel less afflicted."] = "苦痛が和らいだ。",
        ["feels less afflicted."] = "苦痛が和らいだ。",
        ["feel the swell of love inside."] = "内側に愛が満ちるのを感じた。",
        ["feels the swell of love inside."] = "内側に愛が満ちるのを感じた。",
        ["become impervious to disease."] = "病気が効かなくなった。",
        ["becomes impervious to disease."] = "病気が効かなくなった。",
        ["feel like you might be fighting off any ailments you have."] = "病を撃退できそうな気がした。",
        ["feels like you might be fighting off any ailments you have."] = "病を撃退できそうな気がした。",
        ["feel better."] = "気分が良くなった。",
        ["feels better."] = "気分が良くなった。",
        ["become phase-anchored."] = "位相固定された。",
        ["becomes phase-anchored."] = "位相固定された。",
        ["phase out."] = "位相が外れた。",
        ["phases out."] = "位相が外れた。",
        ["teleport."] = "テレポートした。",
        ["teleports."] = "テレポートした。",
        ["teleport all creatures surrounding you."] = "周囲のすべてのクリーチャーをテレポートさせた。",
        ["teleports all creatures surrounding it."] = "周囲のすべてのクリーチャーをテレポートさせた。",
        ["teleport all creatures surrounding them."] = "周囲のすべてのクリーチャーをテレポートさせた。",
        ["teleports all creatures surrounding him."] = "周囲のすべてのクリーチャーをテレポートさせた。",
        ["teleports all creatures surrounding her."] = "周囲のすべてのクリーチャーをテレポートさせた。",
        ["expel a blast of quills."] = "針の突風を放った。",
        ["expels a blast of quills."] = "針の突風を放った。",
        ["grow spines all over your body."] = "全身に棘が生えた。",
        ["grows spines all over your body."] = "全身に棘が生えた。",
        ["grow tiny spines all over your body."] = "全身に小さな棘が生えた。",
        ["grows tiny spines all over your body."] = "全身に小さな棘が生えた。",
        ["feel a overwhelming springiness inside."] = "内側に圧倒的な弾力を感じた。",
        ["feels a overwhelming springiness inside."] = "内側に圧倒的な弾力を感じた。",
        ["feel a springiness inside."] = "内側に弾力を感じた。",
        ["perform an act of brutal violence."] = "残忍な暴力行為を行った。",
        ["performs an act of brutal violence."] = "残忍な暴力行為を行った。",
        ["stop bleeding."] = "出血が止まった。",
        ["stops bleeding."] = "出血が止まった。",
        ["shoot out a trio of sticky tongues."] = "粘つく舌を三本撃ち出した。",
        ["shoots out a trio of sticky tongues."] = "粘つく舌を三本撃ち出した。",
        ["don't thirst."] = "喉が渇かなくなった。",
        ["don't thirst for the next 12 hours."] = "次の12時間喉が渇かなくなった。",
    };

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var beforeApplyDamageEventType = AccessTools.TypeByName("XRL.World.BeforeApplyDamageEvent");
        var damageType = AccessTools.TypeByName("XRL.World.Damage");
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
        if (beforeApplyDamageEventType is not null)
        {
            AddTarget(targets, "XRL.World.Parts.ReflectDamage", "HandleEvent", new[] { beforeApplyDamageEventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0} failed to resolve BeforeApplyDamageEvent.", Context);
        }
        if (damageType is not null)
        {
            AddTarget(targets, "XRL.World.Parts.ModBlinkEscape", "CheckBlinkEscape", new[] { gameObjectType, gameObjectType, damageType });
        }
        else
        {
            Trace.TraceError("QudJP: {0} failed to resolve Damage.", Context);
        }
        AddTarget(targets, "XRL.World.Effects.CookingDomainTeleport_UnitBlink", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.NoPhase_ProceduralCookingTriggeredAction_Effect", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.ProceduralCookingEffectWithTrigger", "Trigger", Type.EmptyTypes);
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

    internal static bool TryTranslateMetabolizeMealPopup(string source, out string translated)
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
        var (effectStripped, effectSpans) = ColorAwareTranslationComposer.Strip(source);
        if (source.IndexOf('\n') < 0)
        {
            return CookingEffectFragmentTranslator.TryTranslate(effectStripped, Context, "CookingRuntime.ApplyEffectsTo", out var translated)
                ? ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                    translated,
                    effectSpans,
                    effectStripped.Length,
                    source)
                : source;
        }

        var changed = false;
        var lines = effectStripped.Split('\n');
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

        if (!changed)
        {
            return source;
        }

        var translatedLines = string.Join("\n", lines);
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedLines,
            effectSpans,
            effectStripped.Length,
            source);
    }

    private static bool TryTranslateWellFedIntro(string source, out string translated)
    {
        translated = source switch
        {
            "You eat the meal. It's tastier than usual." => "食事を食べた。いつもよりおいしい。",
            "You gorge on the succulent meat. It's tastier than usual." => "瑞々しい肉を貪った。いつもよりおいしい。",
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
        if (TryTranslateProceduralCookingTriggerNotification(source, out translated))
        {
            return true;
        }

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

    private static bool TryTranslateProceduralCookingTriggerNotification(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        if (!TryTranslateProceduralCookingTriggerNotificationCore(stripped, out var coreTranslated))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            coreTranslated,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateProceduralCookingTriggerNotificationCore(string source, out string translated)
    {
        var subjectMatch = ProceduralCookingSubjectNotificationPattern.Match(source);
        if (subjectMatch.Success
            && ProceduralCookingSubjectPredicates.TryGetValue(subjectMatch.Groups["body"].Value, out var predicate))
        {
            translated = TranslateProceduralCookingSubject(subjectMatch.Groups["subject"].Value) + "は" + predicate;
            return true;
        }

        var possessiveMatch = ProceduralCookingPossessiveNotificationPattern.Match(source);
        if (possessiveMatch.Success)
        {
            var possessive = TranslateProceduralCookingPossessive(possessiveMatch.Groups["possessive"].Value);
            translated = possessiveMatch.Groups["body"].Value switch
            {
                "wounds heal significantly." => possessive + "傷が大きく癒えた。",
                "wounds heal a bit." => possessive + "傷が少し癒えた。",
                "muscles bulge." => possessive + "筋肉が膨れ上がった。",
                _ => source,
            };
            return !string.Equals(translated, source, StringComparison.Ordinal);
        }

        var plantsMatch = ProceduralCookingPlantsBurgeonPattern.Match(source);
        if (plantsMatch.Success)
        {
            translated = TranslateProceduralCookingObject(plantsMatch.Groups["target"].Value) + "の周囲に植物が芽吹いた！";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateProceduralCookingSubject(string subject)
    {
        return subject switch
        {
            "You" => "あなた",
            "It" => "それ",
            "They" => "それら",
            "He" => "彼",
            "She" => "彼女",
            _ => subject,
        };
    }

    private static string TranslateProceduralCookingPossessive(string possessive)
    {
        return possessive switch
        {
            "Your" => "あなたの",
            "Its" => "それの",
            "Their" => "それらの",
            "His" => "彼の",
            "Her" => "彼女の",
            _ => possessive,
        };
    }

    private static string TranslateProceduralCookingObject(string target)
    {
        return target switch
        {
            "you" => "あなた",
            "it" => "それ",
            "them" => "それら",
            "him" => "彼",
            "her" => "彼女",
            _ => target,
        };
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
