#if HAS_TMP
using System;
using System.Collections.Concurrent;
using System.Text;
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class UiChildTextObservability
{
#if HAS_TMP
    private const int MaxLogsPerBucket = 6;

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Builds a compact observability snapshot string for a Unity Component by inspecting its TextMeshProUGUI children and selected rendering/material properties.
    /// </summary>
    /// <param name="lineInstance">The object expected to be a Unity <see cref="Component"/> whose children will be inspected.</param>
    /// <param name="probeName">A label inserted into the snapshot header to identify the probe source.</param>
    /// <param name="logLine">When the method returns <c>true</c>, contains the constructed snapshot string; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if at least one TextMeshProUGUI child was captured and the per-bucket rate limit was not exceeded; <c>false</c> otherwise.</returns>
    internal static bool TryBuildSnapshot(object? lineInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (lineInstance is not Component component)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        if (texts.Length == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": root='");
        builder.Append(component.gameObject.name);
        builder.Append('\'');

        var capturedCount = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            var current = text.text ?? string.Empty;
            var subMeshes = text.GetComponentsInChildren<TMP_SubMeshUI>(includeInactive: true);
            capturedCount++;
            builder.Append("; child[");
            builder.Append(index);
            builder.Append("]='");
            builder.Append(text.gameObject.name);
            builder.Append("' text='");
            builder.Append(Truncate(current));
            builder.Append("' rect=");
            builder.Append(text.rectTransform.rect.width.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(text.rectTransform.rect.height.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" pref=");
            builder.Append(text.preferredWidth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(text.preferredHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" overflow=");
            builder.Append(text.overflowMode);
            builder.Append(" wrap=");
            builder.Append(text.textWrappingMode);
            builder.Append(" colorA=");
            builder.Append(UnityRuntimeCompatibility.TryGetColorAlpha(text.color)?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" alpha=");
            builder.Append(text.alpha.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" canvasA=");
            builder.Append(TryGetCanvasAlpha(text)?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" font='");
            builder.Append(text.font is null ? string.Empty : text.font.name);
            builder.Append("' material='");
            builder.Append(text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name);
            builder.Append('\'');
            builder.Append(" chars=");
            builder.Append(text.textInfo.characterCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" materials=");
            builder.Append(text.textInfo.materialCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" meshInfos=");
            builder.Append(text.textInfo.meshInfo.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" maxChars=");
            builder.Append(text.maxVisibleCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" maxLines=");
            builder.Append(text.maxVisibleLines.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" page=");
            builder.Append(text.pageToDisplay.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" pageCount=");
            builder.Append(text.textInfo.pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" active=");
            builder.Append(text.gameObject.activeInHierarchy);
            builder.Append(" enabled=");
            builder.Append(text.enabled);
            builder.Append(" propsChanged=");
            builder.Append(text.havePropertiesChanged);
            builder.Append(" stencil=");
            builder.Append(TryGetMaterialInt(text.fontSharedMaterial, "_Stencil")?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" faceA=");
            builder.Append(TryGetFaceColorAlpha(text.fontSharedMaterial)?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" subMeshes=");
            builder.Append(subMeshes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (subMeshes.Length > 0)
            {
                var subMesh = subMeshes[0];
                builder.Append(" sub0={active=");
                builder.Append(subMesh.gameObject.activeInHierarchy);
                builder.Append(", enabled=");
                builder.Append(subMesh.enabled);
                builder.Append(", maskable=");
                builder.Append(subMesh.maskable);
                builder.Append(", font='");
                builder.Append(subMesh.fontAsset is null ? string.Empty : subMesh.fontAsset.name);
                builder.Append("', material='");
                builder.Append(subMesh.sharedMaterial is null ? string.Empty : subMesh.sharedMaterial.name);
                builder.Append("', stencil=");
                builder.Append(TryGetMaterialInt(subMesh.sharedMaterial, "_Stencil")?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>");
                builder.Append(", faceA=");
                builder.Append(TryGetFaceColorAlpha(subMesh.sharedMaterial)?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>");
                builder.Append(", textComponent=");
                builder.Append(subMesh.textComponent is not null);
                builder.Append('}');
            }
        }

        if (capturedCount == 0)
        {
            return false;
        }

        var bucket = component.gameObject.name + ":" + capturedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var hitCount = BucketCounts.AddOrUpdate(
            bucket,
            1,
            static (_, currentValue) => currentValue < int.MaxValue ? currentValue + 1 : int.MaxValue);
        if (hitCount > MaxLogsPerBucket)
        {
            return false;
        }

        logLine = builder.ToString();
        return true;
    }

    private static string Truncate(string value)
    {
        var normalized = value.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length == 0)
        {
            return "<empty>";
        }

        if (normalized.Length <= 60)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 60) + "...";
#pragma warning restore CA1845
    }

    private static float? TryGetCanvasAlpha(TextMeshProUGUI text)
    {
        var property = text.GetType().GetProperty("canvasRenderer");
        var canvasRenderer = property?.GetValue(text, null);
        if (canvasRenderer is null)
        {
            return null;
        }

        var method = canvasRenderer.GetType().GetMethod("GetAlpha", Type.EmptyTypes);
        if (method?.ReturnType != typeof(float))
        {
            return null;
        }

        var value = method.Invoke(canvasRenderer, null);
        return value is float floatValue ? floatValue : null;
    }

    private static int? TryGetMaterialInt(Material? material, string propertyName)
    {
        if (material is null || !material.HasProperty(propertyName))
        {
            return null;
        }

        return material.GetInt(propertyName);
    }

    /// <summary>
    /// Extracts the alpha component of the material's `_FaceColor` property when available.
    /// </summary>
    /// <returns>The `_FaceColor` alpha value as a float, or `null` if the material is null or does not have a `_FaceColor` property.</returns>
    private static float? TryGetFaceColorAlpha(Material? material)
    {
        if (material is null || !material.HasProperty("_FaceColor"))
        {
            return null;
        }

        return UnityRuntimeCompatibility.TryGetFaceColorAlpha(material);
    }
#else
    /// <summary>
    /// No-op implementation used when TextMeshPro support is unavailable; it does not build a snapshot.
    /// </summary>
    /// <param name="lineInstance">Ignored in this build configuration.</param>
    /// <param name="probeName">Ignored in this build configuration.</param>
    /// <param name="logLine">Always set to <c>null</c> when this method returns.</param>
    /// <returns><c>false</c> indicating that no snapshot string was produced.</returns>
    internal static bool TryBuildSnapshot(object? lineInstance, string probeName, out string? logLine)
    {
        _ = lineInstance;
        _ = probeName;
        logLine = null;
        return false;
    }
#endif
}
