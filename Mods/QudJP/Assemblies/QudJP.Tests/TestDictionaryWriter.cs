using System;
using System.IO;
using System.Text;

namespace QudJP.Tests;

internal static class TestDictionaryWriter
{
    public static void WriteEntries(
        string filePath,
        bool appendNewLine,
        params (string key, string text)[] entries)
    {
        var entriesWithContexts = Array.ConvertAll(
            entries,
            entry => (entry.key, entry.text, (string?)null));
        WriteEntries(filePath, appendNewLine, entriesWithContexts);
    }

    public static void WriteEntries(
        string filePath,
        bool appendNewLine,
        params (string key, string text, string? context)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append('"');
            if (entries[index].context is not null)
            {
                builder.Append(",\"context\":\"");
                builder.Append(EscapeJson(entries[index].context!));
                builder.Append('"');
            }

            builder.Append(",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        if (appendNewLine)
        {
            builder.Append('\n');
        }

        File.WriteAllText(
            filePath,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
