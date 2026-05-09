#if HAS_TMP
using System;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace QudJP;

internal static class TooltipReplacementRenderer
{
    private const string ReplacementObjectName = "QudJPTooltipReplacementText";

    internal static bool ShouldAttemptReplacementForTests(
        bool enabled,
        bool activeInHierarchy,
        string? text,
        string objectName,
        int characterCount)
    {
        return enabled
            && HasVisibleSourceText(activeInHierarchy, text)
            && characterCount == 0
            && !string.Equals(objectName, ReplacementObjectName, StringComparison.Ordinal);
    }

    internal static bool ShouldDisableReplacementForTests(
        bool enabled,
        bool activeInHierarchy,
        int characterCount)
    {
        return enabled && activeInHierarchy && characterCount > 0;
    }

    internal static bool ShouldRefreshExistingReplacementForTests(
        bool replacementExists,
        bool activeInHierarchy,
        string? text)
    {
        return replacementExists && HasVisibleSourceText(activeInHierarchy, text);
    }

    internal static bool ShouldHideExistingReplacementForTests(
        bool replacementExists,
        bool activeInHierarchy,
        string? text)
    {
        return replacementExists && !HasVisibleSourceText(activeInHierarchy, text);
    }

    internal static int TryRenderReplacementTexts(object? componentInstance)
    {
        if (componentInstance is not Component component)
        {
            return 0;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var replaced = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            var original = texts[index];
            if (string.Equals(original.gameObject.name, ReplacementObjectName, StringComparison.Ordinal))
            {
                continue;
            }

            original.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            var existingReplacement = GetExistingReplacement(original);
            if (ShouldHideExistingReplacementForTests(
                    existingReplacement is not null,
                    original.gameObject.activeInHierarchy,
                    original.text))
            {
                HideReplacement(existingReplacement);
                continue;
            }

            if (ShouldDisableReplacementForTests(
                    original.enabled,
                    original.gameObject.activeInHierarchy,
                    original.textInfo.characterCount))
            {
                TryDisableReplacement(original.transform.parent);
                continue;
            }

            if (ShouldRefreshExistingReplacementForTests(
                    existingReplacement is not null,
                    original.gameObject.activeInHierarchy,
                    original.text))
            {
                if (RenderReplacement(existingReplacement!, original))
                {
                    original.enabled = false;
                    replaced++;
                }

                continue;
            }

            if (!ShouldAttemptReplacementForTests(
                    original.enabled,
                    original.gameObject.activeInHierarchy,
                    original.text,
                    original.gameObject.name,
                    original.textInfo.characterCount))
            {
                continue;
            }

            var replacement = GetOrCreateReplacement(original);
            if (replacement is null)
            {
                continue;
            }

            if (!RenderReplacement(replacement, original))
            {
                continue;
            }

            original.enabled = false;
            replaced++;
        }

        return replaced;
    }

    private static bool HasVisibleSourceText(bool activeInHierarchy, string? text)
    {
        return activeInHierarchy && !string.IsNullOrEmpty(text);
    }

    private static TextMeshProUGUI? GetExistingReplacement(TextMeshProUGUI original)
    {
        if (original.transform.parent is not RectTransform parent)
        {
            return null;
        }

        var existing = parent.Find(ReplacementObjectName);
        return existing?.GetComponent<TextMeshProUGUI>();
    }

