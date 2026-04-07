using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace QudJP;

internal static class InventoryUiFieldObservability
{
    private const int MaxLogsPerBucket = 4;
    private const int MaxEntriesPerSnapshot = 12;

    private static readonly string[] MemberNameHints =
    {
        "name",
        "title",
        "label",
        "text",
        "compare",
        "item",
        "tooltip",
        "panel",
        "go",
    };

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildScreenSnapshot(object? screenInstance, out string? logLine)
    {
        logLine = null;
        if (screenInstance is null)
        {
            return false;
        }

        var entries = CollectEntries(screenInstance);
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
        builder.Append("[QudJP] InventoryUiFieldProbe/v1: ");
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

    internal static string[] CollectEntriesForTests(object screenInstance)
    {
        return CollectEntries(screenInstance).ToArray();
    }

    private static List<string> CollectEntries(object screenInstance)
    {
        var entries = new List<string>();
        for (var type = screenInstance.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            AppendTypeEntries(entries, screenInstance, type);
            if (entries.Count >= MaxEntriesPerSnapshot)
            {
                return entries;
            }
        }

        return entries;
    }

    private static void AppendTypeEntries(List<string> entries, object screenInstance, Type type)
    {
#pragma warning disable S3011
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
#pragma warning restore S3011
        for (var index = 0; index < fields.Length; index++)
        {
#pragma warning disable S3011
            AppendMember(entries, fields[index].Name, fields[index].FieldType, fields[index].GetValue(screenInstance));
#pragma warning restore S3011
            if (entries.Count >= MaxEntriesPerSnapshot)
            {
                return;
            }
        }

#pragma warning disable S3011
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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
                value = property.GetValue(screenInstance, null);
#pragma warning restore S3011
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            AppendMember(entries, property.Name, property.PropertyType, value);
            if (entries.Count >= MaxEntriesPerSnapshot)
            {
                return;
            }
        }
    }

    private static void AppendMember(List<string> entries, string memberName, Type declaredType, object? value)
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

        entries.Add(memberName + "=" + summary);
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
        var tmp = GetField(value, "_tmp");
        var tmpText = TryGetStringProperty(tmp, "text");

