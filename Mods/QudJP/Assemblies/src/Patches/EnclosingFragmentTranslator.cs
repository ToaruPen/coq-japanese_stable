using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class EnclosingFragmentTranslator
{
    private static readonly Regex ExtricatePattern =
        new Regex(
            "^You extricate (?<subject>.+?) from (?<container>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AlreadyInPattern =
        new Regex(
            "^You are already in (?<container>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FailToGetIntoPattern =
        new Regex(
            "^You fail to get (?<subject>.+?) into (?<container>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NotEnclosedByPattern =
        new Regex(
            "^It is not (?<container>.+?) that you are enclosed by\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotWhileEnclosedPattern =
        new Regex(
            "^You cannot do that while enclosed by (?<container>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NpcFailToGetIntoPattern =
        new Regex(
            "^(?<actor>.+?)(?<tryVerb> tries| try) to get (?<pronoun>.+?) into (?<container>.+?), but(?<failVerb> fails| fail)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ExtricatePattern.Match(stripped);
        if (match.Success)
        {
            var subject = TranslateSubject(match.Groups["subject"], spans);
            var container = RestoreVisible(match.Groups["container"], spans);
            translated = string.Equals(subject, "自分", StringComparison.Ordinal)
                ? string.Concat(container, "から抜け出した。")
                : string.Concat(container, "から", subject, "を引き出した。");
            DynamicTextObservability.RecordTransform(route, family + ".Extricate", source, translated);
            return true;
        }

        match = AlreadyInPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat("すでに", RestoreVisible(match.Groups["container"], spans), "の中にいる。");
            DynamicTextObservability.RecordTransform(route, family + ".AlreadyIn", source, translated);
            return true;
        }

        match = FailToGetIntoPattern.Match(stripped);
        if (match.Success)
        {
            var subject = TranslateSubject(match.Groups["subject"], spans);
            var container = RestoreVisible(match.Groups["container"], spans);
            translated = string.Equals(subject, "自分", StringComparison.Ordinal)
                ? string.Concat(container, "に入れなかった。")
                : string.Concat(subject, "を", container, "の中に入れられなかった。");
            DynamicTextObservability.RecordTransform(route, family + ".FailToGetInto", source, translated);
            return true;
        }

        match = NotEnclosedByPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat("閉じ込めているのは", RestoreVisible(match.Groups["container"], spans), "ではない。");
            DynamicTextObservability.RecordTransform(route, family + ".NotEnclosedBy", source, translated);
            return true;
        }

        match = CannotWhileEnclosedPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat(RestoreVisible(match.Groups["container"], spans), "に閉じ込められている間はそれをできない。");
            DynamicTextObservability.RecordTransform(route, family + ".CannotWhileEnclosed", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color, string route, string family)
    {
        _ = color;

        if (string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(message);
        var match = NpcFailToGetIntoPattern.Match(stripped);
        if (!match.Success)
        {
            return false;
        }

        var actor = RestoreVisible(match.Groups["actor"], spans);
        var pronoun = TranslateSubject(match.Groups["pronoun"], spans);
        var container = RestoreVisible(match.Groups["container"], spans);
        var translated = string.Concat(actor, "は", pronoun, "を", container, "の中に入れようとしたが、失敗した。");
        DynamicTextObservability.RecordTransform(route, family + ".NpcFailToGetInto", message, translated);
        message = translated;
        return true;
    }

    private static string TranslateSubject(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var trimmed = group.Value.Trim();
        if (string.Equals(trimmed, "yourself", StringComparison.OrdinalIgnoreCase))
        {
            return "自分";
        }

        if (string.Equals(trimmed, "itself", StringComparison.OrdinalIgnoreCase))
        {
            return "それ自身";
        }

        return RestoreVisible(group, spans);
    }

    private static string RestoreVisible(Group group, IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