    private static TextMeshProUGUI? GetOrCreateReplacement(TextMeshProUGUI original)
    {
        if (original.transform.parent is not RectTransform parent)
        {
            return null;
        }

        var existing = parent.Find(ReplacementObjectName);
        if (existing is not null)
        {
            return existing.GetComponent<TextMeshProUGUI>();
        }

        var gameObject = new GameObject(ReplacementObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        gameObject.layer = original.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        gameObject.SetActive(false);

        var replacement = gameObject.GetComponent<TextMeshProUGUI>();
        if (replacement is null)
        {
            return null;
        }

        replacement.enabled = false;
        return replacement;
    }

    private static bool RenderReplacement(TextMeshProUGUI replacement, TextMeshProUGUI original)
    {
        SyncReplacement(replacement, original);
        replacement.gameObject.SetActive(true);
        replacement.enabled = true;
        RefreshReplacement(replacement);
        if (replacement.textInfo.characterCount == 0)
        {
            FontManager.ForcePrimaryFont(replacement);
            RefreshReplacement(replacement);
        }

        if (replacement.textInfo.characterCount > 0)
        {
            replacement.transform.SetAsLastSibling();
            return true;
        }

        HideReplacement(replacement);
        return false;
    }

    private static void SyncReplacement(TextMeshProUGUI replacement, TextMeshProUGUI original)
    {
        var replacementRect = replacement.rectTransform;
        replacementRect.anchorMin = original.rectTransform.anchorMin;
        replacementRect.anchorMax = original.rectTransform.anchorMax;
        replacementRect.pivot = original.rectTransform.pivot;
        replacementRect.anchoredPosition = original.rectTransform.anchoredPosition;
        replacementRect.sizeDelta = original.rectTransform.sizeDelta;
        replacementRect.localScale = original.rectTransform.localScale;
        replacementRect.localRotation = original.rectTransform.localRotation;

        replacement.fontSize = original.fontSize;
        replacement.fontSizeMin = original.fontSizeMin;
        replacement.fontSizeMax = original.fontSizeMax;
        replacement.enableAutoSizing = original.enableAutoSizing;
        replacement.font = original.font;
        replacement.fontStyle = original.fontStyle;
        replacement.alignment = original.alignment;
        replacement.overflowMode = TextOverflowModes.Overflow;
        replacement.textWrappingMode = original.textWrappingMode;
        replacement.margin = original.margin;
        replacement.color = original.color;
        replacement.alpha = original.alpha;
        replacement.raycastTarget = original.raycastTarget;
        replacement.maskable = original.maskable;
        replacement.richText = original.richText;
        replacement.isRightToLeftText = original.isRightToLeftText;
        replacement.characterSpacing = original.characterSpacing;
        replacement.wordSpacing = original.wordSpacing;
        replacement.lineSpacing = original.lineSpacing;
        replacement.paragraphSpacing = original.paragraphSpacing;

        var limits = TooltipTextRepairer.NormalizeVisibilityLimits(
            original.maxVisibleCharacters,
            original.maxVisibleLines,
            original.pageToDisplay);
        replacement.maxVisibleCharacters = limits.MaxVisibleCharacters;
        replacement.maxVisibleLines = limits.MaxVisibleLines;
        replacement.pageToDisplay = limits.PageToDisplay;

        if (original.fontSharedMaterial is not null)
        {
            replacement.fontSharedMaterial = original.fontSharedMaterial;
        }

        replacement.text = original.text;
        FontManager.ApplyToText(replacement);
        if (replacement.font is not null)
        {
            replacement.fontSharedMaterial = replacement.font.material;
        }
    }

    private static void RefreshReplacement(TextMeshProUGUI replacement)
    {
        replacement.havePropertiesChanged = true;
        replacement.UpdateMeshPadding();
        replacement.SetAllDirty();
        replacement.RecalculateClipping();
        replacement.RecalculateMasking();
        ForceUpdateCanvases();
        replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
    }

    private static void TryDisableReplacement(Transform? parent)
    {
        if (parent is null)
        {
            return;
        }

        var replacementTransform = parent.Find(ReplacementObjectName);
        if (replacementTransform is null)
        {
            return;
        }

        HideReplacement(replacementTransform.GetComponent<TextMeshProUGUI>());
    }

    private static void HideReplacement(TextMeshProUGUI? replacement)
    {
        if (replacement is not null)
        {
            replacement.enabled = false;
            replacement.gameObject.SetActive(false);
        }
    }

    private static void ForceUpdateCanvases()
    {
        try
        {
            var canvasType = Type.GetType("UnityEngine.Canvas, UnityEngine.UIModule", throwOnError: false);
            if (canvasType is null)
            {
                canvasType = Type.GetType("UnityEngine.Canvas, UnityEngine.CoreModule", throwOnError: false);
            }

            if (canvasType is null)
            {
                canvasType = Type.GetType("UnityEngine.Canvas, UnityEngine", throwOnError: false);
            }

            var method = canvasType?.GetMethod(
                "ForceUpdateCanvases",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            method?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[QudJP] TooltipReplacementRenderer: ForceUpdateCanvases failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
#endif
