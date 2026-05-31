using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RandomAltarBaetylTranslationPatch
{
    private const string Context = nameof(RandomAltarBaetylTranslationPatch);

    private static readonly Regex RewardPopupPattern = new(
        "^I ACCEPT YOUR OFFERING!\\n\\nThe sparking baetyl gives you (?<reward>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DemandPopupPattern = new(
        "^PETTY MORTAL! BRING ME (?<demand>.+?), AND I SHALL REWARD YOU WITH (?<reward>.+?)\\.(?:\\n\\nOffer (?<baetyl>the sparking baetyl|sparking baetyl|.+?) (?:(?<count>\\d+) out of )?(?<offering>.+?)(?<nearby> nearby)?\\?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex QuantityDemandPattern = new(
        "^(?<count>\\d+)\\s+(?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baetylType = AccessTools.TypeByName("XRL.World.Parts.RandomAltarBaetyl");
        if (baetylType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var wantsSacrifice = AccessTools.Method(baetylType, "BaetylWantsSacrifice", []);
        if (wantsSacrifice is not null)
        {
            yield return wantsSacrifice;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.BaetylWantsSacrifice() not found.", Context);
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateRewardPopup(source, stripped, spans, out translated))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + ".RandomAltarBaetylRewardPopup",
                source,
                translated);
            return true;
        }

        if (TryTranslateDemandPopup(source, stripped, spans, out translated))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + ".RandomAltarBaetylDemandPopup",
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateRewardPopup(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = RewardPopupPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"捧げ物を受け取った！\n\n火花を散らすベテルは{RestoreReward(match, spans)}を授けた！",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateDemandPopup(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = DemandPopupPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var demand = TranslateDemandPhrase(Restore(match, spans, "demand"));
        var reward = TranslateDisplayNameCapture(Restore(match, spans, "reward"));
        var body = $"矮小なる凡人よ！{demand}を持ってこい。そうすれば{reward}を授けよう。";
        if (match.Groups["offering"].Success)
        {
            body += "\n\n" + TranslateOfferConfirmation(match, spans);
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            body,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string TranslateOfferConfirmation(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var baetyl = TranslateBaetylName(Restore(match, spans, "baetyl"));
        var offering = TranslateOfferingPhrase(
            Restore(match, spans, "offering"),
            match.Groups["nearby"].Success);
        var count = match.Groups["count"];

        if (count.Success)
        {
            return $"{offering}のうち{count.Value}個を{baetyl}に捧げますか？";
        }

        return $"{offering}を{baetyl}に捧げますか？";
    }

    private static string TranslateOfferingPhrase(string source, bool isNearby)
    {
        if (TryTranslateMixedNearbyAndInventoryOffering(source, out var mixedOffering))
        {
            return mixedOffering;
        }

        var translated = TranslateDisplayNameCaptureWithPluralFallback(StripLeadingInventoryPrefixPreservingColors(source));
        return isNearby ? "近くの" + translated : translated;
    }

    private static bool TryTranslateMixedNearbyAndInventoryOffering(string source, out string translated)
    {
        const string separator = " nearby and ";
        var separatorIndex = source.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            translated = string.Empty;
            return false;
        }

        var nearbySource = source.Substring(0, separatorIndex);
        var inventorySource = source.Substring(separatorIndex + separator.Length);
        if (string.IsNullOrWhiteSpace(nearbySource) || string.IsNullOrWhiteSpace(inventorySource))
        {
            translated = string.Empty;
            return false;
        }

        var nearby = TranslateDisplayNameCaptureWithPluralFallback(StripLeadingInventoryPrefixPreservingColors(nearbySource));
        var inventory = TranslateDisplayNameCaptureWithPluralFallback(StripLeadingInventoryPrefixPreservingColors(inventorySource));
        translated = $"近くの{nearby}と{inventory}";
        return true;
    }

    private static string StripLeadingInventoryPrefixPreservingColors(string source)
    {
        var trimmed = source.Trim();
        const string inventoryPrefix = "your ";
        var visible = ColorAwareTranslationComposer.GetVisibleText(trimmed);
        if (!visible.StartsWith(inventoryPrefix, StringComparison.Ordinal))
        {
            return trimmed;
        }

        return RemoveVisiblePrefixPreservingTokens(trimmed, inventoryPrefix.Length);
    }

    private static string RemoveVisiblePrefixPreservingTokens(string source, int visiblePrefixLength)
    {
        var result = new StringBuilder(source.Length);
        var index = 0;
        var visibleToRemove = visiblePrefixLength;
        while (index < source.Length)
        {
            if (TryReadMarkupToken(source, index, out var tokenLength))
            {
                result.Append(source, index, tokenLength);
                index += tokenLength;
                continue;
            }

            if (visibleToRemove > 0)
            {
                visibleToRemove--;
                index++;
                continue;
            }

            result.Append(source[index]);
            index++;
        }

        return result.ToString();
    }

    private static bool TryReadMarkupToken(string source, int index, out int length)
    {
        return ColorCodePreserver.TryGetMarkupTokenLengthAt(source, index, out length);
    }

    private static string TranslateDemandPhrase(string source)
    {
        var match = QuantityDemandPattern.Match(source.Trim());
        if (!match.Success)
        {
            return TranslateDisplayNameCapture(source);
        }

        var count = match.Groups["count"].Value;
        var item = TranslateDisplayNameCaptureWithPluralFallback(match.Groups["item"].Value);
        return $"{item} x{count}";
    }

    private static string TranslateBaetylName(string source)
    {
        return string.Equals(source, "the sparking baetyl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "sparking baetyl", StringComparison.OrdinalIgnoreCase)
            ? "火花を散らすベテル"
            : source;
    }

    private static string RestoreReward(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateDisplayNameCapture(Restore(match, spans, "reward"));
    }

    private static string TranslateDisplayNameCapture(string source)
    {
        return DisplayNameCaptureTranslator.TranslatePreservingColors(source, Context);
    }

    private static string TranslateDisplayNameCaptureWithPluralFallback(string source)
    {
        var normalizedSource = MessageFrameTranslator.StripAllDirectTranslationMarkers(
            DisplayNameCaptureTranslator.StripLeadingEnglishArticlePreservingColors(source));
        var translated = TranslateDisplayNameCapture(source);
        if (!string.Equals(translated, normalizedSource, StringComparison.Ordinal))
        {
            return translated;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(normalizedSource, TranslatePluralVisibleDisplayName);
    }

    private static string TranslatePluralVisibleDisplayName(string visible)
    {
        foreach (var candidate in SingularDisplayNameCandidates(visible))
        {
            var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(candidate, Context);
            if (!string.Equals(translated, candidate, StringComparison.Ordinal))
            {
                return translated;
            }
        }

        return visible;
    }

    private static IEnumerable<string> SingularDisplayNameCandidates(string source)
    {
        var words = source.Split(' ');
        for (var index = 0; index < words.Length; index++)
        {
            var singular = SingularizeAsciiWord(words[index]);
            if (singular is null)
            {
                continue;
            }

            var candidate = (string[])words.Clone();
            candidate[index] = singular;
            yield return string.Join(" ", candidate);
        }
    }

    private static string? SingularizeAsciiWord(string word)
    {
        if (word.Length <= 3 || !Regex.IsMatch(word, "^[A-Za-z-]+s$", RegexOptions.CultureInvariant))
        {
            return null;
        }

        if (word.EndsWith("ies", StringComparison.Ordinal))
        {
            return word.Substring(0, word.Length - 3) + "y";
        }

        if (word.EndsWith("ses", StringComparison.Ordinal)
            || word.EndsWith("xes", StringComparison.Ordinal)
            || word.EndsWith("zes", StringComparison.Ordinal)
            || word.EndsWith("ches", StringComparison.Ordinal)
            || word.EndsWith("shes", StringComparison.Ordinal))
        {
            return word.Substring(0, word.Length - 2);
        }

        return word.Substring(0, word.Length - 1);
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
