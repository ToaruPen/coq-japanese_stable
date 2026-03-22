using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DeathWrapperFamilyTranslator
{
    private static readonly DeathWrapperDefinition[] PopupDefinitions =
    {
        new(
            "DeathWrapper.KilledBy",
            "^You died\\.\\n\\nYou were killed by (?:a |an |the )?(?<killer>.+?)[.!]?$",
            "QudJP.DeathWrapper.KilledBy.Wrapped",
            "{killer}"),
        new(
            "DeathWrapper.BittenToDeathBy",
            "^You died\\.\\n\\nYou were bitten to death by (?:a |an |the )?(?<killer>.+?)[.!]?$",
            "QudJP.DeathWrapper.BittenToDeathBy.Wrapped",
            "{killer}"),
    };

    private static readonly DeathWrapperDefinition[] MessageDefinitions =
    {
        new(
            "DeathWrapper.KilledBy",
            "^You died\\.\\n\\nYou were killed by (?:a |an |the )?(?<killer>.+?)[.!]?$",
            "QudJP.DeathWrapper.KilledBy.Wrapped",
            "{killer}"),
        new(
            "DeathWrapper.BittenToDeathBy",
            "^You died\\.\\n\\nYou were bitten to death by (?:a |an |the )?(?<killer>.+?)[.!]?$",
            "QudJP.DeathWrapper.BittenToDeathBy.Wrapped",
            "{killer}"),
        new(
            "DeathWrapper.KilledByBare",
            "^You were killed by (?:a |an |the )?(?<killer>.+?)[.!]?$",
            "QudJP.DeathWrapper.KilledBy.Bare",
            "{killer}"),
        new(
            "DeathWrapper.AccidentallyKilledByBare",
            "^You were accidentally killed by (?:a |an |the )?(?<killer>.+?)[.!]?$",
            "QudJP.DeathWrapper.AccidentallyKilledBy.Bare",
            "{killer}"),
    };

    internal static bool TryTranslatePopup(string source, out string translated)
    {
        return TryTranslate(
            source,
            spans: null,
            PopupDefinitions,
            translationContext: nameof(PopupTranslationPatch),
            observabilityRoute: nameof(PopupTranslationPatch),
            out translated);
    }

    internal static bool TryTranslateMessage(string source, IReadOnlyList<ColorSpan>? spans, out string translated)
    {
        return TryTranslate(
            source,
            spans,
            MessageDefinitions,
            translationContext: nameof(MessageLogPatch),
            observabilityRoute: nameof(MessagePatternTranslator),
            out translated);
    }

    internal static string TranslateEntityReference(string source, string routeContext)
    {
        var trimmed = source.TrimEnd();
        var trailingNewline = source.Length > trimmed.Length ? source.Substring(trimmed.Length) : string.Empty;
        var normalized = StringHelpers.StripLeadingEnglishArticle(trimmed, includeCapitalizedDefiniteArticle: true);
        if (Translator.TryGetTranslation(normalized, out var direct)
            && !string.Equals(direct, normalized, StringComparison.Ordinal))
        {
            return direct + trailingNewline;
        }

        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(normalized, routeContext))
        {
            return normalized + trailingNewline;
        }

        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        return GetDisplayNameRouteTranslator.TranslatePreservingColors(normalized, nameof(GetDisplayNameProcessPatch)) + trailingNewline;
    }

    private static bool TryTranslate(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        DeathWrapperDefinition[] definitions,
        string translationContext,
        string observabilityRoute,
        out string translated)
    {
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            var match = definition.Pattern.Match(source);
            if (!match.Success)
            {
                continue;
            }

            var translatedTemplate = Translator.Translate(definition.TemplateKey);
            if (string.Equals(translatedTemplate, definition.TemplateKey, StringComparison.Ordinal))
            {
                translated = source;
                return false;
            }

            var killer = TranslateEntityReference(match.Groups["killer"].Value, translationContext);
            if (spans is not null && spans.Count > 0)
            {
                killer = ColorAwareTranslationComposer.RestoreCapture(killer, spans, match.Groups["killer"]);
            }

            translated = translatedTemplate.Replace(definition.Placeholder, killer);
            if (spans is not null && spans.Count > 0)
            {
                var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(
                    spans,
                    match,
                    source.Length,
                    translated.Length);
                translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
            }

            DynamicTextObservability.RecordTransform(observabilityRoute, definition.Family, source, translated);
            return true;
        }

        translated = source;
        return false;
    }
    private sealed class DeathWrapperDefinition
    {
        internal DeathWrapperDefinition(string family, string pattern, string templateKey, string placeholder)
        {
            Family = family;
            Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            TemplateKey = templateKey;
            Placeholder = placeholder;
        }

        internal string Family { get; }

        internal Regex Pattern { get; }

        internal string TemplateKey { get; }

        internal string Placeholder { get; }
    }
}
