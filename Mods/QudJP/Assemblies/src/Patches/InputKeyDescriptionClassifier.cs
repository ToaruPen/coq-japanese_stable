using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class InputKeyDescriptionClassifier
{
    private static readonly Regex Pattern = new(
        @"^(?:(?:Ctrl|Alt|Shift|Cmd|Command|Option|Meta)\+)*(?:[A-Z]|\d|F\d{1,2}|Space|Enter|Return|Esc|Escape|Tab|Backspace|Delete|Home|End|Page Up|Page Down|Up|Down|Left|Right|Mouse \d|Mouse [A-Za-z]+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsInputKeyDescription(string source)
    {
        return Pattern.IsMatch(source.Trim());
    }
}
