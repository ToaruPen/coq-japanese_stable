using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace QudJP;

internal static class QudMenuBottomContextObservability
{
    private const int MaxLogsPerPhase = 8;

    private static readonly ConcurrentDictionary<string, int> PhaseCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildState(object? contextInstance, string phase, out string? logLine)
    {
        logLine = null;
        if (contextInstance is null)
        {
            return false;
        }

        var hitCount = PhaseCounts.AddOrUpdate(phase, 1, static (_, current) => current + 1);
        if (hitCount > MaxLogsPerPhase)
        {
            return false;
        }

        var items = GetEnumerable(GetPropertyOrFieldValue(contextInstance, "items"));
        var buttons = GetEnumerable(GetPropertyOrFieldValue(contextInstance, "buttons"));
        var builder = new StringBuilder();
        builder.Append("[QudJP] QudMenuBottomContextProbe/");
        builder.Append(phase);
        builder.Append(": items=");
        builder.Append(items.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append(" buttons=");
        builder.Append(buttons.Count.ToString(CultureInfo.InvariantCulture));

        var count = Math.Min(items.Count, buttons.Count);
        for (var index = 0; index < count && index < 6; index++)
        {
            var item = items[index];
            var button = buttons[index];
            var command = TryGetStringByCandidates(item, "command", "Command");
            var hotkey = TryGetStringByCandidates(item, "hotkey", "Hotkey");
            var text = TryGetStringByCandidates(item, "text", "Text");
            builder.Append("; [");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
            builder.Append("] cmd='");
            builder.Append(Escape(command));
            builder.Append("' hotkey='");
            builder.Append(Escape(hotkey));
            builder.Append("' text='");
            builder.Append(Escape(Truncate(text)));
            builder.Append("' button='");
            builder.Append(button?.GetType().FullName ?? string.Empty);
            builder.Append('\'');
        }

        logLine = builder.ToString();
        return true;
    }

    private static List<object?> GetEnumerable(object? value)
    {
        var list = new List<object?>();
        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                list.Add(item);
            }
        }

        return list;
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
        return value!.Length <= 80 ? value : value.Substring(0, 80) + "...";
#pragma warning restore CA1845
    }

    private static string? TryGetStringPropertyOrField(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

 #pragma warning disable S3011
        var property = instance.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
 #pragma warning restore S3011
        if (property is not null && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
        {
#pragma warning disable S3011
            return property.GetValue(instance) as string;
#pragma warning restore S3011
        }

#pragma warning disable S3011
        var field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        return field?.GetValue(instance) as string;
    }

    private static string? TryGetStringByCandidates(object? instance, params string[] memberNames)
    {
        for (var index = 0; index < memberNames.Length; index++)
        {
            var value = TryGetStringPropertyOrField(instance, memberNames[index]);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private static object? GetPropertyOrFieldValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

 #pragma warning disable S3011
        var property = instance.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
 #pragma warning restore S3011
        if (property is not null && property.GetIndexParameters().Length == 0)
        {
#pragma warning disable S3011
            return property.GetValue(instance);
#pragma warning restore S3011
        }

#pragma warning disable S3011
        var field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        return field?.GetValue(instance);
    }
}
