using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsDescriptionAssignmentTranslationPatch
{
    private const string Context = nameof(CyberneticsDescriptionAssignmentTranslationPatch);
    private const string MotorizedTreadsFamily = "CyberneticsMotorizedTreads.BodyPart";
    private const string StasisArenaFamily = "CyberneticsStasisArena.Description";
    private const string OpticalMultiscannerFamily = "CyberneticsOpticalMultiscanner.Description";

    private const string OpticalMultiscannerDescription =
        "You gain access to the precise hit point, armor, and dodge values of robotic creatures, biological creatures, and structures.\n"
        + "Staircases and other up/down map transitions are always revealed to you.";

    private const string OpticalMultiscannerTranslatedDescription =
        "ロボット、生物、建造物の正確なヒットポイント、アーマー値、ドッジ値を確認できる。\n"
        + "階段その他の上下マップ遷移は常に明らかになる。";

    private const string OpticalMultiscannerSifrahRule =
        "Adds a bonus turn, and is otherwise useful, in most tinkering Sifrah games, and is useful in many social Sifrah games.";

    private const string OpticalMultiscannerTranslatedSifrahRule =
        "ほとんどのティンカリングのシフラでボーナスターンを得て、その他にも有用になる。また、多くの社交のシフラで有用になる。";

    private const string TechIndexerDescription =
        "You gain access to the precise hit point, armor, and dodge values of robotic creatures.";

    private const string TechIndexerTranslatedDescription =
        "ロボットの正確なヒットポイント、アーマー値、ドッジ値を確認できる。";

    private const string TechIndexerSifrahRule =
        "Adds a bonus turn, and is otherwise useful, in many tinkering Sifrah games, and is useful in some social Sifrah games involving robots.";

    private const string TechIndexerTranslatedSifrahRule =
        "多くのティンカリングのシフラでボーナスターンを得て、その他にも有用になる。また、ロボットに関係する一部の社交のシフラで有用になる。";

    private static readonly Regex StasisArenaPattern = new(
        "^Activated\\. Cooldown (?<cooldown>\\d+)\\.\\n"
        + "Pick an exclusion zone of up to (?<size>\\d+) squares?; "
        + "the rest of the zone, other than the square you are in, is enveloped in stasis fields that last (?<duration>\\d+(?:-\\d+)?) turns\\.\\n"
        + "Compute power on the local lattice increases this implant's effectiveness\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SingleSkillsoftPattern =
        new(
            "^You gain the skill (?<skill>.+)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TreeSkillsoftPattern =
        new(
            "^You gain access to the (?<skill>.+) skill tree\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SocialCoprocessorPattern =
        new(
            "^Whenever you perform the water ritual with a new creature, you gain an extra (?<bonus>\\d+) reputation\\. "
            + "If you install this implant after you treat with a creature for the first time, you gain (?<nextBonus>\\d+) reputation the next time you treat with them\\.\\n"
            + "Reputation costs in the water ritual are reduced by (?<costReduction>\\d+)%\\.\\n"
            + "You may Proselytize (?<limit>\\d+) additional creatures?\\.\\n"
            + "Compute power on the local lattice increases this implant's effectiveness\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var implantedEventType = AccessTools.TypeByName("XRL.World.ImplantedEvent");
        var motorizedTreadsType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsMotorizedTreads");
        if (implantedEventType is not null && motorizedTreadsType is not null)
        {
            var handleEvent = AccessTools.Method(motorizedTreadsType, "HandleEvent", new[] { implantedEventType });
            if (handleEvent is not null)
            {
                yield return handleEvent;
            }
            else
            {
                Trace.TraceError("QudJP: {0} target method not found: CyberneticsMotorizedTreads.HandleEvent(ImplantedEvent).", Context);
            }
        }
        else
        {
            Trace.TraceError("QudJP: {0} target type not found for CyberneticsMotorizedTreads.HandleEvent.", Context);
        }

        var behaviorDescriptionEventType = AccessTools.TypeByName("XRL.World.GetCyberneticsBehaviorDescriptionEvent");
        var stasisArenaType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsStasisArena");
        if (behaviorDescriptionEventType is not null && stasisArenaType is not null)
        {
            var handleEvent = AccessTools.Method(stasisArenaType, "HandleEvent", new[] { behaviorDescriptionEventType });
            if (handleEvent is not null)
            {
                yield return handleEvent;
            }
            else
            {
                Trace.TraceError("QudJP: {0} target method not found: CyberneticsStasisArena.HandleEvent(GetCyberneticsBehaviorDescriptionEvent).", Context);
            }
        }
        else
        {
            Trace.TraceError("QudJP: {0} target type not found for CyberneticsStasisArena.HandleEvent.", Context);
        }

        foreach (var targetTypeName in new[]
                 {
                     "XRL.World.Parts.CyberneticsOpticalMultiscanner",
                     "XRL.World.Parts.CyberneticsSingleSkillsoft",
                     "XRL.World.Parts.CyberneticsTreeSkillsoft",
                     "XRL.World.Parts.CyberneticsSocialCoprocessor",
                     "XRL.World.Parts.CyberneticsTechIndexer",
                 })
        {
            var targetType = AccessTools.TypeByName(targetTypeName);
            if (behaviorDescriptionEventType is not null && targetType is not null)
            {
                var handleEvent = AccessTools.Method(targetType, "HandleEvent", new[] { behaviorDescriptionEventType });
                if (handleEvent is not null)
                {
                    yield return handleEvent;
                }
                else
                {
                    Trace.TraceError("QudJP: {0} target method not found: {1}.HandleEvent(GetCyberneticsBehaviorDescriptionEvent).", Context, targetTypeName);
                }
            }
            else
            {
                Trace.TraceError("QudJP: {0} target type not found for {1}.HandleEvent.", Context, targetTypeName);
            }
        }
    }

    public static void Postfix(object E, MethodBase __originalMethod)
    {
        try
        {
            var declaringType = __originalMethod.DeclaringType?.FullName;
            if (declaringType is null)
            {
                declaringType = string.Empty;
            }
            if (string.Equals(declaringType, "XRL.World.Parts.CyberneticsMotorizedTreads", StringComparison.Ordinal)
                && TryGetMemberValue(E, "Part", out var part)
                && part is not null)
            {
                TranslateMotorizedTreadsPartForTests(part);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.CyberneticsStasisArena", StringComparison.Ordinal))
            {
                TranslateStasisArenaEventForTests(E);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.CyberneticsOpticalMultiscanner", StringComparison.Ordinal))
            {
                TranslateOpticalMultiscannerEventForTests(E);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.CyberneticsSingleSkillsoft", StringComparison.Ordinal))
            {
                TranslateSingleSkillsoftEventForTests(E);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.CyberneticsTreeSkillsoft", StringComparison.Ordinal))
            {
                TranslateTreeSkillsoftEventForTests(E);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.CyberneticsSocialCoprocessor", StringComparison.Ordinal))
            {
                TranslateSocialCoprocessorEventForTests(E);
                return;
            }

            if (string.Equals(declaringType, "XRL.World.Parts.CyberneticsTechIndexer", StringComparison.Ordinal))
            {
                TranslateTechIndexerEventForTests(E);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateMotorizedTreadsPartForTests(object? part)
    {
        if (part is null)
        {
            return;
        }

        TranslateStringMember(part, "Name", MotorizedTreadsFamily + ".Name", static source =>
            string.Equals(source, "lower body", StringComparison.Ordinal) ? "下半身" : source);
        TranslateStringMember(part, "Description", MotorizedTreadsFamily + ".Description", static source =>
            string.Equals(source, "Lower Body", StringComparison.Ordinal) ? "下半身" : source);
    }

    internal static void TranslateStasisArenaEventForTests(object? eventInstance)
    {
        if (eventInstance is null
            || !TryGetStringMemberValue(eventInstance, "Description", out var current)
            || string.IsNullOrEmpty(current))
        {
            return;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(current!, out var markedText))
        {
            TrySetStringMemberValue(eventInstance, "Description", markedText);
            return;
        }

        var translated = TranslateStasisArenaDescription(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        if (TrySetStringMemberValue(eventInstance, "Description", translated))
        {
            DynamicTextObservability.RecordTransform(Context, StasisArenaFamily, current!, translated);
        }
    }

    internal static void TranslateOpticalMultiscannerEventForTests(object? eventInstance)
    {
        if (eventInstance is null)
        {
            return;
        }

        TranslateStringMember(eventInstance, "Description", OpticalMultiscannerFamily, static source =>
            string.Equals(source, OpticalMultiscannerDescription, StringComparison.Ordinal)
                ? OpticalMultiscannerTranslatedDescription
                : source);

        if (!TryGetMemberValue(eventInstance, "ToAdd", out var raw)
            || raw is not IList<string> additions)
        {
            return;
        }

        for (var index = 0; index < additions.Count; index++)
        {
            var current = additions[index];
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(current, out var markedText))
            {
                additions[index] = markedText;
                continue;
            }

            if (!string.Equals(current, OpticalMultiscannerSifrahRule, StringComparison.Ordinal))
            {
                continue;
            }

            additions[index] = OpticalMultiscannerTranslatedSifrahRule;
            DynamicTextObservability.RecordTransform(
                Context,
                OpticalMultiscannerFamily + ".SifrahRule",
                current,
                OpticalMultiscannerTranslatedSifrahRule);
        }
    }

    internal static void TranslateSingleSkillsoftEventForTests(object? eventInstance)
    {
        if (eventInstance is null)
        {
            return;
        }

        TranslateStringMember(eventInstance, "Description", "CyberneticsSingleSkillsoft.Description", TranslateSingleSkillsoftText);
        TranslateAddedRules(eventInstance, "CyberneticsSingleSkillsoft.Add", TranslateSingleSkillsoftText);
    }

    internal static void TranslateTreeSkillsoftEventForTests(object? eventInstance)
    {
        if (eventInstance is null)
        {
            return;
        }

        TranslateStringMember(eventInstance, "Description", "CyberneticsTreeSkillsoft.Description", TranslateTreeSkillsoftText);
        TranslateAddedRules(eventInstance, "CyberneticsTreeSkillsoft.Add", TranslateTreeSkillsoftText);
    }

    internal static void TranslateSocialCoprocessorEventForTests(object? eventInstance)
    {
        if (eventInstance is null)
        {
            return;
        }

        TranslateStringMember(eventInstance, "Description", "CyberneticsSocialCoprocessor.Description", TranslateSocialCoprocessorText);
    }

    internal static void TranslateTechIndexerEventForTests(object? eventInstance)
    {
        if (eventInstance is null)
        {
            return;
        }

        TranslateStringMember(eventInstance, "Description", "CyberneticsTechIndexer.Description", static source =>
            string.Equals(source, TechIndexerDescription, StringComparison.Ordinal)
                ? TechIndexerTranslatedDescription
                : source);
        TranslateAddedRules(eventInstance, "CyberneticsTechIndexer.Add", static source =>
            string.Equals(source, TechIndexerSifrahRule, StringComparison.Ordinal)
                ? TechIndexerTranslatedSifrahRule
                : source);
    }

    private static string TranslateStasisArenaDescription(string source)
    {
        var match = StasisArenaPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        return string.Concat(
            "起動型。クールダウン ",
            match.Groups["cooldown"].Value,
            "。\n最大",
            match.Groups["size"].Value,
            "マスの除外区域を選ぶ。現在いるマスを除くゾーンの残りは、",
            match.Groups["duration"].Value,
            "ターン持続する停滞フィールドに包まれる。\nローカル格子の計算力はこのインプラントの効果を高める。");
    }

    private static string TranslateSingleSkillsoftText(string source)
    {
        var match = SingleSkillsoftPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var skill = CharGenProducerTranslationHelpers.TranslateText(match.Groups["skill"].Value);
        return skill + "スキルを得る。";
    }

    private static string TranslateTreeSkillsoftText(string source)
    {
        var match = TreeSkillsoftPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var skill = CharGenProducerTranslationHelpers.TranslateText(match.Groups["skill"].Value);
        return skill + "スキルツリーにアクセスできる。";
    }

    private static string TranslateSocialCoprocessorText(string source)
    {
        var match = SocialCoprocessorPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var bonus = match.Groups["bonus"].Value;
        var nextBonus = match.Groups["nextBonus"].Value;
        var costReduction = match.Groups["costReduction"].Value;
        var limit = match.Groups["limit"].Value;
        return string.Concat(
            "新しいクリーチャーと水の儀式を行うたび、評判を追加で",
            bonus,
            "得る。クリーチャーと初めて交渉した後にこのインプラントを取り付けた場合、次にその相手と交渉したときに評判を",
            nextBonus,
            "得る。\n水の儀式での評判コストが",
            costReduction,
            "%減少する。\n追加で",
            limit,
            "体のクリーチャーを布教できる。\nローカル格子の計算力はこのインプラントの効果を高める。");
    }

    private static void TranslateAddedRules(object eventInstance, string family, Func<string, string> translate)
    {
        if (!TryGetMemberValue(eventInstance, "ToAdd", out var raw)
            || raw is not IList<string> additions)
        {
            return;
        }

        for (var index = 0; index < additions.Count; index++)
        {
            var current = additions[index];
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(current, out var markedText))
            {
                additions[index] = markedText;
                continue;
            }

            var translated = translate(current);
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                continue;
            }

            additions[index] = translated;
            DynamicTextObservability.RecordTransform(Context, family, current, translated);
        }
    }

    private static void TranslateStringMember(object target, string memberName, string family, Func<string, string> translate)
    {
        if (!TryGetStringMemberValue(target, memberName, out var current)
            || string.IsNullOrEmpty(current))
        {
            return;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(current!, out var markedText))
        {
            TrySetStringMemberValue(target, memberName, markedText);
            return;
        }

        var translated = translate(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        if (TrySetStringMemberValue(target, memberName, translated))
        {
            DynamicTextObservability.RecordTransform(Context, family, current!, translated);
        }
    }

    private static bool TryGetStringMemberValue(object target, string memberName, out string? value)
    {
        value = null;
        if (!TryGetMemberValue(target, memberName, out var raw))
        {
            return false;
        }

        value = raw as string;
        return true;
    }

    private static bool TryGetMemberValue(object target, string memberName, out object? value)
    {
        var type = target.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            value = field.GetValue(target);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            value = property.GetValue(target);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TrySetStringMemberValue(object target, string memberName, string value)
    {
        var type = target.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(target, value);
            return true;
        }

        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(target, value);
            return true;
        }

        Trace.TraceWarning("QudJP: {0} could not set member '{1}' on '{2}'.", Context, memberName, type.FullName);
        return false;
    }
}
