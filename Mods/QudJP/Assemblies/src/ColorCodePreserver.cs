using System;
using System.Collections.Generic;
using System.Text;

namespace QudJP;

public static class ColorCodePreserver
{
    private const int MarkupTokenLength = 2;
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

            builder.Append(input[index]);
            index++;
        }
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
