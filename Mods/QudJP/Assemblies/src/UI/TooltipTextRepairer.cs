using System;
using System.Reflection;

#if HAS_TMP
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UguiText = UnityEngine.UI.Text;
#endif

namespace QudJP;

internal static class TooltipTextRepairer
{
    private const string ReplacementObjectName = "QudJPReplacementText";

    internal static (int MaxVisibleCharacters, int MaxVisibleLines, int PageToDisplay) NormalizeVisibilityLimits(
        int maxVisibleCharacters,
        int maxVisibleLines,
        int pageToDisplay)
    {
        return (
            maxVisibleCharacters <= 0 ? int.MaxValue : maxVisibleCharacters,
            maxVisibleLines <= 0 ? int.MaxValue : maxVisibleLines,
            pageToDisplay <= 0 ? 1 : pageToDisplay);
    }

    internal static bool CanRepairText(bool enabled, bool activeInHierarchy, string? text, string objectName)
    {
        return enabled
            && activeInHierarchy
            && !string.IsNullOrEmpty(text)
            && !string.Equals(objectName, ReplacementObjectName, System.StringComparison.Ordinal);
    }

    internal static bool IsLookerTooltipName(string? objectName)
    {
        return string.Equals(objectName, "PolatLooker", StringComparison.Ordinal);
    }

    internal static bool ShouldRepairTooltipName(string? objectName)
    {
        return !string.IsNullOrEmpty(objectName);
    }

#if HAS_TMP
#pragma warning disable S3011
    private const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
#pragma warning restore S3011

    internal static bool TryRepairTooltip(object? triggerInstance, bool restoreCanvasRendererVisibility = false)
    {
        GameObject? tooltipObject = null;
        try
        {
            tooltipObject = TryGetTooltipObjectFromTrigger(triggerInstance, out var tooltip);
            if (tooltipObject == null || !ShouldRepairTooltipName(tooltipObject.name))
            {
                return false;
            }

            _ = TryPinLookerTooltip(triggerInstance, tooltip, tooltipObject);
            var repaired = 0;
            repaired += ApplyLegacyFonts(tooltipObject);
            repaired += ApplyTmpFonts(tooltipObject);
            repaired += TmpTextRepairer.TryRepairInvisibleTexts(tooltipObject.transform);
            repaired += TooltipReplacementRenderer.TryRenderReplacementTexts(tooltipObject.transform);
            if (restoreCanvasRendererVisibility)
            {
                repaired += RestoreCanvasRendererVisibility(tooltipObject);
            }

            ForceRebuildLayout(tooltipObject);
            return repaired > 0;
        }
        catch (Exception ex)
        {
            var triggerType = triggerInstance?.GetType().FullName ?? "<null>";
            var tooltipName = tooltipObject == null ? "<null>" : tooltipObject.name;
            System.Diagnostics.Trace.TraceError(
                "QudJP: TooltipTextRepairer.TryRepairTooltip failed for trigger '{0}', tooltip '{1}': {2}",
                triggerType,
                tooltipName,
                ex);
            return false;
        }
    }

    internal static bool TryPinLookerTooltip(object? triggerInstance)
    {
        var tooltipObject = TryGetTooltipObjectFromTrigger(triggerInstance, out var tooltip);
        if (tooltipObject == null)
        {
            return false;
        }

        return TryPinLookerTooltip(triggerInstance, tooltip, tooltipObject);
    }

    internal static bool ShouldScheduleRepair(object? triggerInstance)
    {
        var tooltipObject = TryGetTooltipObjectFromTrigger(triggerInstance, out _);
        return ShouldRepairTooltip(tooltipObject);
    }

    internal static bool TryRestoreLookerTooltipVisibility(object? tooltipInstance)
    {
        var tooltipObject = GetTooltipObject(tooltipInstance);
        if (tooltipObject == null || !ShouldRepairTooltipName(tooltipObject.name))
        {
            return false;
        }

        return RestoreCanvasRendererVisibility(tooltipObject) > 0;
    }

    private static bool TryPinLookerTooltip(object? triggerInstance, object? tooltip, GameObject tooltipObject)
    {
        if (!IsLookerTooltipName(tooltipObject.name))
        {
            return false;
        }

        var updated = false;
        updated |= TrySetBooleanMember(triggerInstance, "isManuallyTriggered", true);
        updated |= TrySetBooleanMember(triggerInstance, "staysOpen", true);
        updated |= TrySetBooleanMember(tooltip, "StaysOpen", true);
        return updated;
    }