        if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(formattedText) && string.IsNullOrEmpty(tmpText))
        {
            return SummarizeUiRoot(value);
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

    private static string? SummarizeUiRoot(object value)
    {
        var gameObject = ResolveGameObjectLike(value);
        if (gameObject is null)
        {
            return null;
        }

        var gameObjectName = TryGetStringPropertyOrField(gameObject, "name");
        if (string.IsNullOrEmpty(gameObjectName))
        {
            return null;
        }

        var builder = new StringBuilder();
        if (!ReferenceEquals(gameObject, value))
        {
            builder.Append(value.GetType().Name);
            builder.Append("(gameObject=");
        }

        builder.Append(gameObject.GetType().Name);
        builder.Append("(name=");
        builder.Append(Quote(Truncate(gameObjectName)));

        var activeSelf = TryGetBooleanPropertyOrField(gameObject, "activeSelf");
        if (activeSelf.HasValue)
        {
            builder.Append(", activeSelf=");
            builder.Append(activeSelf.Value ? "True" : "False");
        }

        var activeInHierarchy = TryGetBooleanPropertyOrField(gameObject, "activeInHierarchy");
        if (activeInHierarchy.HasValue)
        {
            builder.Append(", activeInHierarchy=");
            builder.Append(activeInHierarchy.Value ? "True" : "False");
        }

        var childCount = TryGetChildCount(gameObject);
        if (childCount.HasValue)
        {
            builder.Append(", childCount=");
            builder.Append(childCount.Value);
        }

        var childNames = TryGetChildNames(gameObject);
        if (!string.IsNullOrEmpty(childNames))
        {
            builder.Append(", children=");
            builder.Append(childNames);
        }

        builder.Append(')');
        if (!ReferenceEquals(gameObject, value))
        {
            builder.Append(')');
        }

        return builder.ToString();
    }

    private static string Quote(string? value)
    {
        return string.IsNullOrEmpty(value) ? "''" : "'" + value + "'";
    }

    private static string? TryGetStringPropertyOrField(object? instance, string memberName)
    {
        return TryGetPropertyOrFieldValue(instance, memberName) as string;
    }

    private static bool? TryGetBooleanPropertyOrField(object? instance, string memberName)
    {
        var value = TryGetPropertyOrFieldValue(instance, memberName);
        return value is bool boolValue ? boolValue : null;
    }

    private static int? TryGetIntPropertyOrField(object? instance, string memberName)
    {
        var value = TryGetPropertyOrFieldValue(instance, memberName);
        return value is int intValue ? intValue : null;
    }

    private static string? TryGetStringProperty(object? instance, string memberName)
    {
        return TryGetStringPropertyOrField(instance, memberName);
    }

    private static object? GetField(object instance, string memberName)
    {
#pragma warning disable S3011
        var field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(instance);
#pragma warning restore S3011
    }

    private static object? TryGetPropertyOrFieldValue(object? instance, string memberName)
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
            return property.GetValue(instance, null);
#pragma warning restore S3011
        }

        return GetField(instance, memberName);
    }

    private static object? ResolveGameObjectLike(object value)
    {
        var type = value.GetType();
        if (string.Equals(type.Name, "GameObject", StringComparison.Ordinal)
            || string.Equals(type.FullName, "UnityEngine.GameObject", StringComparison.Ordinal))
        {
            return value;
        }

        var directName = TryGetStringPropertyOrField(value, "name");
        var directTransform = TryGetPropertyOrFieldValue(value, "transform");
        if (!string.IsNullOrEmpty(directName) && directTransform is not null)
        {
            return value;
        }

        return TryGetPropertyOrFieldValue(value, "gameObject");
    }

    private static int? TryGetChildCount(object gameObject)
    {
        var direct = TryGetIntPropertyOrField(gameObject, "childCount");
        if (direct.HasValue)
        {
            return direct;
        }

        var transform = TryGetPropertyOrFieldValue(gameObject, "transform");
        return transform is null ? null : TryGetIntPropertyOrField(transform, "childCount");
    }

    private static string? TryGetChildNames(object gameObject)
    {
        var transform = TryGetPropertyOrFieldValue(gameObject, "transform");
        var childCount = transform is null ? TryGetIntPropertyOrField(gameObject, "childCount") : TryGetIntPropertyOrField(transform, "childCount");
        if (!childCount.HasValue || childCount.Value <= 0)
        {
            return null;
        }

        var maxChildren = Math.Min(childCount.Value, 4);
        var builder = new StringBuilder();
        builder.Append('[');
        for (var index = 0; index < maxChildren; index++)
        {
            var child = TryGetChild(transform ?? gameObject, index);
            if (child is null)
            {
                continue;
            }

            if (builder.Length > 1)
            {
                builder.Append(',');
            }

            builder.Append(Quote(Truncate(TryGetStringPropertyOrField(child, "name"))));
        }

        if (childCount.Value > maxChildren)
        {
            if (builder.Length > 1)
            {
                builder.Append(',');
            }

            builder.Append("...");
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static object? TryGetChild(object transformOrGameObject, int index)
    {
#pragma warning disable S3011
        var method = transformOrGameObject.GetType().GetMethod(
            "GetChild",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(int) },
            modifiers: null);
        if (method is null)
        {
            var transform = TryGetPropertyOrFieldValue(transformOrGameObject, "transform");
            if (transform is null)
            {
                return null;
            }

            method = transform.GetType().GetMethod(
                "GetChild",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null);
#pragma warning restore S3011
            return method?.Invoke(transform, new object[] { index });
        }

        return method.Invoke(transformOrGameObject, new object[] { index });
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var normalized = value!.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length <= 64)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 64) + "...";
#pragma warning restore CA1845
    }
}
