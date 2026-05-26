using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SifrahTokenDescriptionTranslationPatch
{
    internal const string Context = nameof(SifrahTokenDescriptionTranslationPatch);
    internal const string Family = "SifrahToken.Description";

    private static readonly string[] NoArgumentTokenTypeNames =
    [
        "XRL.World.PsionicSifrahTokenApplyAncientLore",
        "XRL.World.PsionicSifrahTokenApplyIntellect",
        "XRL.World.PsionicSifrahTokenCalmMind",
        "XRL.World.PsionicSifrahTokenDiscipline",
        "XRL.World.PsionicSifrahTokenEffectNosebleed",
        "XRL.World.PsionicSifrahTokenEmpathy",
        "XRL.World.PsionicSifrahTokenExertWill",
        "XRL.World.PsionicSifrahTokenTelepathy",
        "XRL.World.PsionicSifrahTokenTenfoldPathBin",
        "XRL.World.PsionicSifrahTokenTenfoldPathHod",
        "XRL.World.PsionicSifrahTokenTenfoldPathHok",
        "XRL.World.PsionicSifrahTokenTenfoldPathKet",
        "XRL.World.PsionicSifrahTokenTenfoldPathKhu",
        "XRL.World.PsionicSifrahTokenTenfoldPathRet",
        "XRL.World.PsionicSifrahTokenTenfoldPathSed",
        "XRL.World.PsionicSifrahTokenTenfoldPathTza",
        "XRL.World.PsionicSifrahTokenTenfoldPathVur",
        "XRL.World.PsionicSifrahTokenTenfoldPathYis",
        "XRL.World.PsionicSifrahTokenThePowerOfLove",
        "XRL.World.RitualSifrahTokenAttributeSacrifice",
        "XRL.World.RitualSifrahTokenBit",
        "XRL.World.RitualSifrahTokenEffectAsleep",
        "XRL.World.RitualSifrahTokenEffectBleeding",
        "XRL.World.RitualSifrahTokenEffectCardiacArrest",
        "XRL.World.RitualSifrahTokenEffectConfused",
        "XRL.World.RitualSifrahTokenEffectDazed",
        "XRL.World.RitualSifrahTokenEffectDisoriented",
        "XRL.World.RitualSifrahTokenEffectExhausted",
        "XRL.World.RitualSifrahTokenEffectIll",
        "XRL.World.RitualSifrahTokenEffectLost",
        "XRL.World.RitualSifrahTokenEffectPoisoned",
        "XRL.World.RitualSifrahTokenEffectShaken",
        "XRL.World.RitualSifrahTokenEffectShatterMentalArmor",
        "XRL.World.RitualSifrahTokenEffectTerrified",
        "XRL.World.RitualSifrahTokenFood",
        "XRL.World.RitualSifrahTokenGift",
        "XRL.World.RitualSifrahTokenHookah",
        "XRL.World.RitualSifrahTokenInvokeHigherBeing",
        "XRL.World.RitualSifrahTokenItem",
        "XRL.World.RitualSifrahTokenLiquid",
        "XRL.World.RitualSifrahTokenPrayHumbly",
        "XRL.World.RitualSifrahTokenRecountAccomplishments",
        "XRL.World.RitualSifrahTokenScourging",
        "XRL.World.RitualSifrahTokenSingAHistoricalEpic",
        "XRL.World.RitualSifrahTokenSingHymn",
        "XRL.World.RitualSifrahTokenThePowerOfLove",
        "XRL.World.SocialSifrahTokenApplySocialCoprocessor",
        "XRL.World.SocialSifrahTokenBit",
        "XRL.World.SocialSifrahTokenBoastOfAccomplishments",
        "XRL.World.SocialSifrahTokenCharge",
        "XRL.World.SocialSifrahTokenCrackAJoke",
        "XRL.World.SocialSifrahTokenDebateRationally",
        "XRL.World.SocialSifrahTokenDisplayABarathrumiteToken",
        "XRL.World.SocialSifrahTokenDisplayAFarmersToken",
        "XRL.World.SocialSifrahTokenDisplayAMerchantsToken",
        "XRL.World.SocialSifrahTokenDisplayAMinstrelsToken",
        "XRL.World.SocialSifrahTokenEffectLovesick",
        "XRL.World.SocialSifrahTokenEffectShamed",
        "XRL.World.SocialSifrahTokenEmpathy",
        "XRL.World.SocialSifrahTokenFlatterInsincerely",
        "XRL.World.SocialSifrahTokenFlirtSuggestively",
        "XRL.World.SocialSifrahTokenGift",
        "XRL.World.SocialSifrahTokenHookah",
        "XRL.World.SocialSifrahTokenInvokeAncientCompacts",
        "XRL.World.SocialSifrahTokenItem",
        "XRL.World.SocialSifrahTokenLeverageBeingFavored",
        "XRL.World.SocialSifrahTokenLeverageBeingLoved",
        "XRL.World.SocialSifrahTokenLeverageBeingTrueKin",
        "XRL.World.SocialSifrahTokenLiquid",
        "XRL.World.SocialSifrahTokenListenSympathetically",
        "XRL.World.SocialSifrahTokenOfferMaintenanceServices",
        "XRL.World.SocialSifrahTokenPayACompliment",
        "XRL.World.SocialSifrahTokenPostureIntimidatingly",
        "XRL.World.SocialSifrahTokenRailAgainstInjustice",
        "XRL.World.SocialSifrahTokenReadFromTheCanticlesChromaic",
        "XRL.World.SocialSifrahTokenScanning",
        "XRL.World.SocialSifrahTokenSecret",
        "XRL.World.SocialSifrahTokenSociableChat",
        "XRL.World.SocialSifrahTokenSpinATaleOfWoe",
        "XRL.World.SocialSifrahTokenTelepathy",
        "XRL.World.SocialSifrahTokenTellAnInspiringTale",
        "XRL.World.SocialSifrahTokenTenfoldPathSed",
        "XRL.World.SocialSifrahTokenThePowerOfLove",
        "XRL.World.TinkeringSifrahTokenAdvancedToolkit",
        "XRL.World.TinkeringSifrahTokenBit",
        "XRL.World.TinkeringSifrahTokenCharge",
        "XRL.World.TinkeringSifrahTokenComputePower",
        "XRL.World.TinkeringSifrahTokenCopperWire",
        "XRL.World.TinkeringSifrahTokenCreationKnowledge",
        "XRL.World.TinkeringSifrahTokenLiquid",
        "XRL.World.TinkeringSifrahTokenPhysicalManipulation",
        "XRL.World.TinkeringSifrahTokenPsychometry",
        "XRL.World.TinkeringSifrahTokenScanning",
        "XRL.World.TinkeringSifrahTokenTelekinesis",
        "XRL.World.TinkeringSifrahTokenTenfoldPathBin",
        "XRL.World.TinkeringSifrahTokenTenfoldPathHok",
        "XRL.World.TinkeringSifrahTokenToolkit",
        "XRL.World.TinkeringSifrahTokenVisualInspection",
    ];

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var typeName in NoArgumentTokenTypeNames)
        {
            foreach (var constructor in ResolveConstructor(typeName, Type.EmptyTypes))
            {
                yield return constructor;
            }
        }

        foreach (var constructor in ResolveConstructor("XRL.World.TinkeringSifrahTokenLiquid", [typeof(string)]))
        {
            yield return constructor;
        }

        foreach (var constructor in ResolveConstructor("XRL.World.RitualSifrahTokenAttributeSacrifice", [typeof(string)]))
        {
            yield return constructor;
        }

        var worshippableType = AccessTools.TypeByName("XRL.World.Worshippable");
        if (worshippableType is null)
        {
            Trace.TraceError("QudJP: {0} Worshippable type not found.", Context);
            yield break;
        }

        var worshippableListType = typeof(List<>).MakeGenericType(worshippableType);
        foreach (var method in ResolveMethod(
                     "XRL.World.RitualSifrahTokenInvokeHigherBeing",
                     "SetBeing",
                     [worshippableType, worshippableListType]))
        {
            yield return method;
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
            if (__instance is null || !TryGetDescription(__instance, out var source))
            {
                return;
            }

            if (!SifrahTokenDescriptionTranslator.TryTranslateDescription(source, out var translated, out var detail))
            {
                return;
            }

            if (TrySetDescription(__instance, translated))
            {
                DynamicTextObservability.RecordTransform(Context, Family + "." + detail, source, translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static IEnumerable<MethodBase> ResolveConstructor(string typeName, Type[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var constructor = AccessTools.Constructor(targetType, parameterTypes);
        if (constructor is not null)
        {
            yield return constructor;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1} constructor target not found.", Context, targetType.FullName);
    }

    private static IEnumerable<MethodBase> ResolveMethod(string typeName, string methodName, Type[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameterTypes);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryGetDescription(object instance, out string description)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, "Description");
        if (field?.GetValue(instance) is string fieldValue)
        {
            description = fieldValue;
            return true;
        }

        var property = AccessTools.Property(type, "Description");
        if (property?.GetValue(instance) is string propertyValue)
        {
            description = propertyValue;
            return true;
        }

        description = string.Empty;
        return false;
    }

    private static bool TrySetDescription(object instance, string description)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, "Description");
        if (field is not null)
        {
            field.SetValue(instance, description);
            return true;
        }

        var property = AccessTools.Property(type, "Description");
        if (property?.CanWrite == true)
        {
            property.SetValue(instance, description);
            return true;
        }

        return false;
    }
}

