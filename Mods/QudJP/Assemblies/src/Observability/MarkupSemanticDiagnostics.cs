using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class MarkupSemanticDiagnostics
{
    internal const string StatusClean = "clean";
    internal const string StatusDrift = "drift";

    private static readonly Regex LiteralQudShaderFragmentPattern =
        new Regex(@"\{\{[^|}]+\}\}\|", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BracketCloseInsideNestedQudScopePattern =
        new Regex(@"\[\{\{[^|}]+\|[^\]}]*\]\}\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WorldGenerationProgressBarPattern =
        new Regex(@"^\{\{Y\|\}\}\{\{Y\|[ .]*(?:\}\}>\{\{K\|)?[ .■]+\{\{Y\|\}\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static MarkupSemanticDiagnosticsResult Analyze(string? text)
    {
        var value = text ?? string.Empty;
        var flags = new List<string>();
        AddFlagIfMatched(flags, "literal_qud_shader_fragment", LiteralQudShaderFragmentPattern, value);
        AddFlagIfMatched(flags, "bracket_close_inside_nested_qud_scope", BracketCloseInsideNestedQudScopePattern, value);

        var structuralFlags = ScanMarkupSemanticStructure(value);
        AddFlagIf(flags, "unmatched_qud_close", structuralFlags.UnmatchedQudClose);
        AddFlagIf(flags, "unclosed_qud_scope", structuralFlags.UnclosedQudScope && !IsWorldGenerationProgressBar(value));
        AddFlagIf(flags, "unclosed_tmp_scope", structuralFlags.UnclosedTmpScope);
        AddFlagIf(flags, "unmatched_tmp_close", structuralFlags.UnmatchedTmpClose);

        return flags.Count == 0
            ? new MarkupSemanticDiagnosticsResult(StatusClean, string.Empty)
            : new MarkupSemanticDiagnosticsResult(StatusDrift, string.Join(",", flags));
    }

    private static bool IsWorldGenerationProgressBar(string value)
    {
        return WorldGenerationProgressBarPattern.IsMatch(value);
    }

    private static MarkupSemanticFlags ScanMarkupSemanticStructure(string value)
    {
        var stack = new Stack<string>();
        var flags = new MarkupSemanticFlags();
        var index = 0;
        while (index < value.Length)
        {
            if (TryReadQudOpen(value, index, out var qudLength))
            {
                stack.Push("qud");
                index += qudLength;
                continue;
            }

            if (StartsWithOrdinal(value, index, "}}"))
            {
                if (!TryPopScope(stack, "qud", out _))
                {
                    flags.UnmatchedQudClose = true;
                }

                index += 2;
                continue;
            }

            if (TryReadTmpColorOpen(value, index, out var tmpOpenLength))
            {
                stack.Push("tmp");
                index += tmpOpenLength;
                continue;
            }

            if (StartsWithOrdinal(value, index, "</color>"))
            {
                if (!TryPopScope(stack, "tmp", out _))
                {
                    flags.UnmatchedTmpClose = true;
                }

                index += "</color>".Length;
                continue;
            }

            if (TryReadPlaceholder(value, index, out _, out var placeholderLength))
            {
                index += placeholderLength;
                continue;
            }

            index++;
        }

        while (stack.Count > 0)
        {
            var scope = stack.Pop();
            if (string.Equals(scope, "qud", StringComparison.Ordinal))
            {
                flags.UnclosedQudScope = true;
            }
            else if (string.Equals(scope, "tmp", StringComparison.Ordinal))
            {
                flags.UnclosedTmpScope = true;
            }
        }

        return flags;
    }

    private static void AddFlagIfMatched(List<string> flags, string flag, Regex pattern, string value)
    {
        if (pattern.IsMatch(value))
        {
            flags.Add(flag);
        }
    }

    private static void AddFlagIf(List<string> flags, string flag, bool condition)
    {
        if (condition)
        {
            flags.Add(flag);
        }
    }

    private static bool TryReadQudOpen(string value, int index, out int length)
    {
        length = 0;
        if (!StartsWithOrdinal(value, index, "{{"))
        {
            return false;
        }

        var separatorIndex = -1;
        for (var scanIndex = index + 2; scanIndex < value.Length; scanIndex++)
        {
            if (scanIndex + 1 < value.Length && value[scanIndex] == '}' && value[scanIndex + 1] == '}')
            {
                return false;
            }

            if (value[scanIndex] == '|')
            {
                separatorIndex = scanIndex;
                break;
            }
        }

        if (separatorIndex < 0)
        {
            return false;
        }

        length = separatorIndex - index + 1;
        return true;
    }

    private static bool TryReadTmpColorOpen(string value, int index, out int length)
    {
        length = 0;
        if (!StartsWithOrdinal(value, index, "<color="))
        {
            return false;
        }

        var endIndex = value.IndexOf('>', index + "<color=".Length);
        if (endIndex < 0)
        {
            return false;
        }

        length = endIndex - index + 1;
        return true;
    }

    private static bool TryReadPlaceholder(string value, int index, out string placeholder, out int length)
    {
        placeholder = string.Empty;
        length = 0;
        if (value[index] != '=')
        {
            return false;
        }

        var endIndex = value.IndexOf('=', index + 1);
        if (endIndex < 0)
        {
            return false;
        }

        var candidate = value.Substring(index + 1, endIndex - index - 1);
        if (candidate.Length == 0)
        {
            return false;
        }

        for (var candidateIndex = 0; candidateIndex < candidate.Length; candidateIndex++)
        {
            var character = candidate[candidateIndex];
            if (!IsAsciiLetterOrDigit(character) && character != '_' && character != '.')
            {
                return false;
            }
        }

        placeholder = candidate;
        length = endIndex - index + 1;
        return true;
    }

    private static bool TryPopScope(Stack<string> stack, string kind, out string scope)
    {
        if (stack.Count > 0 && string.Equals(stack.Peek(), kind, StringComparison.Ordinal))
        {
            scope = stack.Pop();
            return true;
        }

        scope = string.Empty;
        return false;
    }

    private static bool StartsWithOrdinal(string value, int index, string prefix)
    {
        return index <= value.Length - prefix.Length
            && string.CompareOrdinal(value, index, prefix, 0, prefix.Length) == 0;
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return (character >= 'A' && character <= 'Z')
            || (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9');
    }

    private struct MarkupSemanticFlags
    {
        internal bool UnmatchedQudClose { get; set; }

        internal bool UnclosedQudScope { get; set; }

        internal bool UnclosedTmpScope { get; set; }

        internal bool UnmatchedTmpClose { get; set; }
    }
}

internal readonly struct MarkupSemanticDiagnosticsResult
{
    internal MarkupSemanticDiagnosticsResult(string status, string flags)
    {
        Status = status;
        Flags = flags;
    }

    internal string Status { get; }

    internal string Flags { get; }
}
