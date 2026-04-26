using System;
using System.Collections.Generic;
#if HAS_GAME_DLL
using HistoryKit;
#endif

namespace QudJP;

/// <summary>
/// Walks HistoricEvent.eventProperties (direct mutation) and HistoricEntity
/// (mutation events via SetEntityPropertyAtCurrentYear / MutateListPropertyAtCurrentYear)
/// applying <see cref="HistoricNarrativeTextTranslator"/> only to allowlisted keys.
/// </summary>
internal static class HistoricNarrativeDictionaryWalker
{
    private static readonly HashSet<string> EventPropertyAllowlist = new(StringComparer.Ordinal)
    {
        "gospel",
        "tombInscription",
    };

    private static readonly HashSet<string> EntityPropertyAllowlist = new(StringComparer.Ordinal)
    {
        "proverb",
        "defaultSacredThing",
        "defaultProfaneThing",
    };

    private static readonly HashSet<string> EntityListPropertyAllowlist = new(StringComparer.Ordinal)
    {
        "Gospels",
        "sacredThings",
        "profaneThings",
        "immigrant_dialogWhy_Q",
        "immigrant_dialogWhy_A",
        "pet_dialogWhy_Q",
    };

    private const string GospelEventIdSeparator = "|";

    /// <summary>
    /// Splits <paramref name="raw"/> on <see cref="GospelEventIdSeparator"/> and translates
    /// only the prose portion, preserving the trailing <c>|eventId</c> verbatim.
    /// </summary>
    internal static string TranslateGospelEntry(string raw, string? context = null)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw ?? string.Empty;
        }

        var separatorIndex = raw.IndexOf(GospelEventIdSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return HistoricNarrativeTextTranslator.Translate(raw, context);
        }

        var prose = raw.Substring(0, separatorIndex);
        var suffix = raw.Substring(separatorIndex); // includes the leading "|"
        var translatedProse = HistoricNarrativeTextTranslator.Translate(prose, context);
        return translatedProse + suffix;
    }

    /// <summary>Mutates the dict in place per the event-property allowlist.</summary>
    internal static void TranslateEventPropertiesDict(IDictionary<string, string> properties, string? context = null)
    {
        if (properties == null || properties.Count == 0)
        {
            return;
        }

        foreach (var key in EventPropertyAllowlist)
        {
            if (!properties.TryGetValue(key, out var current) || string.IsNullOrEmpty(current))
            {
                continue;
            }
            var translated = HistoricNarrativeTextTranslator.Translate(current, context);
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                properties[key] = translated;
            }
        }
    }

    /// <summary>
    /// Reads current snapshot via <paramref name="readProperty"/> / <paramref name="readList"/>,
    /// translates each allowlisted value, writes back via <paramref name="writeProperty"/> /
    /// <paramref name="mutateList"/>. The list mutation callback is invoked only when at least one
    /// element changed (sequence equality guard).
    /// </summary>
    internal static void TranslateEntityViaCallbacks(
        Func<string, string?> readProperty,
        Func<string, IReadOnlyList<string>?> readList,
        Action<string, string> writeProperty,
        Action<string, Func<string, string>> mutateList,
        string? context = null)
    {
        if (readProperty == null || readList == null || writeProperty == null || mutateList == null)
        {
            return;
        }

        foreach (var key in EntityPropertyAllowlist)
        {
            var current = readProperty(key);
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }
            var translated = HistoricNarrativeTextTranslator.Translate(current, context);
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                writeProperty(key, translated);
            }
        }

        foreach (var key in EntityListPropertyAllowlist)
        {
            var current = readList(key);
            if (current == null || current.Count == 0)
            {
                continue;
            }
            Func<string, string> mutation = key == "Gospels"
                ? (raw => TranslateGospelEntry(raw, context))
                : (raw => HistoricNarrativeTextTranslator.Translate(raw, context));

            // Pre-compute translated values to detect whether any element actually changes.
            // MutateListPropertyAtCurrentYear unconditionally adds a MutateListProperty event
            // (see decompiled MutateListProperty.Generate); avoid event noise on full passthrough.
            var changed = false;
            for (var i = 0; i < current.Count; i++)
            {
                if (!string.Equals(mutation(current[i]), current[i], StringComparison.Ordinal))
                {
                    changed = true;
                    break;
                }
            }
            if (changed)
            {
                mutateList(key, mutation);
            }
        }
    }

#if HAS_GAME_DLL
    internal static void TranslateEventProperties(HistoricEvent ev, string? context = null)
    {
        if (ev?.eventProperties == null)
        {
            return;
        }
        TranslateEventPropertiesDict(ev.eventProperties, context);
    }

    internal static void TranslateEntity(HistoricEntity entity, string? context = null)
    {
        if (entity == null)
        {
            return;
        }

        var snapshot = entity.GetCurrentSnapshot();
        if (snapshot == null)
        {
            return;
        }

        TranslateEntityViaCallbacks(
            readProperty: name =>
            {
                if (!snapshot.hasProperty(name))
                {
                    return null;
                }
                var value = snapshot.GetProperty(name);
                return string.IsNullOrEmpty(value) ? null : value;
            },
            readList: name => snapshot.hasListProperty(name)
                ? (IReadOnlyList<string>)snapshot.GetList(name)
                : null,
            writeProperty: (name, value) => entity.SetEntityPropertyAtCurrentYear(name, value),
            mutateList: (name, mutation) => entity.MutateListPropertyAtCurrentYear(name, mutation),
            context: context);
    }
#endif
}
