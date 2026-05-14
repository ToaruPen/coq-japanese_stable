using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WaterRitualPopupTranslationPatch
{
    private const string Context = nameof(WaterRitualPopupTranslationPatch);

    private static readonly Regex FormalRitualPromptPattern = new(
        "^Do you want to play a game of Sifrah to perform the formal water ritual with (?<speaker>.+?)\\? The formal ritual can be much more impactful\\. If you do not play the game of Sifrah, the informal water ritual will consume 1 dram of (?<liquid>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NotEnoughLiquidPattern = new(
        "^You don't have enough (?<liquid>.+?) to begin the ritual\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SkillPointIntroPattern = new(
        "^Talking to (?<speaker>.+?) rouses in you an inert truth\\. You once wore the frock of a child\\. You poured salt through the cracks of your fingers, and you watched worlds form\\. Can it be all so simple still\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SkillPointGainPattern = new(
        "^You gained (?<points>.+?) skill points!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringModPattern = new(
        "^(?<speaker>.+?) teaches? you to craft the item modification (?<mod>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringRecipePattern = new(
        "^(?<speaker>.+?) teaches? you to craft (?<recipe>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuySecretNoMoreSecretsPattern = new(
        "^(?<speaker>.+?) has no more secrets to share\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuySecretRecipePattern = new(
        "^(?<speaker>.+?) shares? a recipe with you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuySecretLocationPattern = new(
        "^(?<speaker>.+?) shares? the location of (?<location>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuySecretSultanEventPattern = new(
        "^(?<speaker>.+?) shares? an event from the life of a sultan with you\\.\\n\\n\"(?<gospel>.+)\"$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ReputationTooLowPattern = new(
        "^You don't have a high enough reputation with (?<faction>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PerformRitualPattern = new(
        "^You share your (?<liquid>.+?) with (?<speaker>.+?) and begin the water ritual\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuyItemGiftPattern = new(
        "^(?<speaker>.+?) gifts? you (?<item>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GainMutationPattern = new(
        "^Despite your genetic limitations, (?<speaker>.+?) teaches? you to improvise (?<mutation>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RandomMutationIncompatiblePattern = new(
        "^You can't gain (?<category>physical|mental) mutations\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex JoinPartyPattern = new(
        "^(?<speaker>.+?) joins? you!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NephilimCirclePattern = new(
        "^You receive (?<item>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SellSecretNoMoreReputationPattern = new(
        "^(?<speaker>.+?) can't grant you any more reputation\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var enterElementEventType = AccessTools.TypeByName("XRL.World.Conversations.EnterElementEvent");
        var enteredElementEventType = AccessTools.TypeByName("XRL.World.Conversations.EnteredElementEvent");
        var journalEntryType = AccessTools.TypeByName("Qud.API.IBaseJournalEntry");
        if (enterElementEventType is null || enteredElementEventType is null || journalEntryType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve conversation event or journal entry types.", Context);
            yield break;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualBegin",
                     "HandleEvent",
                     [enterElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualSkillPoint",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualTinkeringRecipe",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualBuySecret",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualBuySecret",
                     "RevealEntry",
                     [journalEntryType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.IWaterRitualPart",
                     "UseReputation",
                     [typeof(string)]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitual",
                     "PerformRitual",
                     Type.EmptyTypes))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualBuyItem",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualGainMutation",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualRandomMutation",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualJoinParty",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualNephilimPacify",
                     "TryGiveCircle",
                     Type.EmptyTypes))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.WaterRitualSellSecret",
                     "HandleEvent",
                     [enteredElementEventType]))
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

        if (TryTranslateCore(source, out translated, out var detail))
        {
            DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + "." + detail, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
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

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        if (TryTranslatePattern(
                FormalRitualPromptPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "speaker")}と正式な水の儀式を行うためにシフラーのゲームをプレイしますか？正式な儀式はより大きな影響をもたらすことがあります。シフラーをプレイしない場合、非正式な水の儀式は{Restore(match, spans, "liquid")}を1ドラム消費します。",
                out translated))
        {
            detail = "FormalRitualPrompt";
            return true;
        }

        if (TryTranslatePattern(
                NotEnoughLiquidPattern,
                source,
                (match, spans) => $"儀式を始めるには{Restore(match, spans, "liquid")}が足りない。",
                out translated))
        {
            detail = "NotEnoughLiquid";
            return true;
        }

        if (TryTranslatePattern(
                SkillPointIntroPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "speaker")}との会話が、あなたの内に眠る真実を呼び覚ました。あなたはかつて子供の上着をまとっていた。指の隙間から塩を注ぎ、世界が形作られるのを見ていた。今もなお、それほど単純でありうるのだろうか？",
                out translated))
        {
            detail = "SkillPointIntro";
            return true;
        }

        if (TryTranslatePattern(
                SkillPointGainPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "points")}スキルポイントを得た！",
                out translated))
        {
            detail = "SkillPointGain";
            return true;
        }

        if (TryTranslatePattern(
                TinkeringModPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "speaker")}がアイテム改造{Restore(match, spans, "mod")}の作り方を教えてくれた。",
                out translated))
        {
            detail = "TinkeringMod";
            return true;
        }

        if (TryTranslatePattern(
                TinkeringRecipePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}が{Restore(match, spans, "recipe")}の作り方を教えてくれた。",
                out translated))
        {
            detail = "TinkeringRecipe";
            return true;
        }

        if (TryTranslatePattern(
                BuySecretNoMoreSecretsPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}にはもう共有できる秘密がない。",
                out translated))
        {
            detail = "BuySecretNoMoreSecrets";
            return true;
        }

        if (TryTranslatePattern(
                BuySecretRecipePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}がレシピを共有してくれた。",
                out translated))
        {
            detail = "BuySecretRecipe";
            return true;
        }

        if (TryTranslatePattern(
                BuySecretLocationPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}が{Restore(match, spans, "location")}の場所を教えてくれた。",
                out translated))
        {
            detail = "BuySecretLocation";
            return true;
        }

        if (TryTranslatePattern(
                BuySecretSultanEventPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "speaker")}がスルタンの生涯の出来事を共有してくれた。\n\n\"{Restore(match, spans, "gospel")}\"",
                out translated))
        {
            detail = "BuySecretSultanEvent";
            return true;
        }

        if (TryTranslatePattern(
                ReputationTooLowPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "faction")}との評判が十分に高くない。",
                out translated))
        {
            detail = "ReputationTooLow";
            return true;
        }

        if (TryTranslatePattern(
                PerformRitualPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}と{Restore(match, spans, "liquid")}を分かち合い、水の儀式を始めた。",
                out translated))
        {
            detail = "PerformRitual";
            return true;
        }

        if (TryTranslatePattern(
                BuyItemGiftPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}が{Restore(match, spans, "item")}を贈ってくれた！",
                out translated))
        {
            detail = "BuyItemGift";
            return true;
        }

        if (TryTranslatePattern(
                GainMutationPattern,
                source,
                (match, spans) => $"遺伝的な制限にもかかわらず、{Restore(match, spans, "speaker")}が{Restore(match, spans, "mutation")}を即興で扱う方法を教えてくれた！",
                out translated))
        {
            detail = "GainMutation";
            return true;
        }

        if (TryTranslatePattern(
                RandomMutationIncompatiblePattern,
                source,
                (match, _) => $"{TranslateMutationCategory(match.Groups["category"].Value)}変異は得られない。",
                out translated))
        {
            detail = "RandomMutationIncompatible";
            return true;
        }

        if (TryTranslatePattern(
                JoinPartyPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}が仲間に加わった！",
                out translated))
        {
            detail = "JoinParty";
            return true;
        }

        if (TryTranslatePattern(
                NephilimCirclePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "item")}を受け取った！",
                out translated))
        {
            detail = "NephilimCircle";
            return true;
        }

        if (TryTranslatePattern(
                SellSecretNoMoreReputationPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "speaker")}はこれ以上評判を与えられない。",
                out translated))
        {
            detail = "SellSecretNoMoreReputation";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string TranslateMutationCategory(string category)
    {
        return string.Equals(category, "mental", StringComparison.OrdinalIgnoreCase) ? "精神" : "肉体";
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
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
