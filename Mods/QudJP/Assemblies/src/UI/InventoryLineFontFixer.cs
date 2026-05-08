#if HAS_TMP
using TMPro;
using UnityEngine;
#endif
using System;
using System.Threading;

namespace QudJP;

internal static class InventoryLineFontFixer
{
#if HAS_TMP
    private const int MaxDiagnostics = 128;
    private static int diagnosticsCount;

    internal static bool TryApplyPrimaryFontToItemRow(object? inventoryLineInstance, object? data)
    {
        if (inventoryLineInstance is null || data is null)
        {
            return false;
        }

        if (!TryGetBooleanPropertyOrField(data, "category", out var isCategory) || isCategory)
        {
            return false;
        }

        var displayName = TryGetStringPropertyOrField(data, "displayName");
        var textSkin = GetPropertyOrFieldValue(inventoryLineInstance, "text");
        return TryForcePrimaryFontOnTextSkin(textSkin, displayName);
    }

    internal static bool TryForcePrimaryFontOnTextSkin(object? textSkin, string? finalText)
    {
        if (!TryGetTextMeshPro(textSkin, out var tmp) || tmp is null)
        {
            LogDiagnostics(textSkin, null, finalText, applied: false);
            return false;
        }

        if (tmp.gameObject is null || !tmp.gameObject.activeInHierarchy || !tmp.isActiveAndEnabled)
        {
            return false;
        }

        if (textSkin is not null)
        {
            InvokeIfPresent(textSkin, "Apply");
        }
        _ = FontManager.TryWarmPrimaryFontCharactersForUi(finalText);
        FontManager.ForcePrimaryFont(tmp);
        if (tmp.font is not null)
        {
            tmp.fontSharedMaterial = tmp.font.material;
        }

        if (tmp.maxVisibleCharacters <= 0)
        {
            tmp.maxVisibleCharacters = int.MaxValue;
        }

        if (tmp.maxVisibleLines <= 0)
        {
            tmp.maxVisibleLines = int.MaxValue;
        }

        if (tmp.pageToDisplay <= 0)
        {
            tmp.pageToDisplay = 1;
        }

        var currentText = tmp.text;
        tmp.UpdateMeshPadding();
        InvokeIfPresent(tmp, "SetAllDirty");
        InvokeIfPresent(tmp, "SetVerticesDirty");
        InvokeIfPresent(tmp, "SetLayoutDirty");
        InvokeIfPresent(tmp, "SetMaterialDirty");
        InvokeIfPresent(tmp, "RecalculateClipping");
        InvokeIfPresent(tmp, "RecalculateMasking");
        tmp.havePropertiesChanged = true;
        tmp.text = currentText;
        ForceUpdateCanvases();
        tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        LogDiagnostics(textSkin, tmp, finalText, applied: true);
        return tmp.textInfo.characterCount > 0;
    }

    internal static bool IsActiveItemLine(object? inventoryLineInstance)
    {
        if (inventoryLineInstance is not Component component
            || component.gameObject is null
            || !component.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (GetPropertyOrFieldValue(inventoryLineInstance, "categoryMode") is GameObject categoryMode
            && categoryMode.activeSelf)
        {
            return false;
        }

        if (GetPropertyOrFieldValue(inventoryLineInstance, "itemMode") is GameObject itemMode
            && !itemMode.activeSelf)
        {
            return false;
        }

        return true;
    }

    internal static bool TryRefreshActiveItemLine(object? inventoryLineInstance)
    {
        if (!IsActiveItemLine(inventoryLineInstance))
        {
            return false;
        }

        return TryForcePrimaryFontOnTextSkin(
            GetPropertyOrFieldValue(inventoryLineInstance, "text"),
            GetActiveItemLineText(inventoryLineInstance));
    }

    internal static string? GetActiveItemLineText(object? inventoryLineInstance)
    {
        var textSkin = GetPropertyOrFieldValue(inventoryLineInstance, "text");
        var currentText = TryGetStringPropertyOrField(textSkin, "text");
        if (currentText is null)
        {
            currentText = TryGetStringPropertyOrField(textSkin, "Text");
        }

        return currentText;
    }

    internal static bool HasActiveReplacementForCurrentItemText(object? inventoryLineInstance)
    {
        return TextShellReplacementRenderer.HasActiveReplacementForCurrentItemText(inventoryLineInstance);
    }

    internal static int TryApplyPrimaryFontToAllTextChildren(object? inventoryLineInstance)
    {
        if (inventoryLineInstance is not Component component)
        {
            return 0;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var applied = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            FontManager.ApplyToText(texts[index]);
            applied++;
        }

        return applied;
    }

    private static string? TryGetStringPropertyOrField(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(instance) as string;
        }

        return Access(instance, memberName) as string;
    }

