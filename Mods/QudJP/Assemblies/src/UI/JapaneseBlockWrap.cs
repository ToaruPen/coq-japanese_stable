using System;
using System.Text;

namespace QudJP.UI;

internal static class JapaneseBlockWrap
{
    internal const int DefaultTooltipVisibleColumns = 34;
    private const int DefaultMaxLines = 5000;
    private const int PreferredBreakSearchColumns = 12;

    internal static bool TryWrapTooltipLongDescription(string source, out string wrapped)
    {
        return TryWrapForCjkBlock(source, DefaultTooltipVisibleColumns, DefaultMaxLines, out wrapped);
    }

    internal static bool TryWrapForCjkBlock(string source, int width, int maxLines, out string wrapped)
    {
        wrapped = source;
        if (string.IsNullOrEmpty(source) || width <= 0 || maxLines <= 0 || !ContainsCjk(source))
        {
            return false;
        }

        var builder = new StringBuilder(source.Length + (source.Length / width) + 8);
        char? activeForeground = null;
        char? activeBackground = null;
        var visibleColumn = 0;
        var lineCount = 1;
        var insertedBreak = false;
        var preferredBreak = BreakCandidate.None;

        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            if (TryAppendQudMarkupToken(source, ref index, builder))
            {
                continue;
            }

            if (TryAppendFormattingCode(source, ref index, builder, ref activeForeground, ref activeBackground))
            {
                continue;
            }

            builder.Append(current);
            if (current == '\n')
            {
                visibleColumn = 0;
                lineCount++;
                preferredBreak = BreakCandidate.None;
                if (lineCount > maxLines)
                {
                    break;
                }

                AppendActiveFormatting(builder, activeForeground, activeBackground);
                continue;
            }

            visibleColumn++;
            if (IsPreferredBreakAfter(source, index))
            {
                preferredBreak = new BreakCandidate(
                    builder.Length,
                    visibleColumn,
                    activeForeground,
                    activeBackground);
            }

            if (visibleColumn < width || index >= source.Length - 1 || lineCount >= maxLines || !CanBreakAfter(source, index))
            {
                continue;
            }

            var breakAt = SelectBreakCandidate(preferredBreak, width);
            if (breakAt.HasValue)
            {
                InsertBreakAtCandidate(builder, breakAt.Value);
                visibleColumn -= breakAt.Value.VisibleColumn;
            }
            else
            {
                builder.Append('\n');
                visibleColumn = 0;
                AppendActiveFormatting(builder, activeForeground, activeBackground);
            }

            lineCount++;
            insertedBreak = true;
            preferredBreak = BreakCandidate.None;
        }

        if (!insertedBreak)
        {
            return false;
        }

        wrapped = builder.ToString();
        return true;
    }

    private static bool TryAppendQudMarkupToken(string source, ref int index, StringBuilder builder)
    {
        if (index + 1 >= source.Length || source[index] != '{' || source[index + 1] != '{')
        {
            return TryAppendQudMarkupClose(source, ref index, builder);
        }

        var pipeIndex = source.IndexOf('|', index + 2);
        var closeIndex = source.IndexOf("}}", index + 2, StringComparison.Ordinal);
        if (pipeIndex < 0 || (closeIndex >= 0 && closeIndex < pipeIndex))
        {
            return false;
        }

        builder.Append(source, index, pipeIndex - index + 1);
        index = pipeIndex;
        return true;
    }

    private static bool TryAppendQudMarkupClose(string source, ref int index, StringBuilder builder)
    {
        if (index + 1 >= source.Length || source[index] != '}' || source[index + 1] != '}')
        {
            return false;
        }

        builder.Append("}}");
        index++;
        return true;
    }

    private static bool TryAppendFormattingCode(
        string source,
        ref int index,
        StringBuilder builder,
        ref char? activeForeground,
        ref char? activeBackground)
    {
        var current = source[index];
        if (current != '&' && current != '^')
        {
            return false;
        }

        if (index + 1 >= source.Length)
        {
            return false;
        }

        var next = source[index + 1];
        builder.Append(current);
        builder.Append(next);
        index++;

        if (current == '&' && next != '&')
        {
            activeForeground = next;
        }
        else if (current == '^' && next != '^')
        {
            activeBackground = next;
        }

        return true;
    }

    private static void AppendActiveFormatting(StringBuilder builder, char? activeForeground, char? activeBackground)
    {
        if (activeForeground.HasValue)
        {
            builder.Append('&');
            builder.Append(activeForeground.Value);
        }

        if (activeBackground.HasValue)
        {
            builder.Append('^');
            builder.Append(activeBackground.Value);
        }
    }

    private static BreakCandidate? SelectBreakCandidate(BreakCandidate preferredBreak, int width)
    {
        if (!preferredBreak.HasValue)
        {
            return null;
        }

        var columnsBeforeLimit = width - preferredBreak.VisibleColumn;
        if (columnsBeforeLimit > PreferredBreakSearchColumns)
        {
            return null;
        }

        return preferredBreak;
    }

    private static void InsertBreakAtCandidate(StringBuilder builder, BreakCandidate candidate)
    {
        var insertion = new StringBuilder(5);
        insertion.Append('\n');
        AppendActiveFormatting(insertion, candidate.ActiveForeground, candidate.ActiveBackground);
        builder.Insert(candidate.BuilderIndex, insertion.ToString());
    }

    private static bool IsPreferredBreakAfter(string source, int index)
    {
        var current = source[index];
        if (index >= source.Length - 1 || IsForbiddenLineStart(source[index + 1]))
        {
            return false;
        }

        return current is '。' or '、' or '，' or ','
            || (current is '　' or ' ' && !StartsNumericTerm(source[index + 1]));
    }

    private static bool StartsNumericTerm(char value)
    {
        return value is '+' or '-' or >= '0' and <= '9' or >= '０' and <= '９';
    }

    private static bool CanBreakAfter(string source, int index)
    {
        if (index + 1 < source.Length && IsForbiddenLineStart(source[index + 1]))
        {
            return false;
        }

        return IsCjk(source[index]) || (index + 1 < source.Length && IsCjk(source[index + 1]));
    }

    private static bool IsForbiddenLineStart(char value)
    {
        return value is '。' or '、' or '，' or ',' or '；' or ';' or '：' or ':' or '／' or '/' or '）' or ')' or '］' or ']' or '}' or '」' or '』';
    }

    private static bool ContainsCjk(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (IsCjk(value[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjk(char value)
    {
        return value is >= '\u3040' and <= '\u30ff'
            or >= '\u3400' and <= '\u4dbf'
            or >= '\u4e00' and <= '\u9fff'
            or >= '\uf900' and <= '\ufaff'
            or >= '\uff00' and <= '\uffef';
    }

    private readonly struct BreakCandidate
    {
        internal static BreakCandidate None => default;

        internal BreakCandidate(int builderIndex, int visibleColumn, char? activeForeground, char? activeBackground)
        {
            BuilderIndex = builderIndex;
            VisibleColumn = visibleColumn;
            ActiveForeground = activeForeground;
            ActiveBackground = activeBackground;
            HasValue = true;
        }

        internal int BuilderIndex { get; }

        internal int VisibleColumn { get; }

        internal char? ActiveForeground { get; }

        internal char? ActiveBackground { get; }

        internal bool HasValue { get; }
    }
}
