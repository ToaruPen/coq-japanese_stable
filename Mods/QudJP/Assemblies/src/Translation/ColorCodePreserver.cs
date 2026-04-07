using System;
using System.Collections.Generic;
using System.Text;

namespace QudJP;

public static class ColorCodePreserver
{
    private const int MarkupTokenLength = 2;
    private const string TmpColorOpenTagPrefix = "<color=";
    private const string TmpColorCloseTag = "</color>";

    public static (string stripped, List<ColorSpan> spans) Strip(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (input ?? string.Empty, new List<ColorSpan>());
        }

        var spans = new List<ColorSpan>();
        var builder = new StringBuilder(input!.Length);
        ParseSegment(input, startIndex: 0, endIndex: input.Length, builder, spans);
        return (builder.ToString(), spans);
    }

    public static string Restore(string? translated, List<ColorSpan>? spans)
    {
        var value = translated ?? string.Empty;
        if (spans is null || spans.Count == 0)
        {
            return value;
        }

        var textLength = value.Length;
        var buckets = new Dictionary<int, List<string>>();

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            if (string.IsNullOrEmpty(span.Token))
            {
                continue;
            }

            var index = ResolveIndex(span, textLength);

            if (!buckets.TryGetValue(index, out var tokenList))
            {
                tokenList = new List<string>();
                buckets[index] = tokenList;
            }

            tokenList.Add(span.Token);
        }

        if (buckets.Count == 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + (spans.Count * MarkupTokenLength));

        for (var index = 0; index <= textLength; index++)
        {
            if (buckets.TryGetValue(index, out var tokenList))
            {
                for (var tokenIndex = 0; tokenIndex < tokenList.Count; tokenIndex++)
                {
                    builder.Append(tokenList[tokenIndex]);
                }
            }

            if (index < textLength)
            {
                builder.Append(value[index]);
            }
        }

        return builder.ToString();
    }

    internal static List<ColorSpan> SliceSpans(IReadOnlyList<ColorSpan>? spans, int startIndex, int length)
    {
        var sliced = new List<ColorSpan>();
        if (spans is null || spans.Count == 0 || length < 0)
        {
            return sliced;
        }

        var endIndex = startIndex + length;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index < startIndex || span.Index > endIndex)
            {
                continue;
            }

            if (span.Index == startIndex && IsCaptureClosingToken(span.Token))
            {
                continue;
            }

            if (span.Index == endIndex && !IsCaptureClosingToken(span.Token))
            {
                continue;
            }

            sliced.Add(new ColorSpan(span.Index - startIndex, span.Token, length, usesRelativeIndex: true));
        }

        return sliced;
    }

    internal static List<ColorSpan> SliceAdjacentCaptureBoundarySpans(IReadOnlyList<ColorSpan>? spans, int startIndex, int length)
    {
        var sliced = new List<ColorSpan>();
        if (spans is null || spans.Count == 0 || length < 0)
        {
            return sliced;
        }

        var endIndex = startIndex + length;
        var hasAdjacentOpeningBoundary = false;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index == startIndex - 1 && IsOpeningBoundaryToken(span.Token))
            {
                hasAdjacentOpeningBoundary = true;
                sliced.Add(new ColorSpan(0, span.Token));
                continue;
            }

            if (hasAdjacentOpeningBoundary
                && span.Index == endIndex + 1
                && IsClosingBoundaryToken(span.Token))
            {
                sliced.Add(new ColorSpan(length, span.Token));
            }
        }

        return sliced;
    }

    internal static bool HasAdjacentCaptureWrapper(IReadOnlyList<ColorSpan>? spans, int startIndex, int length)
    {
        if (spans is null || spans.Count == 0 || length < 0)
        {
            return false;
        }

        var endIndex = startIndex + length;
        var hasOpening = false;
        var hasClosing = false;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index == startIndex - 1 && IsOpeningBoundaryToken(span.Token))
            {
                hasOpening = true;
            }

            if (span.Index == endIndex + 1 && IsClosingBoundaryToken(span.Token))
            {
                hasClosing = true;
            }
        }

        return hasOpening && hasClosing;
    }

    private static int ResolveIndex(ColorSpan span, int textLength)
    {
        if (span.UsesRelativeIndex)
        {
            if (span.SourceLength > 0
                && span.Index == span.SourceLength - 1
                && textLength < span.SourceLength
                && textLength > 0
                && (IsOpeningBoundaryToken(span.Token) || IsClosingBoundaryToken(span.Token)))
            {
                return textLength - 1;
            }

            return MapIndex(span.Index, span.SourceLength, textLength);
        }

        if (span.Index < 0)
        {
            return 0;
        }

        return span.Index > textLength ? textLength : span.Index;
    }

    private static int MapIndex(int index, int sourceLength, int textLength)
    {
        if (index <= 0 || textLength == 0)
        {
            return 0;
        }

        if (sourceLength <= 0 || index >= sourceLength)
        {
            return textLength;
        }

        var scaled = ((long)index * textLength + (sourceLength / 2)) / sourceLength;
        if (scaled < 0)
        {
            return 0;
        }

        return scaled > textLength ? textLength : (int)scaled;
    }

    internal static bool IsOpeningBoundaryToken(string token)
    {
        return (token.StartsWith("{{", StringComparison.Ordinal) && token.EndsWith("|", StringComparison.Ordinal))
            || token.StartsWith(TmpColorOpenTagPrefix, StringComparison.OrdinalIgnoreCase)
            || (token.Length == MarkupTokenLength && (token[0] == '&' || token[0] == '^'));
    }

    internal static bool IsBoundaryToken(string token)
    {
        return string.Equals(token, "}}", StringComparison.Ordinal)
            || string.Equals(token, TmpColorCloseTag, StringComparison.OrdinalIgnoreCase)
            || (token.Length == MarkupTokenLength && (token[0] == '&' || token[0] == '^'));
    }

    internal static bool IsClosingBoundaryToken(string token)
    {
        return IsBoundaryToken(token);
    }

    private static bool IsCaptureClosingToken(string token)
    {
        return string.Equals(token, "}}", StringComparison.Ordinal)
            || string.Equals(token, TmpColorCloseTag, StringComparison.OrdinalIgnoreCase);
    }

    private static void ParseSegment(string input, int startIndex, int endIndex, StringBuilder builder, List<ColorSpan> spans)
    {
        var index = startIndex;
        while (index < endIndex)
        {
            if (index + 1 < endIndex && input[index] == '{' && input[index + 1] == '{'
                && TryReadMarkup(input, index, endIndex, out var prefixToken, out var innerStart, out var innerEnd, out var nextIndex))
            {
                spans.Add(new ColorSpan(builder.Length, prefixToken));
                ParseSegment(input, innerStart, innerEnd, builder, spans);
                spans.Add(new ColorSpan(builder.Length, "}}"));
                index = nextIndex;
                continue;
            }

            if (index + 1 < endIndex && input[index] == '&')
            {
                if (input[index + 1] == '&')
                {
                    builder.Append("&&");
                    index += MarkupTokenLength;
                    continue;
                }

                spans.Add(new ColorSpan(builder.Length, input.Substring(index, MarkupTokenLength)));
                index += MarkupTokenLength;
                continue;
            }

            if (index + 1 < endIndex && input[index] == '^')
            {
                if (input[index + 1] == '^')
                {
                    builder.Append("^^");
                    index += MarkupTokenLength;
                    continue;
                }

                spans.Add(new ColorSpan(builder.Length, input.Substring(index, MarkupTokenLength)));
                index += MarkupTokenLength;
                continue;
            }

            if (input[index] == '<'
                && TryReadTmpColorTag(input, index, endIndex, out var tmpToken, out var tmpNextIndex))
            {
                spans.Add(new ColorSpan(builder.Length, tmpToken));
                index = tmpNextIndex;
                continue;
            }

            builder.Append(input[index]);
            index++;
        }
    }

    private static bool TryReadTmpColorTag(
        string input,
        int startIndex,
        int endIndex,
        out string token,
        out int nextIndex)
    {
        token = string.Empty;
        nextIndex = startIndex;

        if (startIndex >= endIndex || input[startIndex] != '<')
        {
            return false;
        }

        if (StartsWithOrdinalIgnoreCase(input, startIndex, TmpColorCloseTag))
        {
            token = TmpColorCloseTag;
            nextIndex = startIndex + TmpColorCloseTag.Length;
            return true;
        }

        if (!StartsWithOrdinalIgnoreCase(input, startIndex, TmpColorOpenTagPrefix))
        {
            return false;
        }

        var closeIndex = input.IndexOf('>', startIndex + TmpColorOpenTagPrefix.Length);
        if (closeIndex < 0 || closeIndex >= endIndex)
        {
            return false;
        }

        token = input.Substring(startIndex, (closeIndex - startIndex) + 1);
        nextIndex = closeIndex + 1;
        return true;
    }

    private static bool TryReadMarkup(
        string input,
        int startIndex,
        int endIndex,
        out string prefixToken,
        out int innerStart,
        out int innerEnd,
        out int nextIndex)
    {
        prefixToken = string.Empty;
        innerStart = 0;
        innerEnd = 0;
        nextIndex = startIndex;

        var pipeIndex = -1;
        for (var index = startIndex + 2; index < endIndex; index++)
        {
            if (index + 1 < endIndex && input[index] == '}' && input[index + 1] == '}')
            {
                return false;
            }

            if (input[index] == '|')
            {
                pipeIndex = index;
                break;
            }
        }

        if (pipeIndex < 0)
        {
            return false;
        }

        var depth = 1;
        var indexAfterPipe = pipeIndex + 1;
        for (var index = indexAfterPipe; index + 1 < endIndex; index++)
        {
            if (input[index] == '{' && input[index + 1] == '{')
            {
                depth++;
                index++;
                continue;
            }

            if (input[index] == '}' && input[index + 1] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    prefixToken = input.Substring(startIndex, (pipeIndex - startIndex) + 1);
                    innerStart = pipeIndex + 1;
                    innerEnd = index;
                    nextIndex = index + 2;
                    return true;
                }

                index++;
            }
        }

        return false;
    }

    private static bool StartsWithOrdinalIgnoreCase(string input, int startIndex, string value)
    {
        if (startIndex < 0 || startIndex + value.Length > input.Length)
        {
            return false;
        }

        return string.Compare(input, startIndex, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }
}

public sealed class ColorSpan
{
    public ColorSpan(int index, string token, int sourceLength = -1, bool usesRelativeIndex = false)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        Index = index;
        Token = token;
        SourceLength = sourceLength;
        UsesRelativeIndex = usesRelativeIndex;
    }

    public int Index { get; }

    public string Token { get; }

    public int SourceLength { get; }

    public bool UsesRelativeIndex { get; }

    internal ColorSpan WithRelativeIndex(int sourceLength)
    {
        return new ColorSpan(Index, Token, sourceLength, usesRelativeIndex: true);
    }
}
