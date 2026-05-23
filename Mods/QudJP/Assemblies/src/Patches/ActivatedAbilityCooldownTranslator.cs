using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class ActivatedAbilityCooldownTranslator
{
    private static readonly Regex RawCooldownPattern = new(
        "^You must wait (?<duration>.+?) before using (?<ability>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslateRawCooldown(
        string source,
        string route,
        string family,
        out string translated)
    {
        if (!TryTranslateRawCooldown(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    internal static bool TryTranslateRawCooldown(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = RawCooldownPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = TranslateRawCooldown(source, match, spans);
        return true;
    }

    internal static string TranslateCooldownDuration(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateVisibleDuration);
    }

    private static string TranslateRawCooldown(
        string source,
        Match match,
        IReadOnlyList<ColorSpan> spans)
    {
        var duration = TranslateCooldownDuration(ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["duration"].Value,
            spans,
            match.Groups["duration"]));
        var ability = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["ability"].Value,
            spans,
            match.Groups["ability"]);
        var translatedAbility = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            ColorAwareTranslationComposer.GetVisibleText(ability),
            out var visibleAbility)
            ? ColorAwareTranslationComposer.TranslatePreservingColors(ability, _ => visibleAbility)
            : ability;

        var translated = $"{translatedAbility}を使うには{duration}待つ必要がある。";
        return ColorAwareTranslationComposer.HasColorMarkup(source)
            && !ColorAwareTranslationComposer.HasColorMarkup(translated)
            ? ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => translated)
            : translated;
    }

    private static string TranslateVisibleDuration(string visible)
    {
        return visible
            .Replace(" rounds", "ラウンド")
            .Replace(" round", "ラウンド")
            .Replace(" turns", "ターン")
            .Replace(" turn", "ターン");
    }
}
