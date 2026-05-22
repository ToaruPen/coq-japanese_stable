#if HAS_TMP
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class ScreenHierarchyObservability
{
#if HAS_TMP
    private const int MaxLogsPerBucket = 4;
    private const int MaxChildrenPerAnchor = 8;
    private const int MaxExpansionDepth = 4;

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildNeighborhoodSnapshot(object? screenInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (screenInstance is not Component component)
        {
            return false;
        }

        var root = component.transform;
        var bucket = probeName + ":" + root.name;
        var hitCount = BucketCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLogsPerBucket)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": ");
        AppendAnchor(builder, "self", root);
        AppendAnchor(builder, "parent", root.parent);
        AppendAnchor(builder, "grandparent", root.parent is null ? null : root.parent.parent);
        logLine = builder.ToString();
        return true;
    }

    internal static bool TryBuildLineItemSnapshot(object? lineInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (lineInstance is not Component component)
        {
            return false;
        }

        var modes = FindDirectChildByName(component.transform, "Modes");
        var item = modes is null ? null : FindDirectChildByName(modes, "Item");
        if (item is null)
        {
            return false;
        }

        var bucket = probeName + ":" + component.gameObject.name;
        var hitCount = BucketCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLogsPerBucket)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": root='");
        builder.Append(component.gameObject.name);
        builder.Append("' item=");
        AppendSubtreeSummary(builder, item);
        builder.Append(" children=[");
        for (var index = 0; index < item.childCount; index++)
        {
            if (index > 0)
            {
                builder.Append(" | ");
            }

            var child = item.GetChild(index);
            AppendSubtreeSummary(builder, child);
            if (string.Equals(child.name, "TextShell", StringComparison.Ordinal))
            {
                AppendTextShellChildStates(builder, child);
            }
            AppendInterestingDescendants(builder, child, depth: 1);
        }

        builder.Append(']');
        logLine = builder.ToString();
        return true;
    }

    private static void AppendTextShellChildStates(StringBuilder builder, Transform textShell)
    {
        builder.Append(" textShellChildren=[");
        for (var index = 0; index < textShell.childCount; index++)
        {
            if (index > 0)
            {
                builder.Append(" | ");
            }

            var child = textShell.GetChild(index);
            builder.Append(child.name);
            builder.Append('#');
            builder.Append(child.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));

            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp is null)
            {
                builder.Append("{tmp=<null>}");
                continue;
            }

            tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            builder.Append("{active=");
            builder.Append(tmp.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(", enabled=");
            builder.Append(tmp.enabled ? "True" : "False");
            builder.Append(", chars=");
            builder.Append(tmp.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(", pageCount=");
            builder.Append(tmp.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(", text='");
            builder.Append(CompactText(tmp.text ?? string.Empty));
            builder.Append("'}");
        }

        builder.Append(']');
    }

    private static void AppendAnchor(StringBuilder builder, string label, Transform? anchor)
    {
        if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
        {
            builder.Append("; ");
        }

        builder.Append(label);
        builder.Append('=');
        if (anchor is null)
        {
            builder.Append("<null>");
            return;
        }

        AppendSubtreeSummary(builder, anchor);
        builder.Append(" children=[");
        var count = Math.Min(anchor.childCount, MaxChildrenPerAnchor);
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append(" | ");
            }

            var child = anchor.GetChild(index);
            AppendSubtreeSummary(builder, child);
            AppendInterestingDescendants(builder, child, depth: 1);
        }

        if (anchor.childCount > count)
        {
            builder.Append(" | ...+");
            builder.Append((anchor.childCount - count).ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    private static Transform? FindDirectChildByName(Transform parent, string name)
    {
        for (var index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static string CompactText(string value)
    {
        var normalized = value.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length <= 40)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 40) + "...";
#pragma warning restore CA1845
    }

    private static void AppendInterestingDescendants(StringBuilder builder, Transform transform, int depth)
    {
        if (depth > MaxExpansionDepth || !ShouldExpand(transform))
        {
            return;
        }

        builder.Append(" -> [");
        var count = Math.Min(transform.childCount, MaxChildrenPerAnchor);
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append(" | ");
            }

            var child = transform.GetChild(index);
            AppendSubtreeSummary(builder, child);
            AppendInterestingDescendants(builder, child, depth + 1);
        }

        if (transform.childCount > count)
        {
            builder.Append(" | ...+");
            builder.Append((transform.childCount - count).ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    private static bool ShouldExpand(Transform transform)
    {
#pragma warning disable CA2249
        return transform.name.IndexOf("Scroller", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("Scrollers", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("Scroll View", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("Viewport", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("Content", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("TextShell", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("Compare", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.name.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0
               || transform.childCount <= 4 && transform.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true).Length >= 8;
#pragma warning restore CA2249
    }

    private static void AppendSubtreeSummary(StringBuilder builder, Transform transform)
    {
        var gameObject = transform.gameObject;
        builder.Append(transform.name);
        builder.Append("{activeSelf=");
        builder.Append(gameObject.activeSelf ? "True" : "False");
        builder.Append(", activeInHierarchy=");
        builder.Append(gameObject.activeInHierarchy ? "True" : "False");
        builder.Append(", childCount=");
        builder.Append(transform.childCount.ToString(CultureInfo.InvariantCulture));

        var tmpTexts = transform.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        builder.Append(", tmp=");
        builder.Append(tmpTexts.Length.ToString(CultureInfo.InvariantCulture));

        var sample = FindSampleText(tmpTexts);
        if (!string.IsNullOrEmpty(sample))
        {
            builder.Append(", sample='");
                builder.Append(Truncate(sample!));
            builder.Append('\'');
        }

        builder.Append('}');
    }

    private static string? FindSampleText(IReadOnlyList<TextMeshProUGUI> texts)
    {
        for (var index = 0; index < texts.Count; index++)
        {
            var current = texts[index].text;
            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            if (ContainsInterestingToken(current))
            {
                return current;
            }
        }

        for (var index = 0; index < texts.Count; index++)
        {
            var current = texts[index].text;
            if (!string.IsNullOrWhiteSpace(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool ContainsInterestingToken(string value)
    {
#pragma warning disable CA2249
        return value.IndexOf("This Item", StringComparison.Ordinal) >= 0
               || value.IndexOf("Equipped Item", StringComparison.Ordinal) >= 0
               || value.IndexOf("(unburnt)", StringComparison.Ordinal) >= 0
               || value.IndexOf("[empty]", StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
    }

    private static string Truncate(string value)
    {
        var normalized = value.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length <= 64)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 64) + "...";
#pragma warning restore CA1845
    }
#else
    internal static bool TryBuildNeighborhoodSnapshot(object? screenInstance, string probeName, out string? logLine)
    {
        _ = screenInstance;
        _ = probeName;
        logLine = null;
        return false;
    }
#endif
}
