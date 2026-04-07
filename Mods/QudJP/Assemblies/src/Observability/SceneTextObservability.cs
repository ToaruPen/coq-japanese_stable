#if HAS_TMP
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UguiText = UnityEngine.UI.Text;
#endif

namespace QudJP;

internal static class SceneTextObservability
{
#if HAS_TMP
    private static readonly string[] InterestingTokens =
    {
        "This Item",
        "Equipped Item",
        "Offhand Attack Chance",
    };

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildCompareSceneSnapshot(string probeName, out string? logLine)
    {
        logLine = null;

        var allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        var interesting = new List<TextMeshProUGUI>();
        var interestingLegacy = new List<UguiText>();
        for (var index = 0; index < allTexts.Length; index++)
        {
            var text = allTexts[index];
            if (!text.enabled || !text.gameObject.activeInHierarchy)
            {
                continue;
            }

            var current = text.text ?? string.Empty;
            if (!ContainsInterestingToken(current))
            {
                continue;
            }

            interesting.Add(text);
        }

        var allLegacyTexts = Resources.FindObjectsOfTypeAll<UguiText>();
        for (var index = 0; index < allLegacyTexts.Length; index++)
        {
            var text = allLegacyTexts[index];
            if (!text.enabled || !text.gameObject.activeInHierarchy)
            {
                continue;
            }

            var current = text.text ?? string.Empty;
            if (!ContainsInterestingToken(current))
            {
                continue;
            }

            interestingLegacy.Add(text);
        }

        if (interesting.Count == 0 && interestingLegacy.Count == 0)
        {
            return false;
        }

        var bucket = BuildBucketKey(probeName, interesting, interestingLegacy);
        var hitCount = BucketCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > 4)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": ");
        var matchIndex = 0;
        for (var index = 0; index < interesting.Count; index++)
        {
            if (matchIndex > 0)
            {
                builder.Append("; ");
            }

            AppendTextContext(builder, interesting[index], matchIndex);
            matchIndex++;
        }

        for (var index = 0; index < interestingLegacy.Count; index++)
        {
            if (matchIndex > 0)
            {
                builder.Append("; ");
            }

            AppendLegacyTextContext(builder, interestingLegacy[index], matchIndex);
            matchIndex++;
        }

        logLine = builder.ToString();
        return true;
    }

    internal static bool TryBuildPopupContainerSnapshot(string probeName, out string? logLine)
    {
        logLine = null;

        var matches = new List<string>();
        CollectPopupMatches(Resources.FindObjectsOfTypeAll<TextMeshProUGUI>(), matches, isLegacy: false);
        CollectPopupMatches(Resources.FindObjectsOfTypeAll<UguiText>(), matches, isLegacy: true);
        if (matches.Count == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": ");
        for (var index = 0; index < matches.Count; index++)
        {
            if (index > 0)
            {
                builder.Append("; ");
            }

            builder.Append(matches[index]);
        }

        logLine = builder.ToString();
        return true;
    }

    internal static void ResetBuckets()
    {
        BucketCounts.Clear();
    }

    private static bool ContainsInterestingToken(string value)
    {
        for (var index = 0; index < InterestingTokens.Length; index++)
        {
#pragma warning disable CA2249
            if (value.IndexOf(InterestingTokens[index], StringComparison.Ordinal) >= 0)
#pragma warning restore CA2249
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildBucketKey(
        string probeName,
        IReadOnlyList<TextMeshProUGUI> interesting,
        IReadOnlyList<UguiText> interestingLegacy)
    {
        var builder = new StringBuilder();
        builder.Append(probeName);
        builder.Append(':');

        for (var index = 0; index < interesting.Count && index < 3; index++)
        {
            var text = interesting[index];
            builder.Append(BuildPath(text.transform));
            builder.Append('=');
            builder.Append(Truncate(text.text));
            builder.Append(';');
        }

        builder.Append('|');
        for (var index = 0; index < interestingLegacy.Count && index < 3; index++)
        {
            var text = interestingLegacy[index];
            builder.Append(BuildPath(text.transform));
            builder.Append('=');
            builder.Append(Truncate(text.text));
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static void CollectPopupMatches<T>(T[] texts, List<string> matches, bool isLegacy)
        where T : Component
    {
        for (var index = 0; index < texts.Length && matches.Count < 20; index++)
        {
            var component = texts[index];
            if (component is null || component.gameObject is null || !component.gameObject.activeInHierarchy)
            {
                continue;
            }

            string textValue;
            if (component is TextMeshProUGUI tmp)
            {
                textValue = tmp.text ?? string.Empty;
            }
            else if (component is UguiText ui)
            {
                textValue = ui.text ?? string.Empty;
            }
            else
            {
                continue;
            }

            var path = BuildPath(component.transform);
#pragma warning disable CA2249
            if (path.IndexOf("PopupMessage", StringComparison.Ordinal) < 0
                && path.IndexOf("Tooltip Container", StringComparison.Ordinal) < 0
                && path.IndexOf("PolatLooker", StringComparison.Ordinal) < 0)
#pragma warning restore CA2249
            {
                continue;
            }

            matches.Add((isLegacy ? "ui:" : "tmp:") + path + "='" + Truncate(textValue) + "'");
        }
    }

    private static void AppendTextContext(StringBuilder builder, TextMeshProUGUI text, int index)
    {
        builder.Append("match[");
        builder.Append(index.ToString(CultureInfo.InvariantCulture));
        builder.Append("] path='");
        builder.Append(BuildPath(text.transform));
        builder.Append("' text='");
        builder.Append(Truncate(text.text));
        builder.Append("' parent='");
        builder.Append(text.transform.parent is null ? string.Empty : text.transform.parent.name);
        builder.Append("' siblings=");

        var parent = text.transform.parent;
        if (parent is null)
        {
            builder.Append("<none>");
            return;
        }

        var siblingTexts = parent.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        builder.Append('[');
        for (var siblingIndex = 0; siblingIndex < siblingTexts.Length; siblingIndex++)
        {
            if (siblingIndex > 0)
            {
                builder.Append(" | ");
            }

            var sibling = siblingTexts[siblingIndex];
            builder.Append(sibling.gameObject.name);
            builder.Append('=');
            builder.Append('\'');
            builder.Append(Truncate(sibling.text));
            builder.Append('\'');
        }

        builder.Append(']');
    }

    private static void AppendLegacyTextContext(StringBuilder builder, UguiText text, int index)
    {
        builder.Append("ui[");
        builder.Append(index.ToString(CultureInfo.InvariantCulture));
        builder.Append("] path='");
        builder.Append(BuildPath(text.transform));
        builder.Append("' text='");
        builder.Append(Truncate(text.text));
        builder.Append("' parent='");
        builder.Append(text.transform.parent is null ? string.Empty : text.transform.parent.name);
        builder.Append("' siblings=");

        var parent = text.transform.parent;
        if (parent is null)
        {
            builder.Append("<none>");
            return;
        }

        var siblingTexts = parent.GetComponentsInChildren<UguiText>(includeInactive: true);
        builder.Append('[');
        for (var siblingIndex = 0; siblingIndex < siblingTexts.Length; siblingIndex++)
        {
            if (siblingIndex > 0)
            {
                builder.Append(" | ");
            }

            var sibling = siblingTexts[siblingIndex];
            builder.Append(sibling.gameObject.name);
            builder.Append('=');
            builder.Append('\'');
            builder.Append(Truncate(sibling.text));
            builder.Append('\'');
        }

        builder.Append(']');
    }

    private static string BuildPath(Transform transform)
    {
        var names = new List<string>();
        Transform? current = transform;
        while (current is not null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
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
#else
    internal static bool TryBuildCompareSceneSnapshot(string probeName, out string? logLine)
    {
        _ = probeName;
        logLine = null;
        return false;
    }
#endif
}
