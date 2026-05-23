#if HAS_TMP
using System.Collections.Concurrent;
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
    private const int MaxSuccessfulRefreshCacheEntries = 1024;
    private const int CleanupInterval = 128;
    private const long SuccessfulRefreshCacheTtlTicks = 5L * 60L * 10_000_000L;
    private static int diagnosticsCount;
    private static int cleanupCount;
    private static readonly ConcurrentDictionary<int, SuccessfulRefreshEntry> SuccessfulRefreshKeysByLine = new();

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
        var textSkin = ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "text");
        return TryRefreshTextSkinWithFallbackFont(textSkin, displayName);
    }

    internal static bool TryRefreshTextSkinWithFallbackFont(object? textSkin, string? finalText)
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
        FontManager.ApplyToText(tmp);
        if (tmp.font is not null)
        {
            tmp.fontSharedMaterial = tmp.font.material;
        }

        tmp.overflowMode = TextOverflowModes.Overflow;

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

        if (ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "categoryMode") is GameObject categoryMode
            && categoryMode.activeSelf)
        {
            return false;
        }

        if (ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "itemMode") is GameObject itemMode
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

        return TryRefreshTextSkinWithFallbackFont(
            ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "text"),
            GetActiveItemLineText(inventoryLineInstance));
    }

    internal static string? GetActiveItemLineRefreshKey(object? inventoryLineInstance)
    {
        var textSkin = ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "text");
        var currentText = GetActiveItemLineText(inventoryLineInstance);

        _ = TryGetTextMeshPro(textSkin, out var tmp);
        if (tmp is not null)
        {
            if (string.IsNullOrEmpty(currentText))
            {
                currentText = tmp.text;
            }

            if (string.IsNullOrEmpty(currentText))
            {
                return null;
            }

            return currentText
                + "\u001f"
                + (tmp.font is null ? string.Empty : tmp.font.name)
                + "\u001f"
                + (tmp.fontSharedMaterial is null ? string.Empty : tmp.fontSharedMaterial.name)
                + "\u001f"
                + tmp.overflowMode;
        }

        return string.IsNullOrEmpty(currentText) ? null : currentText;
    }

    internal static bool HasSuccessfulRefreshForCurrentKey(object? inventoryLineInstance, string? refreshKey)
    {
        if (string.IsNullOrEmpty(refreshKey) || inventoryLineInstance is not Component component)
        {
            return false;
        }

        CleanupSuccessfulRefreshCacheIfNeeded();
        var lineId = component.GetInstanceID();
        if (!SuccessfulRefreshKeysByLine.TryGetValue(lineId, out var previousEntry))
        {
            return false;
        }

        if (IsExpired(previousEntry))
        {
            SuccessfulRefreshKeysByLine.TryRemove(lineId, out _);
            return false;
        }

        return string.Equals(previousEntry.RefreshKey, refreshKey, StringComparison.Ordinal);
    }

    internal static bool HasHealthySuccessfulRefreshForCurrentKey(object? inventoryLineInstance, string? refreshKey)
    {
        if (!HasSuccessfulRefreshForCurrentKey(inventoryLineInstance, refreshKey))
        {
            return false;
        }

        var textSkin = ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "text");
        if (!TryGetTextMeshPro(textSkin, out var tmp) || tmp is null)
        {
            ForgetSuccessfulRefreshForLine(inventoryLineInstance);
            return false;
        }

        var healthy = HasLiveRenderableText(tmp);
        if (!healthy)
        {
            ForgetSuccessfulRefreshForLine(inventoryLineInstance);
        }

        return healthy;
    }

    private static bool HasLiveRenderableText(TextMeshProUGUI tmp)
    {
        if (tmp.gameObject is null
            || !tmp.gameObject.activeInHierarchy
            || !tmp.isActiveAndEnabled
            || tmp.textInfo.characterCount <= 0
            || tmp.maxVisibleCharacters <= 0
            || tmp.maxVisibleLines <= 0
            || tmp.pageToDisplay <= 0
            || tmp.font is null
            || tmp.fontSharedMaterial is null
            || tmp.alpha <= 0f)
        {
            return false;
        }

        var textColorAlpha = UnityRuntimeCompatibility.TryGetColorAlpha(tmp.color);
        if (textColorAlpha.HasValue && textColorAlpha.Value <= 0f)
        {
            return false;
        }

        var faceColorAlpha = UnityRuntimeCompatibility.TryGetFaceColorAlpha(tmp.fontSharedMaterial);
        if (faceColorAlpha.HasValue && faceColorAlpha.Value <= 0f)
        {
            return false;
        }

        var canvasRenderer = tmp.canvasRenderer;
        if (canvasRenderer is null || canvasRenderer.cull || canvasRenderer.GetAlpha() <= 0f)
        {
            return false;
        }

        var rect = tmp.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return false;
        }

        var combinedParentCanvasGroupAlpha = TryGetCombinedParentCanvasGroupAlpha(tmp.transform);
        return !combinedParentCanvasGroupAlpha.HasValue || combinedParentCanvasGroupAlpha.Value > 0f;
    }

    private static float? TryGetCombinedParentCanvasGroupAlpha(Transform transform)
    {
        float? combinedAlpha = null;
        for (var current = transform; current is not null; current = current.parent)
        {
            var component = current.GetComponent("CanvasGroup");
            if (component is null)
            {
                continue;
            }

#pragma warning disable S3011
            var alphaProperty = component.GetType().GetProperty(
                "alpha",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
#pragma warning restore S3011
            if (alphaProperty?.PropertyType == typeof(float)
                && alphaProperty.GetIndexParameters().Length == 0
                && alphaProperty.GetValue(component, null) is float alpha)
            {
                combinedAlpha = (combinedAlpha ?? 1f) * alpha;
            }
        }

        return combinedAlpha;
    }

    internal static void RecordSuccessfulRefreshForCurrentKey(object? inventoryLineInstance, string? refreshKey)
    {
        if (refreshKey is null || refreshKey.Length == 0)
        {
            ForgetSuccessfulRefreshForLine(inventoryLineInstance);
            return;
        }

        if (inventoryLineInstance is not Component component)
        {
            return;
        }

        SuccessfulRefreshKeysByLine[component.GetInstanceID()] = new SuccessfulRefreshEntry(refreshKey, DateTime.UtcNow.Ticks);
        CleanupSuccessfulRefreshCacheIfNeeded();
    }

    internal static void ForgetSuccessfulRefreshForLine(object? inventoryLineInstance)
    {
        if (inventoryLineInstance is Component component)
        {
            SuccessfulRefreshKeysByLine.TryRemove(component.GetInstanceID(), out _);
        }
    }

    private static void CleanupSuccessfulRefreshCacheIfNeeded()
    {
        var count = Interlocked.Increment(ref cleanupCount);
        if (count % CleanupInterval != 0 && SuccessfulRefreshKeysByLine.Count <= MaxSuccessfulRefreshCacheEntries)
        {
            return;
        }

        var nowTicks = DateTime.UtcNow.Ticks;
        foreach (var entry in SuccessfulRefreshKeysByLine)
        {
            if (nowTicks - entry.Value.LastSeenUtcTicks > SuccessfulRefreshCacheTtlTicks
                || SuccessfulRefreshKeysByLine.Count > MaxSuccessfulRefreshCacheEntries)
            {
                SuccessfulRefreshKeysByLine.TryRemove(entry.Key, out _);
            }
        }
    }

    private static bool IsExpired(SuccessfulRefreshEntry entry)
    {
        return DateTime.UtcNow.Ticks - entry.LastSeenUtcTicks > SuccessfulRefreshCacheTtlTicks;
    }

    private readonly struct SuccessfulRefreshEntry
    {
        internal SuccessfulRefreshEntry(string refreshKey, long lastSeenUtcTicks)
        {
            RefreshKey = refreshKey;
            LastSeenUtcTicks = lastSeenUtcTicks;
        }

        internal string RefreshKey { get; }

        internal long LastSeenUtcTicks { get; }
    }

    internal static string? GetActiveItemLineText(object? inventoryLineInstance)
    {
        var textSkin = ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "text");
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
