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

#endif
}
