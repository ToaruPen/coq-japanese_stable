using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if HAS_TMP
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class EquipmentLineObservability
{
    private const int MaxLogsPerBucket = 6;

    private static readonly string[] MemberNameHints =
    {
        "name",
        "title",
        "label",
        "text",
        "item",
        "equip",
        "compare",
    };

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildState(object? lineInstance, object? data, int fontApplications, out string? logLine)
    {
        logLine = null;
        if (lineInstance is null || data is null)
        {
            return false;
        }

        var entries = new List<string>();
        AppendObjectEntries(entries, "line", lineInstance);
        AppendObjectEntries(entries, "data", data);
        if (entries.Count == 0)
        {
            return false;
        }

        var bucket = entries[0];
        var hitCount = BucketCounts.AddOrUpdate(
            bucket,
            1,
            static (_, currentValue) => currentValue < int.MaxValue ? currentValue + 1 : int.MaxValue);
        if (hitCount > MaxLogsPerBucket)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] EquipmentLineProbe/v1: fontApplications=");
        builder.Append(fontApplications);
        builder.Append("; ");
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append("; ");
            }

            builder.Append(entries[index]);
        }

        logLine = builder.ToString();
        return true;
    }

    internal static bool TryBuildCompactChildSummary(object? lineInstance, out string? logLine)
    {
        logLine = null;
#if HAS_TMP
        if (lineInstance is not Component component)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        if (texts.Length == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] EquipmentLineCompactChildren/v1: root='");
        builder.Append(component.gameObject.name);
        builder.Append('\'');
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            builder.Append("; child[");
            builder.Append(index);
            builder.Append("]='");
            builder.Append(text.gameObject.name);
            builder.Append("' text='");
            builder.Append(CompactTruncate(text.text ?? string.Empty));
            builder.Append("' chars=");
            builder.Append(text.textInfo.characterCount);
            builder.Append(" pageCount=");
            builder.Append(text.textInfo.pageCount);
            builder.Append(" active=");
            builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(" enabled=");
            builder.Append(text.enabled ? "True" : "False");
        }

        logLine = builder.ToString();
        return true;
#else
        _ = lineInstance;
        return false;
#endif
    }

    private static void AppendObjectEntries(List<string> entries, string prefix, object instance)
    {
        var type = instance.GetType();
#pragma warning disable S3011
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        for (var index = 0; index < fields.Length; index++)
        {
            AppendMember(entries, prefix, fields[index].Name, fields[index].FieldType, GetFieldValue(fields[index], instance));
        }

#pragma warning disable S3011
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        for (var index = 0; index < properties.Length; index++)
        {
            var property = properties[index];
            if (property.GetIndexParameters().Length != 0 || !property.CanRead)
            {
                continue;
            }

            object? value;
            try
            {
#pragma warning disable S3011
                value = property.GetValue(instance, null);
#pragma warning restore S3011
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            AppendMember(entries, prefix, property.Name, property.PropertyType, value);
        }
    }

    private static void AppendMember(List<string> entries, string prefix, string memberName, Type declaredType, object? value)
    {
        if (!ContainsHint(memberName))
        {
            return;
        }

        var summary = SummarizeValue(value, declaredType);
        if (string.IsNullOrEmpty(summary))
        {
            return;
        }

        entries.Add(prefix + "." + memberName + "=" + summary);
    }

    private static bool ContainsHint(string memberName)
    {
        for (var index = 0; index < MemberNameHints.Length; index++)
        {
#pragma warning disable CA2249
            if (memberName.IndexOf(MemberNameHints[index], StringComparison.OrdinalIgnoreCase) >= 0)
#pragma warning restore CA2249
            {
                return true;
            }
        }

        return false;
    }

    private static string? SummarizeValue(object? value, Type declaredType)
    {
        if (value is null)
        {
            return null;
        }

        if (declaredType == typeof(string) || value is string)
        {
            var stringValue = value as string;
            return string.IsNullOrEmpty(stringValue) ? null : Quote(Truncate(stringValue!));
        }

        var text = TryGetStringProperty(value, "text");
        var formattedText = TryGetStringProperty(value, "formattedText");
        var tmp = TryGetField(value, "_tmp");
        var tmpText = TryGetStringProperty(tmp, "text");
        if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(formattedText) && string.IsNullOrEmpty(tmpText))
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append(value.GetType().Name);
        builder.Append("(text=");
        builder.Append(Quote(Truncate(text)));
        builder.Append(", formatted=");
        builder.Append(Quote(Truncate(formattedText)));
        builder.Append(", tmp=");
        builder.Append(Quote(Truncate(tmpText)));
        builder.Append(')');
        return builder.ToString();
    }

    private static object? GetFieldValue(FieldInfo field, object instance)
    {
#pragma warning disable S3011
        return field.GetValue(instance);
#pragma warning restore S3011
    }

    private static string? TryGetStringProperty(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

#pragma warning disable S3011
        var property = instance.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        if (property is null || property.PropertyType != typeof(string) || property.GetIndexParameters().Length != 0)
        {
            return null;
        }

#pragma warning disable S3011
        return property.GetValue(instance, null) as string;
#pragma warning restore S3011
    }

    private static object? TryGetField(object instance, string memberName)
    {
#pragma warning disable S3011
        var field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(instance);
#pragma warning restore S3011
    }

    private static string Quote(string? value)
    {
        return string.IsNullOrEmpty(value) ? "''" : "'" + value + "'";
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var normalized = value!.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length <= 80)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 80) + "...";
#pragma warning restore CA1845
    }

    #if HAS_TMP
    private static string CompactTruncate(string value)
    {
        var normalized = value.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length == 0)
        {
            return "<empty>";
        }

        if (normalized.Length <= 28)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 28) + "...";
#pragma warning restore CA1845
    }
    #endif
}