    private static bool ShouldRepairTooltip(GameObject? tooltipObject)
    {
        return tooltipObject != null && ShouldRepairTooltipName(tooltipObject.name);
    }

    private static int RestoreCanvasRendererVisibility(GameObject root)
    {
        var restored = 0;
        var renderers = root.GetComponentsInChildren<CanvasRenderer>(includeInactive: true);
        for (var index = 0; index < renderers.Length; index++)
        {
            if (!renderers[index].gameObject.activeInHierarchy)
            {
                continue;
            }

            renderers[index].SetAlpha(1f);
            restored++;
        }

        return restored;
    }

    private static int ApplyLegacyFonts(GameObject root)
    {
        var applied = 0;
        var texts = root.GetComponentsInChildren<UguiText>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            if (!CanRepairText(text.enabled, text.gameObject.activeInHierarchy, text.text, text.gameObject.name))
            {
                continue;
            }

            FontManager.ApplyToLegacyText(text);
            applied++;
        }

        return applied;
    }

    private static int ApplyTmpFonts(GameObject root)
    {
        var applied = 0;
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            if (RepairTmpText(texts[index]))
            {
                applied++;
            }
        }

        return applied;
    }

    private static bool RepairTmpText(TextMeshProUGUI text)
    {
        var currentText = text.text;
        if (!CanRepairText(text.enabled, text.gameObject.activeInHierarchy, currentText, text.gameObject.name))
        {
            return false;
        }

        _ = FontManager.TryWarmPrimaryFontCharactersForUi(currentText);
        FontManager.ApplyToText(text);
        ApplySharedFontMaterial(text);

        var limits = NormalizeVisibilityLimits(
            text.maxVisibleCharacters,
            text.maxVisibleLines,
            text.pageToDisplay);
        text.maxVisibleCharacters = limits.MaxVisibleCharacters;
        text.maxVisibleLines = limits.MaxVisibleLines;
        text.pageToDisplay = limits.PageToDisplay;

        RefreshTextMesh(text, currentText);
        if (text.textInfo.characterCount > 0)
        {
            return true;
        }

        FontManager.ForcePrimaryFont(text);
        ApplySharedFontMaterial(text);

        RefreshTextMesh(text, currentText);
        return text.textInfo.characterCount > 0;
    }

    private static void ApplySharedFontMaterial(TextMeshProUGUI text)
    {
        if (text.font is not null)
        {
            text.fontSharedMaterial = text.font.material;
        }
    }

    private static void RefreshTextMesh(TextMeshProUGUI text, string currentText)
    {
        text.havePropertiesChanged = true;
        text.UpdateMeshPadding();
        text.SetAllDirty();
        text.RecalculateClipping();
        text.RecalculateMasking();
        text.text = currentText;
        ForceUpdateCanvases();
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
    }

    private static void ForceRebuildLayout(GameObject root)
    {
        if (root.transform is RectTransform rectTransform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        ForceUpdateCanvases();
    }


    private static GameObject? TryGetTooltipObjectFromTrigger(object? triggerInstance, out object? tooltip)
    {
        tooltip = ReflectionUtils.GetPropertyOrFieldValue(triggerInstance, "Tooltip");
        return GetTooltipObject(tooltip);
    }

    private static GameObject? GetTooltipObject(object? tooltip)
    {
        return ReflectionUtils.GetPropertyOrFieldValue(tooltip, "GameObject") as GameObject;
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
            Debug.LogWarning($"[QudJP] TooltipTextRepairer: ForceUpdateCanvases failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TrySetBooleanMember(object? instance, string memberName, bool value)
    {
        if (instance is null)
        {
            return false;
        }

        var type = instance.GetType();
#pragma warning disable S3011
        var property = type.GetProperty(memberName, InstanceMemberFlags);
#pragma warning restore S3011
        if (property is not null
            && property.PropertyType == typeof(bool)
            && property.CanWrite
            && property.GetIndexParameters().Length == 0)
        {
            property.SetValue(instance, value);
            return true;
        }

#pragma warning disable S3011
        var field = type.GetField(memberName, InstanceMemberFlags);
#pragma warning restore S3011
        if (field is not null && field.FieldType == typeof(bool))
        {
            field.SetValue(instance, value);
            return true;
        }

        return false;
    }
#endif
}
