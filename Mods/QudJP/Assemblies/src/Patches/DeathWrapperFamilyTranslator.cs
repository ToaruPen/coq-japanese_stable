using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DeathWrapperFamilyTranslator
{
    private const string WrappedTemplateKey = "QudJP.DeathWrapper.Generic.Wrapped";

    private static readonly Regex WrappedCausePattern =
        new Regex(
            "^You died\\.\\n\\n(?:You were|You|Your) (?<cause>.+?) (?<prep>by colliding with|because of|from|by) (?:a |an |the )?(?<killer>.+?)[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareCausePattern =
        new Regex(
            "^(?:You were|You|Your) (?<cause>.+?) (?<prep>by colliding with|because of|from|by) (?:a |an |the )?(?<killer>.+?)[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WrappedExplosionPattern =
        new Regex(
            "^You died\\.\\n\\nYou died in the explosion of (?:a |an |the )?(?<killer>.+?)[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareExplosionPattern =
        new Regex(
            "^You died in the explosion of (?:a |an |the )?(?<killer>.+?)[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WrappedGenericExplosionPattern =
        new Regex(
            "^You died\\.\\n\\nYou died in an explosion[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareGenericExplosionPattern =
        new Regex(
            "^You died in an explosion[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WrappedSelfExplosionPattern =
        new Regex(
            "^You died\\.\\n\\nYou exploded[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareSelfExplosionPattern =
        new Regex(
            "^You exploded[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WrappedNeutronPattern =
        new Regex(
            "^You died\\.\\n\\nYou were crushed under the weight of a thousand suns[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareNeutronPattern =
        new Regex(
            "^You were crushed under the weight of a thousand suns[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WrappedThirstPattern =
        new Regex(
            "^You died\\.\\n\\nYou died of thirst[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareThirstPattern =
        new Regex(
            "^You died of thirst[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WrappedSuicidePattern =
        new Regex(
            "^You died\\.\\n\\nYou (?<accidental>accidentally )?killed yourself[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareSuicidePattern =
        new Regex(
            "^You (?<accidental>accidentally )?killed yourself[.!]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> CauseBodyKeys = new(StringComparer.Ordinal)
    {
        ["killed|by"] = "QudJP.DeathWrapper.KilledBy.Bare",
        ["killed|by colliding with"] = "QudJP.DeathWrapper.KilledByCollidingWith.Bare",
        ["accidentally killed|by"] = "QudJP.DeathWrapper.AccidentallyKilledBy.Bare",
        ["bitten to death|by"] = "QudJP.DeathWrapper.BittenToDeathBy.Bare",
        ["frozen to death|by"] = "QudJP.DeathWrapper.FrozenToDeathBy.Bare",
        ["immolated|by"] = "QudJP.DeathWrapper.ImmolatedBy.Bare",
        ["vaporized|by"] = "QudJP.DeathWrapper.VaporizedBy.Bare",
        ["electrocuted|by"] = "QudJP.DeathWrapper.ElectrocutedBy.Bare",
        ["dissolved|by"] = "QudJP.DeathWrapper.DissolvedBy.Bare",
        ["disintegrated|by"] = "QudJP.DeathWrapper.DisintegratedBy.Bare",
        ["plasma-burned to death|by"] = "QudJP.DeathWrapper.PlasmaBurnedToDeathBy.Bare",
        ["lased to death|by"] = "QudJP.DeathWrapper.LasedToDeathBy.Bare",
        ["illuminated to death|by"] = "QudJP.DeathWrapper.IlluminatedToDeathBy.Bare",
        ["cooked|by"] = "QudJP.DeathWrapper.CookedBy.Bare",
        ["died of poison|from"] = "QudJP.DeathWrapper.DiedOfPoisonFrom.Bare",
        ["bled to death|because of"] = "QudJP.DeathWrapper.BledToDeathBecauseOf.Bare",
        ["died of asphyxiation|from"] = "QudJP.DeathWrapper.DiedOfAsphyxiationFrom.Bare",
        ["metabolism failed|from"] = "QudJP.DeathWrapper.MetabolismFailedFrom.Bare",
        ["vital essence was drained to extinction|by"] = "QudJP.DeathWrapper.VitalEssenceDrainedToExtinctionBy.Bare",
        ["psychically extinguished|by"] = "QudJP.DeathWrapper.PsychicallyExtinguishedBy.Bare",
        ["mentally obliterated|by"] = "QudJP.DeathWrapper.MentallyObliteratedBy.Bare",
        ["pricked to death|by"] = "QudJP.DeathWrapper.PrickedToDeathBy.Bare",
        ["decapitated|by"] = "QudJP.DeathWrapper.DecapitatedBy.Bare",
        ["consumed whole|by"] = "QudJP.DeathWrapper.ConsumedWholeBy.Bare",
        ["slammed|by"] = "QudJP.DeathWrapper.SlammedBy.Bare",
        ["slammed into a wall|by"] = "QudJP.DeathWrapper.SlammedIntoWallBy.Bare",
        ["slammed into two walls|by"] = "QudJP.DeathWrapper.SlammedIntoTwoWallsBy.Bare",
        ["relieved of your vital anatomy|by"] = "QudJP.DeathWrapper.RelievedOfYourVitalAnatomyBy.Bare",
    };

    internal static bool TryTranslatePopup(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        string route,
        out string translated)
    {
        return TryTranslate(source, spans, route, route, out translated);
    }

    internal static bool TryTranslateMessage(string source, IReadOnlyList<ColorSpan>? spans, out string translated)
    {
        return TryTranslate(
            source,
            spans,
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
        string translationContext,
        string observabilityRoute,
        out string translated)
    {
        if (TryTranslateKillerBody(
                source,
                spans,
                WrappedCausePattern,
                wrapped: true,
                family: "DeathWrapper.Generic.Wrapped",
                translationContext,
                observabilityRoute,
                ResolveCauseBodyKey,
                out translated)
            || TryTranslateKillerBody(
                source,
                spans,
                BareCausePattern,
                wrapped: false,
                family: "DeathWrapper.Generic.Bare",
                translationContext,
                observabilityRoute,
                ResolveCauseBodyKey,
                out translated)
            || TryTranslateKillerBody(
                source,
                spans,
                WrappedExplosionPattern,
                wrapped: true,
                family: "DeathWrapper.ExplosionOf.Wrapped",
                translationContext,
                observabilityRoute,
                _ => "QudJP.DeathWrapper.DiedInExplosionOf.Bare",
                out translated)
            || TryTranslateKillerBody(
                source,
                spans,
                BareExplosionPattern,
                wrapped: false,
                family: "DeathWrapper.ExplosionOf.Bare",
                translationContext,
                observabilityRoute,
                _ => "QudJP.DeathWrapper.DiedInExplosionOf.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                WrappedGenericExplosionPattern,
                wrapped: true,
                family: "DeathWrapper.Explosion.Wrapped",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.DiedInExplosion.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                BareGenericExplosionPattern,
                wrapped: false,
                family: "DeathWrapper.Explosion.Bare",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.DiedInExplosion.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                WrappedSelfExplosionPattern,
                wrapped: true,
                family: "DeathWrapper.Exploded.Wrapped",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.Exploded.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                BareSelfExplosionPattern,
                wrapped: false,
                family: "DeathWrapper.Exploded.Bare",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.Exploded.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                WrappedNeutronPattern,
                wrapped: true,
                family: "DeathWrapper.CrushedUnderSuns.Wrapped",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.CrushedUnderSuns.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                BareNeutronPattern,
                wrapped: false,
                family: "DeathWrapper.CrushedUnderSuns.Bare",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.CrushedUnderSuns.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                WrappedThirstPattern,
                wrapped: true,
                family: "DeathWrapper.Thirst.Wrapped",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.DiedOfThirst.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                BareThirstPattern,
                wrapped: false,
                family: "DeathWrapper.Thirst.Bare",
                observabilityRoute,
                _ => "QudJP.DeathWrapper.DiedOfThirst.Bare",
                out translated)
            || TryTranslateBody(
                source,
                spans,
                WrappedSuicidePattern,
                wrapped: true,
                family: "DeathWrapper.Suicide.Wrapped",
                observabilityRoute,
                ResolveSuicideBodyKey,
                out translated)
            || TryTranslateBody(
                source,
                spans,
                BareSuicidePattern,
                wrapped: false,
                family: "DeathWrapper.Suicide.Bare",
                observabilityRoute,
                ResolveSuicideBodyKey,
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateKillerBody(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        Regex pattern,
        bool wrapped,
        string family,
        string translationContext,
        string observabilityRoute,
        Func<Match, string?> resolveBodyKey,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (resolveBodyKey(match) is not string bodyKey
            || !TryTranslateBodyTemplate(bodyKey, out var bodyTemplate))
        {
            translated = source;
            return false;
        }

        var killer = TranslateEntityReference(match.Groups["killer"].Value, translationContext);
        if (spans is not null && spans.Count > 0)
        {
            killer = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(killer, spans, match.Groups["killer"]);
        }

        return TryComposeTranslation(
            source,
            spans,
            match,
            wrapped,
            family,
            observabilityRoute,
            bodyTemplate.Replace("{killer}", killer),
            out translated);
    }

    private static bool TryTranslateBody(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        Regex pattern,
        bool wrapped,
        string family,
        string observabilityRoute,
        Func<Match, string?> resolveBodyKey,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (resolveBodyKey(match) is not string bodyKey
            || !TryTranslateBodyTemplate(bodyKey, out var bodyTemplate))
        {
            translated = source;
            return false;
        }

        return TryComposeTranslation(
            source,
            spans,
            match,
            wrapped,
            family,
            observabilityRoute,
            bodyTemplate,
            out translated);
    }

    private static bool TryComposeTranslation(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        Match match,
        bool wrapped,
        string family,
        string observabilityRoute,
        string body,
        out string translated)
    {
        if (wrapped)
        {
            if (!TryTranslateBodyTemplate(WrappedTemplateKey, out var wrappedTemplate))
            {
                translated = source;
                return false;
            }

            translated = wrappedTemplate.Replace("{body}", body);
        }
        else
        {
            translated = body;
        }

        if (spans is not null && spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(
                spans,
                match,
                source.Length,
                translated.Length);
            translated = ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        DynamicTextObservability.RecordTransform(observabilityRoute, family, source, translated);
        return true;
    }

    private static bool TryTranslateBodyTemplate(string templateKey, out string translatedTemplate)
    {
        translatedTemplate = Translator.Translate(templateKey);
        return !string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal);
    }

    private static string? ResolveCauseBodyKey(Match match)
    {
        var causeKey = match.Groups["cause"].Value + "|" + match.Groups["prep"].Value;
        return CauseBodyKeys.TryGetValue(causeKey, out var bodyKey) ? bodyKey : null;
    }

    private static string ResolveSuicideBodyKey(Match match)
    {
        return match.Groups["accidental"].Success
            ? "QudJP.DeathWrapper.AccidentalSuicide.Bare"
            : "QudJP.DeathWrapper.Suicide.Bare";
    }

}
