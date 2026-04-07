#if HAS_TMP
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UguiText = UnityEngine.UI.Text;
#endif

namespace QudJP;

internal static class ComparePopupTextFixer
{
#if HAS_TMP
    private static readonly string[] HeaderTokens =
    {
        "This Item",
        "Equipped Item",
        "Offhand Attack Chance",
    };

    internal static bool TryRepairActiveComparePopup(out string? logLine)
    {
        logLine = null;

        var roots = FindCandidateRoots();
        if (roots.Count == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ComparePopupTextRepair/v1:");

        var repairedLegacy = 0;
        var repairedTmp = 0;
        var repairedInvisible = 0;
        var replacementCount = 0;
        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            repairedLegacy += ApplyLegacyFonts(root);
            repairedTmp += ApplyTmpFonts(root);
            repairedInvisible += TmpTextRepairer.TryRepairInvisibleTexts(root);
            replacementCount += TextShellReplacementRenderer.TryRenderReplacementTexts(root, out _);

            builder.Append(" root[");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
            builder.Append("]='");
            builder.Append(BuildPath(root));
            builder.Append("' texts=");
            AppendTextSummary(builder, root);
        }

        builder.Append(" legacyApplied=");
        builder.Append(repairedLegacy.ToString(CultureInfo.InvariantCulture));
        builder.Append(" tmpApplied=");
        builder.Append(repairedTmp.ToString(CultureInfo.InvariantCulture));
        builder.Append(" invisibleRepaired=");
        builder.Append(repairedInvisible.ToString(CultureInfo.InvariantCulture));
        builder.Append(" replacements=");
        builder.Append(replacementCount.ToString(CultureInfo.InvariantCulture));

        logLine = builder.ToString();
        return true;
    }

    internal static bool TryRepairAnyActivePopup(out string? logLine)
    {
        logLine = null;

        var roots = FindActivePopupRoots();
        if (roots.Count == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] PopupContainerTextRepair/v1:");

        var repairedLegacy = 0;
        var repairedTmp = 0;
        var repairedInvisible = 0;
        var replacementCount = 0;
        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            repairedLegacy += ApplyLegacyFonts(root);
            repairedTmp += ApplyTmpFonts(root);
            repairedInvisible += TmpTextRepairer.TryRepairInvisibleTexts(root);
            replacementCount += TextShellReplacementRenderer.TryRenderReplacementTexts(root, out _);

            builder.Append(" root[");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
            builder.Append("]='");
            builder.Append(BuildPath(root));
            builder.Append("' texts=");
            AppendTextSummary(builder, root);
        }

        builder.Append(" legacyApplied=");
        builder.Append(repairedLegacy.ToString(CultureInfo.InvariantCulture));
        builder.Append(" tmpApplied=");
        builder.Append(repairedTmp.ToString(CultureInfo.InvariantCulture));
        builder.Append(" invisibleRepaired=");
        builder.Append(repairedInvisible.ToString(CultureInfo.InvariantCulture));
        builder.Append(" replacements=");
        builder.Append(replacementCount.ToString(CultureInfo.InvariantCulture));

