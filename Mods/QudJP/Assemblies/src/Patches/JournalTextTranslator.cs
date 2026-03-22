using System;
using System.Text;
using HarmonyLib;

namespace QudJP.Patches;

internal static class JournalTextTranslator
{
    internal static bool TryTranslateBaseEntry(object entry, string source, string route, out string translated)
    {
        translated = source;
        if (!ShouldTranslateBaseEntry(entry))
        {
            return false;
        }

        return TryTranslateDisplayText(source, route, out translated);
    }

    internal static bool TryTranslateMapNoteEntry(object entry, string source, string route, out string translated)
    {
        translated = source;
        if (!ShouldTranslateMapNoteEntry(entry))
        {
            return false;
        }

        return TryTranslateDisplayText(source, route, out translated);
    }

    private static bool ShouldTranslateBaseEntry(object entry)
    {
        var typeName = entry.GetType().Name;
#pragma warning disable CA2249
        if (typeName.IndexOf("JournalAccomplishment", StringComparison.Ordinal) >= 0)
        {
            var category = GetStringMemberValue(entry, "Category");
            return !string.Equals(category, "player", StringComparison.OrdinalIgnoreCase);
        }

        return typeName.IndexOf("JournalObservation", StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
    }

    private static bool ShouldTranslateMapNoteEntry(object entry)
    {
        var category = GetStringMemberValue(entry, "Category");
        return !string.Equals(category, "Miscellaneous", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(category, "Named Locations", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryTranslateDisplayText(string source, string route, out string translated)
    {
        if (TryTranslateExactPreservingColors(source, route, "Journal.Exact", out translated))
        {
            return true;
        }

        translated = MessagePatternTranslator.Translate(source, route);
        if (!string.Equals(source, translated, StringComparison.Ordinal))
        {
            return true;
        }

        return TryTranslateLines(source, route, out translated);
    }

    private static bool TryTranslateExactPreservingColors(string source, string route, string family, out string translated)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var exact)
                ? exact
                : visible);
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateLines(string source, string route, out string translated)
    {
        if (source.IndexOf('\n') < 0)
        {
            translated = source;
            return false;
        }

        var lines = source.Split(new[] { '\n' }, StringSplitOptions.None);
        var changed = false;
        var builder = new StringBuilder(source.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var translatedLine = line;
            if (!string.IsNullOrEmpty(line)
                && !TryTranslateExactPreservingColors(line, route, "Journal.LineExact", out translatedLine))
            {
                translatedLine = MessagePatternTranslator.Translate(line, route);
            }

            changed |= !string.Equals(line, translatedLine, StringComparison.Ordinal);
            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append(translatedLine);
        }

        translated = changed ? builder.ToString() : source;
        if (changed)
        {
            DynamicTextObservability.RecordTransform(route, "Journal.Lines", source, translated);
        }

        return changed;
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance) as string;
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance) as string;
    }
}
