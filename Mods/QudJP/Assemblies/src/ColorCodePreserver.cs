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

            var index = span.Index;
            if (index < 0)
            {
                index = 0;
            }
            else if (index > textLength)
            {
                index = textLength;
            }

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

            sliced.Add(new ColorSpan(span.Index - startIndex, span.Token));
        }

        return sliced;
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
    public ColorSpan(int index, string token)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        Index = index;
        Token = token;
    }

    public int Index { get; }

    public string Token { get; }
}