        logLine = builder.ToString();
        return true;
    }

    internal static bool RepairActiveComparePopup()
    {
        var roots = FindCandidateRoots();
        if (roots.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            _ = ApplyLegacyFonts(root);
            _ = ApplyTmpFonts(root);
            _ = TmpTextRepairer.TryRepairInvisibleTexts(root);
            _ = TextShellReplacementRenderer.TryRenderReplacementTexts(root, out _);
        }

        return true;
    }

    internal static bool RepairAnyActivePopup()
    {
        var roots = FindActivePopupRoots();
        if (roots.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            _ = ApplyLegacyFonts(root);
            _ = ApplyTmpFonts(root);
            _ = TmpTextRepairer.TryRepairInvisibleTexts(root);
            _ = TextShellReplacementRenderer.TryRenderReplacementTexts(root, out _);
        }

        return true;
    }

    private static List<Transform> FindCandidateRoots()
    {
        var roots = new List<Transform>();
        var seen = new HashSet<int>();

        var legacyTexts = Resources.FindObjectsOfTypeAll<UguiText>();
        for (var index = 0; index < legacyTexts.Length; index++)
        {
            var text = legacyTexts[index];
            if (!text.enabled || !text.gameObject.activeInHierarchy || !ContainsHeaderToken(text.text ?? string.Empty))
            {
                continue;
            }

            AddCandidateRoot(roots, seen, text.transform);
        }

        var tmpTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (var index = 0; index < tmpTexts.Length; index++)
        {
            var text = tmpTexts[index];
            if (!text.enabled || !text.gameObject.activeInHierarchy || !ContainsHeaderToken(text.text ?? string.Empty))
            {
                continue;
            }

            AddCandidateRoot(roots, seen, text.transform);
        }

        return roots;
    }

    private static List<Transform> FindActivePopupRoots()
    {
        var roots = new List<Transform>();
        var seen = new HashSet<int>();

        var legacyTexts = Resources.FindObjectsOfTypeAll<UguiText>();
        for (var index = 0; index < legacyTexts.Length; index++)
        {
            var text = legacyTexts[index];
            if (!text.enabled || !text.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!IsPopupPath(BuildPath(text.transform)))
            {
                continue;
            }

            AddCandidateRoot(roots, seen, text.transform);
        }

        var tmpTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (var index = 0; index < tmpTexts.Length; index++)
        {
            var text = tmpTexts[index];
            if (!text.enabled || !text.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!IsPopupPath(BuildPath(text.transform)))
            {
                continue;
            }

            AddCandidateRoot(roots, seen, text.transform);
        }

        return roots;
    }

    private static void AddCandidateRoot(List<Transform> roots, HashSet<int> seen, Transform anchor)
    {
        var root = anchor.parent?.parent ?? anchor.parent ?? anchor;
        if (!seen.Add(root.GetInstanceID()))
        {
            return;
        }

        roots.Add(root);
    }

    private static bool ContainsHeaderToken(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (var index = 0; index < HeaderTokens.Length; index++)
        {
#pragma warning disable CA2249
            if (value.IndexOf(HeaderTokens[index], StringComparison.OrdinalIgnoreCase) >= 0)
#pragma warning restore CA2249
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPopupPath(string path)
    {
#pragma warning disable CA2249
        return path.IndexOf("PopupMessage", StringComparison.Ordinal) >= 0
            || path.IndexOf("Tooltip Container", StringComparison.Ordinal) >= 0
            || path.IndexOf("PolatLooker", StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
    }

    private static int ApplyLegacyFonts(Transform root)
    {
        var applied = 0;
        var texts = root.GetComponentsInChildren<UguiText>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            if (!text.gameObject.activeInHierarchy)
            {
                continue;
            }

            FontManager.ApplyToLegacyText(text);
            applied++;
        }

        return applied;
    }

    private static int ApplyTmpFonts(Transform root)
    {
        var applied = 0;
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            if (!text.gameObject.activeInHierarchy)
            {
                continue;
            }

            FontManager.ApplyToText(text);
            applied++;
        }

        return applied;
    }

    private static void AppendTextSummary(StringBuilder builder, Transform root)
    {
        var appended = 0;

        var legacyTexts = root.GetComponentsInChildren<UguiText>(includeInactive: true);
        for (var index = 0; index < legacyTexts.Length && appended < 12; index++)
        {
            var text = legacyTexts[index];
            if (!text.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (appended > 0)
            {
                builder.Append(" | ");
            }

            builder.Append("ui:'");
            builder.Append(BuildRelativePath(root, text.transform));
            builder.Append("' text='");
            builder.Append(Truncate(text.text));
            builder.Append("' font='");
            builder.Append(text.font is null ? string.Empty : text.font.name);
            builder.Append('\'');
            appended++;
        }

        var tmpTexts = root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        for (var index = 0; index < tmpTexts.Length && appended < 12; index++)
        {
            var text = tmpTexts[index];
            if (!text.gameObject.activeInHierarchy)
            {
                continue;
            }

            var current = text.text;
            if (string.IsNullOrWhiteSpace(current) && !ContainsHeaderToken(current ?? string.Empty))
            {
                continue;
            }

            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (appended > 0)
            {
                builder.Append(" | ");
            }

            builder.Append("tmp:'");
            builder.Append(BuildRelativePath(root, text.transform));
            builder.Append("' text='");
            builder.Append(Truncate(current));
            builder.Append("' chars=");
            builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pageCount=");
            builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" font='");
            builder.Append(text.font is null ? string.Empty : text.font.name);
            builder.Append('\'');
            appended++;
        }

        if (appended == 0)
        {
            builder.Append("<none>");
        }
    }

    private static string BuildPath(Transform transform)
    {
        var segments = new List<string>();
        Transform? current = transform;
        while (current is not null)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string BuildRelativePath(Transform root, Transform target)
    {
        var segments = new Stack<string>();
        var current = target;
        while (current != root && current is not null)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", segments.ToArray());
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value!.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length <= 60)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 60) + "...";
#pragma warning restore CA1845
    }
#endif
}
