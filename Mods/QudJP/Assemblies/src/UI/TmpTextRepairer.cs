#if HAS_TMP
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class TmpTextRepairer
{
#if HAS_TMP
    private const int MaxLeafProbeLogsPerBucket = 3;
    private const string ReplacementObjectName = "QudJPReplacementText";

    private static readonly ConcurrentDictionary<string, int> LeafProbeCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static int TryRepairInvisibleTexts(object? componentInstance)
    {
        if (componentInstance is not Component component)
        {
            return 0;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var repaired = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            if (TryRepairInvisibleText(texts[index]))
            {
                repaired++;
            }
        }

        return repaired;
    }

    internal static bool CanAttemptRepairForTests(
        bool enabled,
        bool activeInHierarchy,
        string? text,
        string objectName)
    {
        return enabled
            && activeInHierarchy
            && !string.IsNullOrEmpty(text)
            && !string.Equals(objectName, ReplacementObjectName, StringComparison.Ordinal);
    }

    internal static bool TryBuildTextShellLeafProbe(object? componentInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (componentInstance is not Component component)
        {
            return false;
        }

        var bucket = probeName + ":" + component.gameObject.name;
        var hitCount = LeafProbeCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLeafProbeLogsPerBucket)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": root='");
        builder.Append(component.gameObject.name);
        builder.Append("' type='");
        builder.Append(GetTypeName(component));
        builder.Append('\'');

        var matches = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            var relativePath = BuildRelativePath(component.transform, text.transform);
            if (!IsTextShellLeaf(relativePath))
            {
                continue;
            }

            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            builder.Append("; leaf[");
            builder.Append(matches.ToString(CultureInfo.InvariantCulture));
            builder.Append("] path='");
            builder.Append(relativePath);
            builder.Append("' text='");
            builder.Append(Truncate(text.text));
            builder.Append("' chars=");
            builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pageCount=");
            builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" enabled=");
            builder.Append(text.enabled ? "True" : "False");
            builder.Append(" active=");
            builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(" eligible=");
            builder.Append(CanAttemptRepair(text) ? "True" : "False");
            builder.Append(" skip='");
            builder.Append(GetSkipReason(text));
            builder.Append('\'');
            matches++;
        }

        if (matches == 0)
        {
            builder.Append("; matches=0");
        }

        logLine = builder.ToString();
        return true;
    }

    internal static bool TryBuildTextShellLeafStateProbe(object? componentInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (componentInstance is not Component component)
        {
            return false;
        }

        var bucket = probeName + ":" + component.gameObject.name;
        var hitCount = LeafProbeCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLeafProbeLogsPerBucket)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": root='");
        builder.Append(component.gameObject.name);
        builder.Append("' type='");
        builder.Append(GetTypeName(component));
        builder.Append('\'');

        var matches = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            var relativePath = BuildRelativePath(component.transform, text.transform);
            if (!IsTextShellLeaf(relativePath))
            {
                continue;
            }

            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (text.textInfo.characterCount > 0)
            {
                continue;
            }

            var subMeshes = text.GetComponentsInChildren<TMP_SubMeshUI>(includeInactive: true);
            builder.Append("; leaf[");
            builder.Append(matches.ToString(CultureInfo.InvariantCulture));
            builder.Append("] path='");
            builder.Append(relativePath);
            builder.Append("' rect=");
            builder.Append(text.rectTransform.rect.width.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(text.rectTransform.rect.height.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" pref=");
            builder.Append(text.preferredWidth.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(text.preferredHeight.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" overflow=");
            builder.Append(text.overflowMode);
            builder.Append(" wrap=");
            builder.Append(text.textWrappingMode);
            builder.Append(" chars=");
            builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pageCount=");
            builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" enabled=");
            builder.Append(text.enabled ? "True" : "False");
            builder.Append(" active=");
            builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(" maskable=");
            builder.Append(text.maskable ? "True" : "False");
            builder.Append(" canvasA=");
            builder.Append(TryGetCanvasAlpha(text)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" font='");
            builder.Append(text.font is null ? string.Empty : text.font.name);
            builder.Append("' material='");
            builder.Append(text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name);
            builder.Append("' stencil=");
            builder.Append(TryGetMaterialInt(text.fontSharedMaterial, "_Stencil")?.ToString(CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" faceA=");
            builder.Append(TryGetFaceColorAlpha(text.fontSharedMaterial)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" subMeshes=");
            builder.Append(subMeshes.Length.ToString(CultureInfo.InvariantCulture));
            if (subMeshes.Length > 0)
            {
                var subMesh = subMeshes[0];
                builder.Append(" sub0={active=");
                builder.Append(subMesh.gameObject.activeInHierarchy ? "True" : "False");
                builder.Append(", enabled=");
                builder.Append(subMesh.enabled ? "True" : "False");
                builder.Append(", maskable=");
                builder.Append(subMesh.maskable ? "True" : "False");
                builder.Append(", font='");
                builder.Append(subMesh.fontAsset is null ? string.Empty : subMesh.fontAsset.name);
                builder.Append("', material='");
                builder.Append(subMesh.sharedMaterial is null ? string.Empty : subMesh.sharedMaterial.name);
                builder.Append("', stencil=");
                builder.Append(TryGetMaterialInt(subMesh.sharedMaterial, "_Stencil")?.ToString(CultureInfo.InvariantCulture) ?? "<unknown>");
                builder.Append(", faceA=");
                builder.Append(TryGetFaceColorAlpha(subMesh.sharedMaterial)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
                builder.Append('}');
            }

            matches++;
        }

        if (matches == 0)
        {
            builder.Append("; matches=0");
        }

        logLine = builder.ToString();
        return true;
    }

    internal static bool HasFailingEligibleTextShellLeaf(object? componentInstance)
    {
        if (componentInstance is not Component component)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            var relativePath = BuildRelativePath(component.transform, text.transform);
            if (!IsTextShellLeaf(relativePath) || !CanAttemptRepair(text))
            {
                continue;
            }

            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (text.textInfo.characterCount == 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryBuildTextShellSentinelProbe(object? componentInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (componentInstance is not Component component)
        {
            return false;
        }

        var bucket = probeName + ":" + component.gameObject.name;
        var hitCount = LeafProbeCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLeafProbeLogsPerBucket)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            var relativePath = BuildRelativePath(component.transform, text.transform);
            if (!IsTextShellLeaf(relativePath) || !CanAttemptRepair(text))
            {
                continue;
            }

            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (text.textInfo.characterCount > 0)
            {
                continue;
            }

            var originalText = text.text ?? string.Empty;
            text.havePropertiesChanged = true;
            text.SetAllDirty();
            text.text = "TEST";
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            var sentinelChars = text.textInfo.characterCount;
            var sentinelPages = text.textInfo.pageCount;

            text.havePropertiesChanged = true;
            text.SetAllDirty();
            text.text = originalText;
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

            var builder = new StringBuilder();
            builder.Append("[QudJP] ");
            builder.Append(probeName);
            builder.Append(": root='");
            builder.Append(component.gameObject.name);
            builder.Append("' path='");
            builder.Append(relativePath);
            builder.Append("' original='");
            builder.Append(Truncate(originalText));
            builder.Append("' sentinel='TEST' sentinelChars=");
            builder.Append(sentinelChars.ToString(CultureInfo.InvariantCulture));
            builder.Append(" sentinelPageCount=");
            builder.Append(sentinelPages.ToString(CultureInfo.InvariantCulture));
            builder.Append(" restoredChars=");
            builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" restoredPageCount=");
            builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" font='");
            builder.Append(text.font is null ? string.Empty : text.font.name);
            builder.Append("' material='");
            builder.Append(text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name);
            builder.Append('\'');
            logLine = builder.ToString();
            return true;
        }

        logLine = "[QudJP] " + probeName + ": matches=0";
        return true;
    }

    internal static bool TryBuildTextShellDirectFontSentinelProbe(object? componentInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (componentInstance is not Component component)
        {
            return false;
        }

        var bucket = probeName + ":" + component.gameObject.name;
        var hitCount = LeafProbeCounts.AddOrUpdate(bucket, 1, static (_, current) => current + 1);
        if (hitCount > MaxLeafProbeLogsPerBucket)
        {
            return false;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            var relativePath = BuildRelativePath(component.transform, text.transform);
            if (!IsTextShellLeaf(relativePath) || !CanAttemptRepair(text))
            {
                continue;
            }

            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (text.textInfo.characterCount > 0)
            {
                continue;
            }

            var originalText = text.text ?? string.Empty;
            var originalFontName = text.font is null ? string.Empty : text.font.name;
            var originalMaterialName = text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name;
            var originalInternalFontName = GetTmpFontFieldName(text, "m_fontAsset");
            var originalInternalSharedMaterialName = GetMaterialFieldName(text, "m_sharedMaterial");

            FontManager.ForcePrimaryFont(text);

            var directFontName = text.font is null ? string.Empty : text.font.name;
            var directMaterialName = text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name;
            var directInternalFontName = GetTmpFontFieldName(text, "m_fontAsset");
            var directInternalSharedMaterialName = GetMaterialFieldName(text, "m_sharedMaterial");
            var primaryFont = FontManager.GetPrimaryFontAssetForDiagnostics();
            var fontMatchesPrimary = primaryFont is not null && ReferenceEquals(text.font, primaryFont);
            var internalFontMatchesPrimary = primaryFont is not null && ReferenceEquals(GetFieldValue(text, "m_fontAsset"), primaryFont);

            text.havePropertiesChanged = true;
            text.SetAllDirty();
            text.text = "TEST";
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            var sentinelChars = text.textInfo.characterCount;
            var sentinelPages = text.textInfo.pageCount;

            text.havePropertiesChanged = true;
            text.SetAllDirty();
            text.text = originalText;
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

            var builder = new StringBuilder();
            builder.Append("[QudJP] ");
            builder.Append(probeName);
            builder.Append(": root='");
            builder.Append(component.gameObject.name);
            builder.Append("' path='");
            builder.Append(relativePath);
            builder.Append("' original='");
            builder.Append(Truncate(originalText));
            builder.Append("' preFont='");
            builder.Append(originalFontName);
            builder.Append("' preInternalFont='");
            builder.Append(originalInternalFontName);
            builder.Append("' preMaterial='");
            builder.Append(originalMaterialName);
            builder.Append("' preInternalSharedMaterial='");
            builder.Append(originalInternalSharedMaterialName);
            builder.Append("' postFont='");
            builder.Append(directFontName);
            builder.Append("' postInternalFont='");
            builder.Append(directInternalFontName);
            builder.Append("' postMaterial='");
            builder.Append(directMaterialName);
            builder.Append("' postInternalSharedMaterial='");
            builder.Append(directInternalSharedMaterialName);
            builder.Append("' fontMatchesPrimary=");
            builder.Append(fontMatchesPrimary ? "True" : "False");
            builder.Append(" internalFontMatchesPrimary=");
            builder.Append(internalFontMatchesPrimary ? "True" : "False");
            builder.Append("' sentinel='TEST' sentinelChars=");
            builder.Append(sentinelChars.ToString(CultureInfo.InvariantCulture));
            builder.Append(" sentinelPageCount=");
            builder.Append(sentinelPages.ToString(CultureInfo.InvariantCulture));
            builder.Append(" restoredChars=");
            builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" restoredPageCount=");
            builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            logLine = builder.ToString();
            return true;
        }

        logLine = "[QudJP] " + probeName + ": matches=0";
        return true;
    }

    internal static string BuildRepairLog(string probeName, int repairedCount)
    {
        return "[QudJP] " + probeName + ": repaired=" + repairedCount.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryRepairInvisibleText(TextMeshProUGUI text)
    {
        if (!CanAttemptRepair(text))
        {
            return false;
        }

        var currentText = text.text;
        if (string.IsNullOrEmpty(currentText))
        {
            return false;
        }

        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        if (text.textInfo.characterCount > 0)
        {
            return false;
        }

        _ = FontManager.TryWarmPrimaryFontCharactersForUi(currentText);
        if (text.font is not null)
        {
            text.fontSharedMaterial = text.font.material;
        }

        text.havePropertiesChanged = true;
        text.SetAllDirty();
        text.text = currentText;
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        return text.textInfo.characterCount > 0;
    }

    private static bool CanAttemptRepair(TextMeshProUGUI text)
    {
        return CanAttemptRepairForTests(
            text.enabled,
            text.gameObject.activeInHierarchy,
            text.text,
            text.gameObject.name);
    }

    private static string GetSkipReason(TextMeshProUGUI text)
    {
        if (!text.enabled)
        {
            return "disabled";
        }

        if (!text.gameObject.activeInHierarchy)
        {
            return "inactive";
        }

        if (string.Equals(text.gameObject.name, ReplacementObjectName, StringComparison.Ordinal))
        {
            return "replacement";
        }

        return string.IsNullOrEmpty(text.text) ? "empty" : "none";
    }

    private static bool IsTextShellLeaf(string relativePath)
    {
#pragma warning disable CA2249
        return relativePath.IndexOf("TextShell/Text", StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
    }

    private static string BuildRelativePath(Transform root, Transform target)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        var current = target;
        while (current != root && current is not null)
        {
            stack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", stack.ToArray());
    }

    private static string Truncate(string value)
    {
        var normalized = value.Replace("\r", "\\r")
            .Replace("\n", "\\n");
        if (normalized.Length <= 48)
        {
            return normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, 48) + "...";
#pragma warning restore CA1845
    }

    private static string GetTypeName(Component component)
    {
        var type = component.GetType();
        return type.FullName is null ? type.Name : type.FullName;
    }

    private static object? GetFieldValue(object instance, string fieldName)
    {
#pragma warning disable S3011
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
#pragma warning restore S3011
        return field?.GetValue(instance);
    }

    private static string GetTmpFontFieldName(TextMeshProUGUI text, string fieldName)
    {
        var value = GetFieldValue(text, fieldName);
        if (value is not TMP_FontAsset fontAsset)
        {
            return string.Empty;
        }

        return fontAsset.name;
    }

    private static string GetMaterialFieldName(TextMeshProUGUI text, string fieldName)
    {
        var value = GetFieldValue(text, fieldName);
        if (value is not Material material)
        {
            return string.Empty;
        }

        return material.name;
    }

    private static float? TryGetCanvasAlpha(TextMeshProUGUI text)
    {
#pragma warning disable S3011
        var property = text.GetType().GetProperty("canvasRenderer");
#pragma warning restore S3011
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

    private static float? TryGetFaceColorAlpha(Material? material)
    {
        if (material is null || !material.HasProperty("_FaceColor"))
        {
            return null;
        }

        return material.GetColor("_FaceColor").a;
    }
#endif
}