internal static class SifrahTokenDescriptionTranslator
{
    private static readonly Regex UseNamedLiquidPattern = new(
        "^use (?<liquid>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SacrificeNamedAttributePattern = new(
        "^sacrifice a point of (?<attribute>Strength|Agility|Toughness|Intelligence|Willpower|Ego)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InvokeBeingPattern = new(
        "^invoke (?<being>.+?)(?:, in the manner of (?<manner>.+))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, (string Translation, string Detail)> ExactDescriptionTranslations = new(StringComparer.Ordinal)
    {
        ["accept a nosebleed starting"] = ("{{r|鼻血}}が始まることを受け入れる", "Exact.AcceptNosebleed"),
        ["accept becoming dazed"] = ("{{C|朦朧}}状態になることを受け入れる", "Exact.AcceptDazed"),
        ["accept becoming exhausted"] = ("{{K|疲労}}状態になることを受け入れる", "Exact.AcceptExhausted"),
        ["accept becoming terrified"] = ("{{W|恐怖}}状態になることを受け入れる", "Exact.AcceptTerrified"),
        ["accept becoming ill"] = ("{{g|病気}}になることを受け入れる", "Exact.AcceptIll"),
        ["accept becoming lovesick"] = ("{{lovesickness|恋煩い}}になることを受け入れる", "Exact.AcceptLovesick"),
        ["accept becoming psionically cleaved"] = ("{{psionic|精神を切り裂かれた}}状態になることを受け入れる", "Exact.AcceptPsionicallyCleaved"),
        ["accept becoming shamed"] = ("{{r|恥辱}}状態になることを受け入れる", "Exact.AcceptShamed"),
        ["accept becoming disoriented"] = ("方向感覚を失うことを受け入れる", "Exact.AcceptDisoriented"),
        ["accept becoming lost"] = ("迷子になることを受け入れる", "Exact.AcceptLost"),
        ["accept becoming shaken"] = ("動揺状態になることを受け入れる", "Exact.AcceptShaken"),
        ["accept beginning to bleed"] = ("{{r|出血}}し始めることを受け入れる", "Exact.AcceptBleeding"),
        ["accept falling asleep"] = ("{{c|睡眠}}に落ちることを受け入れる", "Exact.AcceptAsleep"),
        ["accept going into cardiac arrest"] = ("{{W|心停止}}に陥ることを受け入れる", "Exact.AcceptCardiacArrest"),
        ["apply a toolkit"] = ("ツールキットを使う", "Exact.ApplyToolkit"),
        ["apply ancient lore"] = ("古代の知識を使う", "Exact.ApplyAncientLore"),
        ["apply an advanced toolkit"] = ("高度なツールキットを使う", "Exact.ApplyAdvancedToolkit"),
        ["apply compute power"] = ("計算力を使う", "Exact.ApplyComputePower"),
        ["apply intellect"] = ("知性を使う", "Exact.ApplyIntellect"),
        ["apply knowledge of this artifact's manufacture"] = ("このアーティファクトの製造知識を使う", "Exact.ApplyCreationKnowledge"),
        ["apply social coprocessor"] = ("社交コプロセッサを使う", "Exact.ApplySocialCoprocessor"),
        ["apply the creativity of Hok"] = ("Hokの創造性を使う", "Exact.ApplyHok"),
        ["apply the insights of Bin"] = ("Binの洞察を使う", "Exact.ApplyBin"),
        ["boast of my accomplishments"] = ("自分の偉業を自慢する", "Exact.BoastAccomplishments"),
        ["calm mind"] = ("心を落ち着ける", "Exact.CalmMind"),
        ["crack a joke"] = ("冗談を言う", "Exact.CrackAJoke"),
        ["debate rationally"] = ("理性的に議論する", "Exact.DebateRationally"),
        ["display a Barathrumite token"] = ("バラサルム派のしるしを見せる", "Exact.DisplayBarathrumiteToken"),
        ["display a farmer's token"] = ("農民のしるしを見せる", "Exact.DisplayFarmersToken"),
        ["display a merchant's token"] = ("商人のしるしを見せる", "Exact.DisplayMerchantsToken"),
        ["display a minstrel's token"] = ("吟遊詩人のしるしを見せる", "Exact.DisplayMinstrelsToken"),
        ["draw on reserves of self-discipline"] = ("自制心の蓄えを引き出す", "Exact.DrawDiscipline"),
        ["draw on the authority of Ket"] = ("Ketの権威を引き出す", "Exact.DrawKet"),
        ["draw on the beauty of Ret"] = ("Retの美を引き出す", "Exact.DrawRet"),
        ["draw on the constancy of Tza"] = ("Tzaの不変性を引き出す", "Exact.DrawTza"),
        ["draw on the creativity of Hok"] = ("Hokの創造性を引き出す", "Exact.DrawHok"),
        ["draw on the depths of Yis"] = ("Yisの深遠さを引き出す", "Exact.DrawYis"),
        ["draw on the grace of Sed"] = ("Sedの優雅さを引き出す", "Exact.DrawSed"),
        ["draw on the insights of Bin"] = ("Binの洞察を引き出す", "Exact.DrawBin"),
        ["draw on the majesty of Hod"] = ("Hodの威厳を引き出す", "Exact.DrawHod"),
        ["draw on the might of Vur"] = ("Vurの力を引き出す", "Exact.DrawVur"),
        ["draw on the power of true love"] = ("真実の愛の力を引き出す", "Exact.DrawTrueLove"),
        ["draw on the solidity of Khu"] = ("Khuの堅牢さを引き出す", "Exact.DrawKhu"),
        ["exert will"] = ("意志を振り絞る", "Exact.ExertWill"),
        ["flatter insincerely"] = ("心にもなくお世辞を言う", "Exact.FlatterInsincerely"),
        ["flirt suggestively"] = ("思わせぶりに口説く", "Exact.FlirtSuggestively"),
        ["gift an item"] = ("アイテムを贈る", "Exact.GiftItem"),
        ["gift bit"] = ("ビットを贈る", "Exact.GiftBit"),
        ["invoke a higher being"] = ("高次の存在を呼び出す", "Exact.InvokeHigherBeing"),
        ["invoke ancient compacts"] = ("古代の契約を呼び出す", "Exact.InvokeAncientCompacts"),
        ["leverage being True Kin"] = ("真の人間であることを利用する", "Exact.LeverageTrueKin"),
        ["leverage being favored"] = ("好意を寄せられていることを利用する", "Exact.LeverageFavored"),
        ["leverage being loved"] = ("愛されていることを利用する", "Exact.LeverageLoved"),
        ["leverage telepathy"] = ("テレパシーを利用する", "Exact.LeverageTelepathy"),
        ["listen sympathetically"] = ("共感して耳を傾ける", "Exact.ListenSympathetically"),
        ["manifest the grace of Sed"] = ("Sedの優雅さを顕現させる", "Exact.ManifestSed"),
        ["offer 1 charge from an energy cell"] = ("エネルギーセルから{{C|1}}チャージを提供する", "Exact.OfferOneCharge"),
        ["offer a puff on a hookah"] = ("水タバコの一服を提供する", "Exact.OfferHookahPuff"),
        ["offer an item"] = ("アイテムを差し出す", "Exact.OfferItem"),
        ["offer bit"] = ("ビットを差し出す", "Exact.OfferBit"),
        ["offer food"] = ("食料を差し出す", "Exact.OfferFood"),
        ["offer liquid"] = ("液体を差し出す", "Exact.OfferLiquid"),
        ["offer maintenance services"] = ("保守サービスを申し出る", "Exact.OfferMaintenanceServices"),
        ["pay a compliment"] = ("賛辞を述べる", "Exact.PayCompliment"),
        ["physical manipulation"] = ("物理的操作", "Exact.PhysicalManipulation"),
        ["posture intimidatingly"] = ("威圧的に構える", "Exact.PostureIntimidatingly"),
        ["pray humbly"] = ("謙虚に祈る", "Exact.PrayHumbly"),
        ["psychometric inspection"] = ("サイコメトリー検査", "Exact.PsychometricInspection"),
        ["rail against injustice"] = ("不正義を糾弾する", "Exact.RailAgainstInjustice"),
        ["read from the Canticles Chromaic"] = ("色彩聖歌集から読み上げる", "Exact.ReadCanticlesChromaic"),
        ["recount my accomplishments"] = ("自分の偉業を語る", "Exact.RecountAccomplishments"),
        ["rhapsodize about your true love"] = ("真実の愛について熱く語る", "Exact.RhapsodizeTrueLove"),
        ["scanning"] = ("スキャン", "Exact.Scanning"),
        ["scourge myself with a leather whip"] = ("革の鞭で自らを打つ", "Exact.ScourgeSelf"),
        ["share liquid"] = ("液体を分かち合う", "Exact.ShareLiquid"),
        ["sing a historical epic"] = ("歴史叙事詩を歌う", "Exact.SingHistoricalEpic"),
        ["sing a hymn"] = ("賛歌を歌う", "Exact.SingHymn"),
        ["sociable chat"] = ("社交的な会話", "Exact.SociableChat"),
        ["speak of the nobility of true love"] = ("真実の愛の高貴さを語る", "Exact.SpeakTrueLoveNobility"),
        ["spin a tale of woe"] = ("悲話を語る", "Exact.SpinTaleOfWoe"),
        ["subtly employ empathy"] = ("さりげなく共感力を使う", "Exact.EmployEmpathy"),
        ["subtly employ telepathy"] = ("さりげなくテレパシーを使う", "Exact.EmployTelepathy"),
        ["take a puff on a hookah"] = ("水タバコを一服吸う", "Exact.TakeHookahPuff"),
        ["telekinetic manipulation"] = ("念動操作", "Exact.TelekineticManipulation"),
        ["tell a secret"] = ("秘密を話す", "Exact.TellSecret"),
        ["tell an inspiring tale"] = ("心を奮い立たせる話をする", "Exact.TellInspiringTale"),
        ["use 1 charge from an energy cell"] = ("エネルギーセルから{{C|1}}チャージを使う", "Exact.UseOneCharge"),
        ["use a length of copper wire"] = ("銅線を使う", "Exact.UseCopperWire"),
        ["use bit"] = ("ビットを使う", "Exact.UseBit"),
        ["visual inspection"] = ("目視検査", "Exact.VisualInspection"),
    };

    internal static bool TryTranslateDescription(string source, out string translated, out string detail)
    {
        if (string.IsNullOrEmpty(source) || MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var captureSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        if (!TryTranslateCore(source, stripped, captureSpans, out translated, out detail))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        if (ExactDescriptionTranslations.TryGetValue(stripped, out var exact))
        {
            translated = exact.Translation;
            detail = exact.Detail;
            return true;
        }

        if (string.Equals(stripped, "use liquid", StringComparison.Ordinal))
        {
            translated = "液体を使う";
            detail = "UseLiquid";
            return true;
        }

        var match = UseNamedLiquidPattern.Match(stripped);
        if (match.Success && !match.Groups["liquid"].Value.StartsWith("a ", StringComparison.Ordinal))
        {
            translated = Restore(match, spans, "liquid") + "を使う";
            detail = "UseNamedLiquid";
            return true;
        }

        if (string.Equals(stripped, "sacrifice a point of an attribute", StringComparison.Ordinal))
        {
            translated = "能力値を1ポイント捧げる";
            detail = "SacrificeGenericAttribute";
            return true;
        }

        match = SacrificeNamedAttributePattern.Match(stripped);
        if (match.Success)
        {
            translated = Restore(match, spans, "attribute") + "を1ポイント捧げる";
            detail = "SacrificeNamedAttribute";
            return true;
        }

        match = InvokeBeingPattern.Match(stripped);
        if (match.Success)
        {
            var being = Restore(match, spans, "being");
            var manner = match.Groups["manner"];
            translated = manner.Success
                ? string.Concat(Restore(match, spans, "manner"), "流に", being, "を呼び出す")
                : string.Concat(being, "を呼び出す");
            detail = manner.Success ? "InvokeBeingManner" : "InvokeBeing";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
