using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
#if HAS_TMP
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class InventoryLineUpdateObservability
{
#if HAS_TMP
    private const int MaxTransitionLogsPerLine = 6;
    private const string LeafSegment = "TextShell/Text";
    private const string ReplacementObjectName = "QudJPReplacementText";

    private static readonly ConcurrentDictionary<int, string> LastSignatureByLine = new();
    private static readonly ConcurrentDictionary<int, int> LogCountByLine = new();

    internal static bool TryBuildTransitionLog(object? lineInstance, out string? logLine)
    {
        logLine = null;
        if (lineInstance is not Component component)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var builder = new StringBuilder();
        builder.Append("[QudJP] InventoryLineUpdateProbe/v1: root='");
        builder.Append(component.gameObject.name);
        builder.Append("' frame=");
        builder.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture));

        var signatureBuilder = new StringBuilder();
        var relevant = 0;

        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            var relativePath = BuildRelativePath(component.transform, text.transform);
#pragma warning disable CA2249
            var isLeaf = relativePath.IndexOf(LeafSegment, StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
            var isReplacement = string.Equals(text.gameObject.name, ReplacementObjectName, StringComparison.Ordinal);
            if (!isLeaf && !isReplacement)
            {
                continue;
            }

            if (!isReplacement && text.textInfo.characterCount > 0)
            {
                continue;
            }

            AppendEntry(builder, signatureBuilder, text, relativePath, relevant, isLeaf ? "leaf" : "replacement");
            relevant++;
        }

        if (relevant == 0)
        {
            return false;
        }

        var lineId = component.GetInstanceID();
        var signature = signatureBuilder.ToString();
        if (LastSignatureByLine.TryGetValue(lineId, out var previous) && string.Equals(previous, signature, StringComparison.Ordinal))
        {
            return false;
        }

        var count = LogCountByLine.AddOrUpdate(lineId, 1, static (_, current) => current + 1);
        if (count > MaxTransitionLogsPerLine)
        {
            LastSignatureByLine[lineId] = signature;
            return false;
        }

        LastSignatureByLine[lineId] = signature;
        logLine = builder.ToString();
        return true;
    }

    private static void AppendEntry(
        StringBuilder builder,
        StringBuilder signatureBuilder,
        TMP_Text text,
        string relativePath,
        int index,
        string kind)
    {
        var rect = text.rectTransform.rect;
        builder.Append("; ");
        builder.Append(kind);
        builder.Append('[');
        builder.Append(index.ToString(CultureInfo.InvariantCulture));
        builder.Append("] path='");
        builder.Append(relativePath);
        builder.Append("' activeSelf=");
        builder.Append(text.gameObject.activeSelf ? "True" : "False");
        builder.Append(" active=");
        builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
        builder.Append(" enabled=");
        builder.Append(text.enabled ? "True" : "False");
        builder.Append(" chars=");
        builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" pages=");
        builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" rect=");
        builder.Append(rect.width.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append('x');
        builder.Append(rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(" parent='");
        builder.Append(text.transform.parent is null ? string.Empty : text.transform.parent.name);
        builder.Append("' text='");
        builder.Append(Truncate(text.text));
        builder.Append('\'');

        signatureBuilder.Append(kind);
        signatureBuilder.Append('|');
        signatureBuilder.Append(relativePath);
        signatureBuilder.Append('|');
        signatureBuilder.Append(text.gameObject.activeSelf ? '1' : '0');
        signatureBuilder.Append(text.gameObject.activeInHierarchy ? '1' : '0');
        signatureBuilder.Append(text.enabled ? '1' : '0');
        signatureBuilder.Append('|');
        signatureBuilder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
        signatureBuilder.Append('|');
        signatureBuilder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
        signatureBuilder.Append('|');
        signatureBuilder.Append(rect.width.ToString("0.###", CultureInfo.InvariantCulture));
        signatureBuilder.Append('x');
        signatureBuilder.Append(rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        signatureBuilder.Append('|');
        signatureBuilder.Append(text.transform.parent is null ? string.Empty : text.transform.parent.name);
        signatureBuilder.Append(';');
    }

    private static string BuildRelativePath(Transform root, Transform target)
    {
        var segments = new System.Collections.Generic.Stack<string>();
        var current = target;
        while (current != root && current is not null)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", segments);
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value!.Replace("\r", "\\r")
            .Replace("\n", "\\n");
#pragma warning disable CA1845
        return normalized.Length <= 80 ? normalized : normalized.Substring(0, 80) + "...";
#pragma warning restore CA1845
    }
#else
    internal static bool TryBuildTransitionLog(object? lineInstance, out string? logLine)
    {
        _ = lineInstance;
        logLine = null;
        return false;
    }
#endif
}