    private static bool TryGetBooleanPropertyOrField(object instance, string memberName, out bool value)
    {
        value = false;

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
        {
            value = property.GetValue(instance) as bool? ?? false;
            return true;
        }

        var field = Access(instance, memberName);
        if (field is bool fieldValue)
        {
            value = fieldValue;
            return true;
        }

        return false;
    }

    private static object? GetPropertyOrFieldValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(instance);
        }

        return Access(instance, memberName);
    }

    private static object? Access(object instance, string memberName)
    {
        var type = instance.GetType();
        var field = type.GetField(memberName);
        if (field is not null)
        {
            return field.GetValue(instance);
        }

#pragma warning disable S3011
        var nonPublicField = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
#pragma warning restore S3011
        return nonPublicField?.GetValue(instance);
    }

    private static bool TryGetTextMeshPro(object? textSkin, out TextMeshProUGUI? tmp)
    {
        tmp = null;
        if (textSkin is null)
        {
            return false;
        }

        if (textSkin is Component component)
        {
            tmp = component.GetComponent<TextMeshProUGUI>();
            if (tmp is not null)
            {
                return true;
            }
        }

        tmp = Access(textSkin, "_tmp") as TextMeshProUGUI
            ?? Access(textSkin, "tmp") as TextMeshProUGUI;
        return tmp is not null;
    }

    private static void LogDiagnostics(object? textSkin, TextMeshProUGUI? tmp, string? finalText, bool applied)
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled)
        {
            return;
        }

        var count = Interlocked.Increment(ref diagnosticsCount);
        if (count > MaxDiagnostics)
        {
            return;
        }

        try
        {
            var textInfo = tmp?.textInfo;
            var rect = tmp?.rectTransform?.rect;
            RuntimeDiagnostics.LogVerboseProbe(() =>
                "[QudJP] InventoryLineFontFixer/v1: "
                + $"applied={applied} "
                + $"textSkin='{textSkin?.GetType().FullName ?? "<null>"}' "
                + $"tmp='{tmp?.GetType().FullName ?? "<null>"}' "
                + $"font='{tmp?.font?.name ?? "<null>"}' "
                + $"source='{finalText ?? string.Empty}' "
                + $"tmpText='{tmp?.text ?? string.Empty}' "
                + $"charCount={textInfo?.characterCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"} "
                + $"pageCount={textInfo?.pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"} "
                + $"tmpAlpha={tmp?.alpha.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"} "
                + $"fontSize={tmp?.fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"} "
                + $"maxChars={tmp?.maxVisibleCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"} "
                + $"maxLines={tmp?.maxVisibleLines.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"} "
                + $"pageToDisplay={tmp?.pageToDisplay.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"} "
                + $"richText={tmp?.richText.ToString() ?? "<null>"} "
                + $"enabled={tmp?.enabled.ToString() ?? "<null>"} "
                + $"activeAndEnabled={tmp?.isActiveAndEnabled.ToString() ?? "<null>"} "
                + $"activeSelf={tmp?.gameObject?.activeSelf.ToString() ?? "<null>"} "
                + $"activeInHierarchy={tmp?.gameObject?.activeInHierarchy.ToString() ?? "<null>"} "
                + $"cull={ReadCanvasCull(tmp)} "
                + $"rect={rect?.width.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"}x{rect?.height.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"}");
        }
        catch (Exception ex)
        {
            try
            {
                System.Diagnostics.Trace.TraceWarning(
                    "QudJP: InventoryLineFontFixer diagnostics failed: {0}: {1}",
                    ex.GetType().Name,
                    ex.Message);
            }
            catch
            {
                // Diagnostics must never interrupt inventory row translation.
            }
        }
    }

    private static string ReadCanvasCull(TextMeshProUGUI? tmp)
    {
        if (tmp is null)
        {
            return "<null>";
        }

        var canvasRendererProperty = tmp.GetType().GetProperty("canvasRenderer");
        var canvasRenderer = canvasRendererProperty?.GetValue(tmp);
        if (canvasRenderer is null)
        {
            return "<null>";
        }

        var cullProperty = canvasRenderer.GetType().GetProperty("cull");
        var cull = cullProperty?.GetValue(canvasRenderer);
        return cull?.ToString() ?? "<null>";
    }

    private static void InvokeIfPresent(object target, string methodName)
    {
        try
        {
            _ = target.GetType().GetMethod(methodName, Type.EmptyTypes)?.Invoke(target, Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[QudJP] InventoryLineFontFixer: {methodName} failed: {ex.GetType().Name}: {ex.Message}");
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
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            method?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[QudJP] InventoryLineFontFixer: ForceUpdateCanvases failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
#endif
}
