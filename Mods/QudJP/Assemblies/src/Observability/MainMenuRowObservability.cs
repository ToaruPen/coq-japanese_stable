using System;
using System.Reflection;
using System.Text;

namespace QudJP;

internal static class MainMenuRowObservability
{
    private const int MaxTextLength = 96;

    internal static bool TryBuildStateForTests(object? row, string phase, out string? logLine)
    {
        return TryBuildState(row, phase, out logLine);
    }

    internal static bool TryBuildState(object? row, string phase, out string? logLine)
    {
        try
        {
            return TryBuildStateCore(row, phase, out logLine);
        }
        catch (Exception)
        {
            // Diagnostic probes must never affect main-menu rendering.
            logLine = null;
            return false;
        }
    }

    private static bool TryBuildStateCore(object? row, string phase, out string? logLine)
    {
        logLine = null;
        if (row is null || string.IsNullOrEmpty(phase))
        {
            return false;
        }

        var data = GetPropertyOrFieldValue(row, "data");
        var text = GetPropertyOrFieldValue(row, "text");
        var font = GetPropertyOrFieldValue(text, "font");

        var builder = new StringBuilder();
        builder.Append("[QudJP] MainMenuRowProbe/");
        builder.Append(phase);
        builder.Append(": rowType='");
        builder.Append(Escape(row.GetType().FullName));
        builder.Append("' textType='");
        builder.Append(Escape(text?.GetType().FullName));
        builder.Append("' dataText='");
        builder.Append(Escape(Truncate(GetStringByCandidates(data, "Text", "text"))));
        builder.Append("' rowText='");
        builder.Append(Escape(Truncate(GetStringByCandidates(text, "text", "Text"))));
        builder.Append("' font='");
        builder.Append(Escape(GetStringByCandidates(font, "name", "Name")));
        builder.Append('\'');

        logLine = builder.ToString();
        return true;
    }

    private static string Escape(string? value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value!.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

#pragma warning disable CA1845
        return value!.Length <= MaxTextLength ? value : value.Substring(0, MaxTextLength) + "...";
#pragma warning restore CA1845
    }

    private static string? GetStringByCandidates(object? instance, params string[] memberNames)
    {
        for (var index = 0; index < memberNames.Length; index++)
        {
            var value = GetPropertyOrFieldValue(instance, memberNames[index]) as string;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private static object? GetPropertyOrFieldValue(object? instance, string memberName)
    {
        if (instance is null || string.IsNullOrEmpty(memberName))
        {
            return null;
        }

        var type = instance.GetType();
#pragma warning disable S3011
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        if (property is not null && property.GetIndexParameters().Length == 0)
        {
#pragma warning disable S3011
            return property.GetValue(instance);
#pragma warning restore S3011
        }

#pragma warning disable S3011
        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        return field?.GetValue(instance);
    }
}
