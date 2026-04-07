using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
#if HAS_TMP
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class SelectableTextMenuItemObservability
{
    private const int MaxLogsPerBucket = 8;

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildState(object? menuInstance, string phase, out string? logLine)
    {
        logLine = null;
        if (menuInstance is null)
        {
            return false;
        }

        var data = GetPropertyOrFieldValue(menuInstance, "data");
        var command = TryGetStringByCandidates(data, "command", "Command");
        var hotkey = TryGetStringByCandidates(data, "hotkey", "Hotkey");
        var dataText = TryGetStringByCandidates(data, "text", "Text");
        var itemSkin = GetPropertyOrFieldValue(menuInstance, "item");
        var bucket = (command ?? "<null>") + ":" + phase;
        var hitCount = BucketCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLogsPerBucket)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] SelectableTextMenuItemProbe/");
        builder.Append(phase);
        builder.Append(": cmd='");
        builder.Append(Escape(command));
        builder.Append("' hotkey='");
        builder.Append(Escape(hotkey));
        builder.Append("' dataText='");
        builder.Append(Escape(Truncate(dataText)));
        builder.Append('\'');

        AppendTmpState(builder, itemSkin);
        logLine = builder.ToString();
        return true;
    }

    private static void AppendTmpState(StringBuilder builder, object? itemSkin)
    {
        builder.Append(" itemSkin='");
        builder.Append(itemSkin?.GetType().FullName ?? string.Empty);
        builder.Append('\'');

#if HAS_TMP
        if (itemSkin is Component component)
        {
            var tmp = component.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (tmp is not null)
            {
                tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                builder.Append(" tmpText='");
                builder.Append(Escape(Truncate(tmp.text)));
                builder.Append("' charCount=");
                builder.Append(tmp.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" pageCount=");
                builder.Append(tmp.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" tmpAlpha=");
                builder.Append(tmp.color.a.ToString("0.##", CultureInfo.InvariantCulture));

                var rect = tmp.rectTransform.rect;
                builder.Append(" rect=");
                builder.Append(rect.width.ToString("0.##", CultureInfo.InvariantCulture));
                builder.Append('x');
                builder.Append(rect.height.ToString("0.##", CultureInfo.InvariantCulture));
                builder.Append(" cull=");
                builder.Append(TryGetCanvasCull(tmp)?.ToString() ?? "<unknown>");
                builder.Append(" groupAlpha=");
                builder.Append(TryGetParentCanvasGroupAlpha(tmp)?.ToString("0.##", CultureInfo.InvariantCulture) ?? "<unknown>");
            }
        }
#endif
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
        return value!.Length <= 96 ? value : value.Substring(0, 96) + "...";
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

        return Access(instance, memberName) as string;
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

        return Access(instance, memberName);
    }

    private static object? Access(object instance, string memberName)
    {
        var type = instance.GetType();
#pragma warning disable S3011
        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        return field?.GetValue(instance);
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

#if HAS_TMP
    private static bool? TryGetCanvasCull(TMP_Text tmp)
    {
 #pragma warning disable S3011
        var property = tmp.GetType().GetProperty("canvasRenderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
 #pragma warning restore S3011
        var canvasRenderer = property?.GetValue(tmp, null);
        if (canvasRenderer is null)
        {
            return null;
        }

 #pragma warning disable S3011
        var cullProperty = canvasRenderer.GetType().GetProperty("cull", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
 #pragma warning restore S3011
        if (cullProperty?.PropertyType != typeof(bool))
        {
            return null;
        }

        return cullProperty.GetValue(canvasRenderer, null) as bool?;
    }

    private static float? TryGetParentCanvasGroupAlpha(Component component)
    {
        var current = component.transform.parent;
        while (current is not null)
        {
            var group = current.GetComponent("CanvasGroup");
            if (group is not null)
            {
 #pragma warning disable S3011
                var alphaProperty = group.GetType().GetProperty("alpha", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
 #pragma warning restore S3011
                if (alphaProperty?.PropertyType == typeof(float))
                {
 #pragma warning disable S3011
                    return alphaProperty.GetValue(group, null) as float?;
 #pragma warning restore S3011
                }
            }

            current = current.parent;
        }

        return null;
    }
#endif
}
