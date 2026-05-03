using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TradeUiPopupTranslationPatch
{
    private const string Context = nameof(TradeUiPopupTranslationPatch);
    private const string FreshWaterText = "清水";

    private static readonly Regex CannotCarryPattern = new(
        "^(?<subject>.+?) cannot carry things\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EngagedInMeleePattern = new(
        "^(?<subject>.+?) engaged in melee combat and.+? too busy to trade with you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OnFirePattern = new(
        "^(?<subject>.+?) on fire and.+? too busy to trade with you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HasNothingToTradePattern = new(
        "^(?:\u0002have\u001F\\d+\u001F\\d+\u001F\u0003)?(?<subject>.+?) has nothing to trade\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterDebtPattern = new(
        "^(?<subject>.+?) will not trade with you until you pay (?<them1>.+?) the (?<amount>\\d+) drams? of fresh water you owe (?<them2>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterDebtGiveYourDramsPattern = new(
        "^(?<subject>.+?) will not trade with you until you pay (?<them1>.+?) the (?<amount>\\d+) drams? of fresh water you owe (?<them2>.+?)\\. Do you want to give (?<them3>.+?) your (?<free>\\d+) drams? now\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterDebtGiveItPattern = new(
        "^(?<subject>.+?) will not trade with you until you pay (?<them1>.+?) the (?<amount>\\d+) drams? of fresh water you owe (?<them2>.+?)\\. Do you want to give it to (?<them3>.+?) now\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExplanationPattern = new(
        "^You can't understand (?<owner>.+?) explanation\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IdentifyTooComplexPattern = new(
        "^This item is too complex for (?<trader>.+?) to identify\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IdentifyRequiredDramsPattern = new(
        "^You do not have the required (?<amount>\\d+) drams? to identify this item\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IdentifyQuestionPattern = new(
        "^You may identify this for (?<amount>\\d+) drams? of fresh water\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IdentifyResultPattern = new(
        "^(?<subject>.+?) (?:identify|identifies) (?<item>.+?) as (?<identified>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RepairTooComplexPattern = new(
        "^(?<target>These items are|This item is) too complex for (?<trader>.+?) to repair\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RepairNeedPattern = new(
        "^You need (?<amount>\\d+) drams? of fresh water to repair (?<target>those|that)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RepairQuestionPattern = new(
        "^You may repair (?<target>those|this) for (?<amount>\\d+) drams? of fresh water\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlayerPonyUpQuestionPattern = new(
        "^You'll have to pony up (?<amount>\\d+) drams? of fresh water to even up the trade\\. Agreed\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlayerMissingDramsPattern = new(
        "^You don't have (?<amount>\\d+) drams? of fresh water to even up the trade!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlayerOweMorePattern = new(
        "^You pony up (?<paid>\\d+) drams? of fresh water, and now owe (?<trader>.+?) (?<owed>\\d+) drams?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlayerOweFreshWaterPattern = new(
        "^You now owe (?<trader>.+?) (?<owed>\\d+) drams? of fresh water\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TraderPonyUpQuestionPattern = new(
        "^(?<trader>.+?) will have to pony up (?<amount>\\d+) drams? of fresh water to even up the trade\\. Agreed\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TraderMissingDramsQuestionPattern = new(
        "^(?<trader>.+?) have (?<amount>\\d+) drams? of fresh water to even up the trade! Do you want to complete the trade anyway\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterContainersStorePattern = new(
        "^You don't have enough water containers to carry that many drams! You can store (?<amount>\\d+) drams?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterContainersQuestionPattern = new(
        "^You don't have enough water containers to carry that many drams! Do you want to complete the trade for the (?<amount>\\d+) drams? you can store\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RechargeNeedPattern = new(
        "^You need (?<amount>\\d+) drams? of fresh water to charge (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RechargeQuestionPattern = new(
        "^You may recharge (?<target>.+?) for (?<amount>\\d+) drams? of fresh water\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TryRemovePattern = new(
        "^Trade could not be completed, (?<receiver>.+?) couldn't drop object: (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HaggleCostsPattern = new(
        "^As a result, the trade costs you (?<after>\\d+) drams? rather than (?<before>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HaggleWorthPattern = new(
        "^As a result, the trade is worth (?<after>\\d+) drams? rather than (?<before>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HaggleCostToWorthPattern = new(
        "^As a result, the trade goes from costing you (?<before>\\d+) drams? to being worth (?<after>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HaggleWorthToWorthPattern = new(
        "^As a result, the trade goes from being worth (?<before>\\d+) drams? to being worth (?<after>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new List<MethodBase>();
        var popupType = AccessTools.TypeByName("XRL.UI.Popup");
        if (popupType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRL.UI.Popup.", Context);
            return methods;
        }

        var location2DType = AccessTools.TypeByName("Genkit.Location2D");
        if (location2DType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Genkit.Location2D.", Context);
            return methods;
        }

        var dialogResultType = AccessTools.TypeByName("XRL.UI.DialogResult");
        if (dialogResultType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRL.UI.DialogResult.", Context);
            return methods;
        }

        AddTarget(
            methods,
            AccessTools.Method(
                popupType,
                "Show",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    location2DType,
                }),
            "Popup.Show");

        AddTarget(
            methods,
            AccessTools.Method(
                popupType,
                "ShowBlock",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    location2DType,
                }),
            "Popup.ShowBlock");

        AddTarget(
            methods,
            AccessTools.Method(
                popupType,
                "ShowYesNo",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    dialogResultType,
                }),
            "Popup.ShowYesNo");

        if (methods.Count == 0)
        {
            Trace.TraceError("QudJP: {0} resolved zero target methods.", Context);
        }

        return methods;
    }

    public static void Prefix(object[] __args)
    {
        try
        {
            if (__args.Length == 0 || __args[0] is not string message || string.IsNullOrEmpty(message))
            {
                return;
            }

            __args[0] = TranslatePopupText(message);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static string TranslatePopupText(string source)
    {
        if (TryTranslateTradeUiPopupText(source, out var translated))
        {
            return translated;
        }

        var popupTranslated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, Context);
        if (!string.Equals(popupTranslated, source, StringComparison.Ordinal))
        {
            return popupTranslated;
        }

        return MessagePatternTranslator.Translate(source, Context);
    }

    internal static bool TryTranslateTradeUiPopupText(string source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                CannotCarryPattern,
                "{0} cannot carry things.",
                "TradeUiPopup.CannotCarry",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                HasNothingToTradePattern,
                "{0} has nothing to trade.",
                "TradeUiPopup.HasNothingToTrade",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                EngagedInMeleePattern,
                "{0} engaged in melee combat and is too busy to trade with you.",
                "TradeUiPopup.EngagedInMelee",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                OnFirePattern,
                "{0} on fire and is too busy to trade with you.",
                "TradeUiPopup.OnFire",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                WaterDebtPattern,
                "{0} will not trade with you until you pay {1} the {2} you owe {3}.",
                "TradeUiPopup.WaterDebt",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                    TranslatePronoun(match.Groups["them1"].Value),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount"), emphasizeFreshWater: true),
                    TranslatePronoun(match.Groups["them2"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                WaterDebtGiveYourDramsPattern,
                "{0} will not trade with you until you pay {1} the {2} you owe {3}. Do you want to give {4} your {5} now?",
                "TradeUiPopup.WaterDebtGiveYourDrams",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                    TranslatePronoun(match.Groups["them1"].Value),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount"), emphasizeFreshWater: true),
                    TranslatePronoun(match.Groups["them2"].Value),
                    TranslatePronoun(match.Groups["them3"].Value),
                    FormatDramCount(RestoreCapture(match, spans, "free")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                WaterDebtGiveItPattern,
                "{0} will not trade with you until you pay {1} the {2} you owe {3}. Do you want to give it to {4} now?",
                "TradeUiPopup.WaterDebtGiveIt",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                    TranslatePronoun(match.Groups["them1"].Value),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount"), emphasizeFreshWater: true),
                    TranslatePronoun(match.Groups["them2"].Value),
                    TranslatePronoun(match.Groups["them3"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                ExplanationPattern,
                "You can't understand {0} explanation.",
                "TradeUiPopup.Explanation",
                match => new object[]
                {
                    ToJapanesePossessive(match.Groups["owner"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                IdentifyTooComplexPattern,
                "This item is too complex for {0} to identify.",
                "TradeUiPopup.IdentifyTooComplex",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["trader"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                IdentifyRequiredDramsPattern,
                "You do not have the required {0} to identify this item.",
                "TradeUiPopup.IdentifyRequiredDrams",
                match => new object[]
                {
                    FormatDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                IdentifyQuestionPattern,
                "You may identify this for {0}.",
                "TradeUiPopup.IdentifyQuestion",
                match => new object[]
                {
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                IdentifyResultPattern,
                "{0} identifies {1} as {2}.",
                "TradeUiPopup.IdentifyResult",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["subject"].Value),
                    RestoreCapture(match, spans, "item"),
                    RestoreCapture(match, spans, "identified"),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                RepairTooComplexPattern,
                "{0} are too complex for {1} to repair.",
                "TradeUiPopup.RepairTooComplex",
                match => new object[]
                {
                    TranslateTradeReference(match.Groups["target"].Value),
                    NormalizeSubject(match.Groups["trader"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                RepairNeedPattern,
                "You need {0} to repair {1}.",
                "TradeUiPopup.RepairNeed",
                match => new object[]
                {
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                    TranslateTradeReference(match.Groups["target"].Value),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                RepairQuestionPattern,
                "You may repair {0} for {1}.",
                "TradeUiPopup.RepairQuestion",
                match => new object[]
                {
                    TranslateTradeReference(match.Groups["target"].Value),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                PlayerPonyUpQuestionPattern,
                "You'll have to pony up {0} to even up the trade. Agreed?",
                "TradeUiPopup.PlayerPonyUpQuestion",
                match => new object[]
                {
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                PlayerMissingDramsPattern,
                "You don't have {0} to even up the trade!",
                "TradeUiPopup.PlayerMissingDrams",
                match => new object[]
                {
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                PlayerOweMorePattern,
                "You pony up {0}, and now owe {1} {2}.",
                "TradeUiPopup.PlayerOweMore",
                match => new object[]
                {
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "paid")),
                    NormalizeSubject(match.Groups["trader"].Value),
                    FormatDramCount(RestoreCapture(match, spans, "owed")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                PlayerOweFreshWaterPattern,
                "You now owe {0} {1}.",
                "TradeUiPopup.PlayerOweFreshWater",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["trader"].Value),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "owed")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                TraderPonyUpQuestionPattern,
                "{0} will have to pony up {1} to even up the trade. Agreed?",
                "TradeUiPopup.TraderPonyUpQuestion",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["trader"].Value),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                TraderMissingDramsQuestionPattern,
                "{0} don't have {1} to even up the trade! Do you want to complete the trade anyway?",
                "TradeUiPopup.TraderMissingDramsQuestion",
                match => new object[]
                {
                    NormalizeSubject(match.Groups["trader"].Value),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                WaterContainersStorePattern,
                "You don't have enough water containers to carry that many drams! You can store {0}.",
                "TradeUiPopup.WaterContainersStore",
                match => new object[]
                {
                    FormatDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                WaterContainersQuestionPattern,
                "You don't have enough water containers to carry that many drams! Do you want to complete the trade for the {0} you can store?",
                "TradeUiPopup.WaterContainersQuestion",
                match => new object[]
                {
                    FormatDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                RechargeNeedPattern,
                "You need {0} to charge {1}.",
                "TradeUiPopup.RechargeNeed",
                match => new object[]
                {
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                    TranslateTradeReference(RestoreCapture(match, spans, "target")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                RechargeQuestionPattern,
                "You may recharge {0} for {1}.",
                "TradeUiPopup.RechargeQuestion",
                match => new object[]
                {
                    TranslateTradeReference(RestoreCapture(match, spans, "target")),
                    FormatFreshWaterDramCount(RestoreCapture(match, spans, "amount")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                TryRemovePattern,
                "Trade could not be completed, {0} couldn't drop object: {1}",
                "TradeUiPopup.TryRemove",
                match => new object[]
                {
                    TranslateReceiver(match.Groups["receiver"].Value),
                    RestoreCapture(match, spans, "item"),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                HaggleCostsPattern,
                "As a result, the trade costs you {0} rather than {1}.",
                "TradeUiPopup.HaggleCosts",
                match => new object[]
                {
                    FormatDramCount(RestoreCapture(match, spans, "after")),
                    FormatDramCount(RestoreCapture(match, spans, "before")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                HaggleWorthPattern,
                "As a result, the trade is worth {0} rather than {1}.",
                "TradeUiPopup.HaggleWorth",
                match => new object[]
                {
                    FormatDramCount(RestoreCapture(match, spans, "after")),
                    FormatDramCount(RestoreCapture(match, spans, "before")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                HaggleCostToWorthPattern,
                "As a result, the trade goes from costing you {0} to being worth {1}.",
                "TradeUiPopup.HaggleCostToWorth",
                match => new object[]
                {
                    FormatDramCount(RestoreCapture(match, spans, "before")),
                    FormatDramCount(RestoreCapture(match, spans, "after")),
                },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                HaggleWorthToWorthPattern,
                "As a result, the trade goes from being worth {0} to being worth {1}.",
                "TradeUiPopup.HaggleWorthToWorth",
                match => new object[]
                {
                    FormatDramCount(RestoreCapture(match, spans, "before")),
                    FormatDramCount(RestoreCapture(match, spans, "after")),
                },
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTemplate(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Regex pattern,
        string templateKey,
        string family,
        Func<Match, object[]> argsFactory,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate(templateKey);
        if (string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var visible = string.Format(CultureInfo.InvariantCulture, template, argsFactory(match));
        var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, stripped.Length, visible.Length);
        translated = boundarySpans.Count == 0
            ? visible
            : ColorAwareTranslationComposer.Restore(visible, boundarySpans);

        DynamicTextObservability.RecordTransform(Context, family, source, translated);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return ColorAwareTranslationComposer.RestoreCapture(
            match.Groups[groupName].Value,
            spans,
            match.Groups[groupName]);
    }

    private static string NormalizeSubject(string value)
    {
        var subject = value.Trim();

        subject = StripLeadingArticle(subject);
        subject = StripTrailingSuffix(subject, " doesn't");
        subject = StripTrailingSuffix(subject, " don't");
        subject = StripTrailingSuffix(subject, " does");
        subject = StripTrailingSuffix(subject, " do");
        subject = StripTrailingSuffix(subject, " is");
        subject = StripTrailingSuffix(subject, " are");
        subject = StripTrailingSuffix(subject, " has");
        subject = StripTrailingSuffix(subject, " have");
        subject = StripTrailingSuffix(subject, " pony");
        subject = StripTrailingSuffix(subject, " ponies");
        subject = StripTrailingSuffix(subject, " identify");
        subject = StripTrailingSuffix(subject, " identifies");
        subject = TranslatePronoun(subject);

        return subject.Trim();
    }

    private static string TranslatePronoun(string value)
    {
        return value.Trim() switch
        {
            "you" => "あなた",
            "You" => "あなた",
            "your" => "あなたの",
            "Your" => "あなたの",
            "me" => "私",
            "Me" => "私",
            "I" => "私",
            "my" => "私の",
            "My" => "私の",
            "we" => "私たち",
            "We" => "私たち",
            "us" => "私たち",
            "Us" => "私たち",
            "they" => "彼ら",
            "They" => "彼ら",
            "them" => "彼ら",
            "Them" => "彼ら",
            "he" => "彼",
            "He" => "彼",
            "him" => "彼",
            "Him" => "彼",
            "she" => "彼女",
            "She" => "彼女",
            "her" => "彼女",
            "Her" => "彼女",
            "it" => "それ",
            "It" => "それ",
            _ => value.Trim(),
        };
    }

    private static string TranslateReceiver(string value)
    {
        return TranslatePronoun(StripLeadingArticle(value.Trim()));
    }

    private static string ToJapanesePossessive(string value)
    {
        var owner = value.Trim();
        if (owner.EndsWith("の", StringComparison.Ordinal))
        {
            return owner;
        }

        owner = StripLeadingArticle(owner);
        if (owner.EndsWith("'s", StringComparison.Ordinal))
        {
            return owner.Substring(0, owner.Length - 2) + "の";
        }

        if (owner.EndsWith("s'", StringComparison.Ordinal))
        {
            return owner.Substring(0, owner.Length - 1) + "の";
        }

        return owner + "の";
    }

    private static string TranslateTradeReference(string value)
    {
        return value.Trim() switch
        {
            "This item is" => "この品",
            "These items are" => "これらの品",
            "that" => "それ",
            "this" => "これ",
            "those" => "それら",
            "one of those" => "そのうちの1つ",
            _ => value.Trim(),
        };
    }

    private static string FormatDramCount(string amount)
    {
        return amount + "ドラム";
    }

    private static string FormatFreshWaterDramCount(string amount, bool emphasizeFreshWater = false)
    {
        var liquid = emphasizeFreshWater ? "{{B|" + FreshWaterText + "}}" : FreshWaterText;
        return amount + "ドラムの" + liquid;
    }

    private static string StripLeadingArticle(string value)
    {
        if (value.StartsWith("The ", StringComparison.Ordinal))
        {
            return value.Substring(4);
        }

        if (value.StartsWith("the ", StringComparison.Ordinal))
        {
            return value.Substring(4);
        }

        if (value.StartsWith("An ", StringComparison.Ordinal))
        {
            return value.Substring(3);
        }

        if (value.StartsWith("an ", StringComparison.Ordinal))
        {
            return value.Substring(3);
        }

        if (value.StartsWith("A ", StringComparison.Ordinal))
        {
            return value.Substring(2);
        }

        if (value.StartsWith("a ", StringComparison.Ordinal))
        {
            return value.Substring(2);
        }

        return value;
    }

    private static string StripTrailingSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.Ordinal)
            ? value.Substring(0, value.Length - suffix.Length)
            : value;
    }

    private static void AddTarget(List<MethodBase> targets, MethodBase method, string description)
    {
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve {1}.", Context, description);
            return;
        }

        targets.Add(method);
    }
}
