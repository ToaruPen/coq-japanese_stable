using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DesalinationPelletFragmentTranslator
{
    private static readonly Regex DropPattern = new(
        "^You drop (?<pellet>.+?) into (?<container>.+?)\\.(?<body>(?:\\n\\n.*)?)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source is null ? string.Empty : source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = DropPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var pellet = RestoreCapture(match.Groups["pellet"], spans);
        var container = RestoreCapture(match.Groups["container"], spans);
        var body = RestoreCapture(match.Groups["body"], spans, trim: false);
        translated = string.Concat(pellet, "を", container, "に入れた。", body);
        DynamicTextObservability.RecordTransform(route, family + ".DropIntoContainer", source, translated);
        return true;
    }

    private static string RestoreCapture(Group group, IReadOnlyList<ColorSpan> spans, bool trim = true)
    {
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group);
        return trim ? restored.Trim() : restored;
    }
}
