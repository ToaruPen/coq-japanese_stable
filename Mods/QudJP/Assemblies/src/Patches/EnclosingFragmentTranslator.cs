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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ExtricatePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = TranslateSubject(match.Groups["subject"], spans);
        var container = RestoreVisible(match.Groups["container"], spans);
        translated = string.Equals(subject, "自分", StringComparison.Ordinal)
            ? string.Concat(container, "から抜け出した。")
            : string.Concat(container, "から", subject, "を引き出した。");
        DynamicTextObservability.RecordTransform(route, family + ".Extricate", source, translated);
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
