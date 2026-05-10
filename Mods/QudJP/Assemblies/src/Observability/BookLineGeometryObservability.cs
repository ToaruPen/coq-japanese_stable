#if HAS_TMP && QUDJP_DEV_BUILD
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UguiText = UnityEngine.UI.Text;
#endif

namespace QudJP;

internal static class BookLineGeometryObservability
{
#if HAS_TMP && QUDJP_DEV_BUILD
    private const int MaxLogsPerBucket = 8;
    private const int MaxHierarchyDepth = 12;

    private static readonly ConcurrentDictionary<string, int> BucketCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryBuildSnapshot(
        object? bookLineInstance,
        string source,
        string rendered,
        out string? logLine,
        string phase = "setData")
    {
        logLine = null;

        try
        {
            if (bookLineInstance is not Component component)
            {
                return false;
            }

            var bucket = component.gameObject.name + ":" + Truncate(rendered, 32);
            var hitCount = BucketCounts.AddOrUpdate(
                bucket,
                1,
                static (_, current) => current < int.MaxValue ? current + 1 : int.MaxValue);
            if (hitCount > MaxLogsPerBucket)
            {
                return false;
            }

            var builder = new StringBuilder();
            var rootTypeName = component.GetType().FullName;
            if (rootTypeName is null)
            {
                rootTypeName = component.GetType().Name;
            }

            builder.Append("[QudJP] BookLineGeometryProbe/v1: root='");
            builder.Append(Escape(component.gameObject.name));
            builder.Append("' rootType='");
            builder.Append(Escape(rootTypeName));
            builder.Append("' phase='");
            builder.Append(Escape(phase));
            builder.Append("' sourceLen=");
            builder.Append((source?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
            builder.Append(" renderedLen=");
            builder.Append((rendered?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
            builder.Append(" source='");
            builder.Append(Truncate(source ?? string.Empty, 160));
            builder.Append("' rendered='");
            builder.Append(Truncate(rendered ?? string.Empty, 160));
            builder.Append('\'');

            AppendTmpTexts(builder, component);
            AppendLegacyTexts(builder, component);
            AppendHierarchy(builder, component.transform);

            logLine = builder.ToString();
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BookLineGeometryObservability.TryBuildSnapshot failed: {0}", ex);
            logLine = null;
            return false;
        }
    }

    private static void AppendTmpTexts(StringBuilder builder, Component root)
    {
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        builder.Append("; tmpCount=");
        builder.Append(texts.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

            builder.Append("; tmpText[");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
            builder.Append("]={path='");
            builder.Append(Escape(BuildRelativePath(root.transform, text.transform)));
            builder.Append("' name='");
            builder.Append(Escape(text.gameObject.name));
            builder.Append("' rect=");
            AppendRect(builder, text.rectTransform);
            builder.Append(" preferred=");
            builder.Append(text.preferredWidth.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(text.preferredHeight.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" fontSize=");
            builder.Append(text.fontSize.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" autoSize=");
            builder.Append(text.enableAutoSizing ? "True" : "False");
            builder.Append(" overflow=");
            builder.Append(text.overflowMode);
            builder.Append(" wrap=");
            builder.Append(text.textWrappingMode);
            builder.Append(" alignment=");
            builder.Append(text.alignment);
            builder.Append(" margin=");
            AppendVector4(builder, text.margin);
            builder.Append(" maxVisibleCharacters=");
            builder.Append(text.maxVisibleCharacters.ToString(CultureInfo.InvariantCulture));
            builder.Append(" maxVisibleLines=");
            builder.Append(text.maxVisibleLines.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pageToDisplay=");
            builder.Append(text.pageToDisplay.ToString(CultureInfo.InvariantCulture));
            builder.Append(" chars=");
            builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" visibleChars=");
            builder.Append(CountVisibleCharacters(text).ToString(CultureInfo.InvariantCulture));
            builder.Append(" lineCount=");
            builder.Append(TryGetIntPropertyOrField(text.textInfo, "lineCount")?.ToString(CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" pageCount=");
            builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" materialCount=");
            builder.Append(text.textInfo.materialCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" meshInfos=");
            builder.Append(text.textInfo.meshInfo.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" activeSelf=");
            builder.Append(text.gameObject.activeSelf ? "True" : "False");
            builder.Append(" activeInHierarchy=");
            builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(" enabled=");
            builder.Append(text.enabled ? "True" : "False");
            builder.Append(" isActiveAndEnabled=");
            builder.Append(text.isActiveAndEnabled ? "True" : "False");
            builder.Append(" maskable=");
            builder.Append(text.maskable ? "True" : "False");
            builder.Append(" raycast=");
            builder.Append(text.raycastTarget ? "True" : "False");
            builder.Append(" richText=");
            builder.Append(text.richText ? "True" : "False");
            builder.Append(" alpha=");
            builder.Append(text.alpha.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" canvasA=");
            builder.Append(TryGetCanvasAlpha(text)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" font='");
            builder.Append(Escape(text.font is null ? string.Empty : text.font.name));
            builder.Append("' material='");
            builder.Append(Escape(text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name));
            builder.Append("' textTruncated=");
            builder.Append(GetBoolFieldValue(text, "m_isTextTruncated")?.ToString() ?? "<unknown>");
            builder.Append(" inputParsingRequired=");
            builder.Append(GetBoolFieldValue(text, "m_isInputParsingRequired")?.ToString() ?? "<unknown>");
            builder.Append(" layoutDirty=");
            builder.Append(GetBoolFieldValue(text, "m_isLayoutDirty")?.ToString() ?? "<unknown>");
            builder.Append(" vertsDirty=");
            builder.Append(GetBoolFieldValue(text, "m_VertsDirty")?.ToString() ?? "<unknown>");
            builder.Append(" materialDirty=");
            builder.Append(GetBoolFieldValue(text, "m_MaterialDirty")?.ToString() ?? "<unknown>");
            builder.Append(" maskChain='");
            builder.Append(Escape(BuildMaskChain(text.transform)));
            builder.Append("' text='");
            builder.Append(Truncate(text.text, 160));
            builder.Append("'}");
        }
    }

    private static void AppendLegacyTexts(StringBuilder builder, Component root)
    {
        var texts = root.GetComponentsInChildren<UguiText>(includeInactive: true);
        builder.Append("; legacyCount=");
        builder.Append(texts.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            builder.Append("; legacyText[");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
            builder.Append("]={path='");
            builder.Append(Escape(BuildRelativePath(root.transform, text.transform)));
            builder.Append("' name='");
            builder.Append(Escape(text.gameObject.name));
            builder.Append("' rect=");
            AppendRect(builder, text.rectTransform);
            builder.Append(" activeSelf=");
            builder.Append(text.gameObject.activeSelf ? "True" : "False");
            builder.Append(" activeInHierarchy=");
            builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(" enabled=");
            builder.Append(text.enabled ? "True" : "False");
            builder.Append(" font='");
            builder.Append(Escape(text.font is null ? string.Empty : text.font.name));
            builder.Append("' maskChain='");
            builder.Append(Escape(BuildMaskChain(text.transform)));
            builder.Append("' text='");
            builder.Append(Truncate(text.text, 160));
            builder.Append("'}");
        }
    }

    private static void AppendHierarchy(StringBuilder builder, Transform root)
    {
        builder.Append("; hierarchy='");
        var current = root;
        for (var depth = 0; current is not null && depth < MaxHierarchyDepth; depth++)
        {
            if (depth > 0)
            {
                builder.Append(" <- ");
            }

            builder.Append(Escape(current.gameObject.name));
            builder.Append('[');
            if (current is RectTransform rectTransform)
            {
                builder.Append("rect=");
                AppendRect(builder, rectTransform);
                builder.Append(',');
            }

            builder.Append("activeSelf=");
            builder.Append(current.gameObject.activeSelf ? "True" : "False");
            builder.Append(", activeInHierarchy=");
            builder.Append(current.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(", components=");
            AppendComponentNames(builder, current);
            builder.Append(']');
            current = current.parent;
        }

        builder.Append('\'');
    }

    private static void AppendComponentNames(StringBuilder builder, Component component)
    {
        var components = component.GetComponents<Component>();
        if (components.Length == 0)
        {
            builder.Append("<none>");
            return;
        }

        for (var index = 0; index < components.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(Escape(components[index]?.GetType().Name ?? "<null>"));
        }
    }

    private static string BuildMaskChain(Transform transform)
    {
        var builder = new StringBuilder();
        var current = transform;
        var depth = 0;
        while (current is not null && depth < MaxHierarchyDepth)
        {
            var mask = GetComponentByName(current, "UnityEngine.UI.Mask", "Mask");
            var rectMask = GetComponentByName(current, "UnityEngine.UI.RectMask2D", "RectMask2D");
            var canvas = GetComponentByName(current, "UnityEngine.Canvas", "Canvas");
            var canvasGroup = GetComponentByName(current, "UnityEngine.CanvasGroup", "CanvasGroup");
            var scrollRect = GetComponentByName(current, "UnityEngine.UI.ScrollRect", "ScrollRect");
            if (mask is not null || rectMask is not null || canvas is not null || canvasGroup is not null || scrollRect is not null)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" <- ");
                }

                builder.Append(current.gameObject.name);
                builder.Append('[');
                AppendNamedComponent(builder, "Mask", mask);
                AppendNamedComponent(builder, "RectMask2D", rectMask);
                AppendNamedComponent(builder, "Canvas", canvas);
                AppendNamedComponent(builder, "CanvasGroup", canvasGroup);
                AppendNamedComponent(builder, "ScrollRect", scrollRect);
                builder.Append(']');
            }

            current = current.parent;
            depth++;
        }

        return builder.Length == 0 ? "<none>" : builder.ToString();
    }

    private static Component? GetComponentByName(Component component, string fullName, string shortName)
    {
        var result = component.GetComponent(fullName);
        if (result is not null)
        {
            return result;
        }

        return component.GetComponent(shortName);
    }

    private static void AppendNamedComponent(StringBuilder builder, string name, Component? component)
    {
        if (component is null)
        {
            return;
        }

        if (builder.Length > 0 && builder[builder.Length - 1] != '[')
        {
            builder.Append(',');
        }

        builder.Append(name);
        if (component is Behaviour behaviour)
        {
            builder.Append("(enabled=");
            builder.Append(behaviour.enabled ? "True" : "False");
            builder.Append(')');
        }
    }

    private static int CountVisibleCharacters(TMP_Text text)
    {
        var count = 0;
        var characterInfo = text.textInfo.characterInfo;
        for (var index = 0; index < characterInfo.Length; index++)
        {
            if (characterInfo[index].isVisible)
            {
                count++;
            }
        }

        return count;
    }

    private static void AppendRect(StringBuilder builder, RectTransform rectTransform)
    {
        var rect = rectTransform.rect;
        builder.Append(rect.width.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append('x');
        builder.Append(rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append('@');
        builder.Append(rectTransform.anchoredPosition.x.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(rectTransform.anchoredPosition.y.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(" sizeDelta=");
        builder.Append(rectTransform.sizeDelta.x.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(rectTransform.sizeDelta.y.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static void AppendVector4(StringBuilder builder, Vector4 value)
    {
        builder.Append(value.x.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.y.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.z.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.w.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static int? TryGetIntPropertyOrField(object instance, string memberName)
    {
        var value = GetPropertyOrFieldValue(instance, memberName);
        return value is int intValue ? intValue : null;
    }

    private static bool? GetBoolFieldValue(object instance, string fieldName)
    {
        var value = GetPropertyOrFieldValue(instance, fieldName);
        return value is bool boolValue ? boolValue : null;
    }

    private static object? GetPropertyOrFieldValue(object instance, string memberName)
    {
#pragma warning disable S3011
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = instance.GetType();
        var property = type.GetProperty(memberName, flags);
        if (property is not null && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(instance, null);
        }

        var field = type.GetField(memberName, flags);
        return field?.GetValue(instance);
#pragma warning restore S3011
    }

    private static float? TryGetCanvasAlpha(TMP_Text text)
    {
        var canvasRenderer = text.canvasRenderer;
        var method = canvasRenderer.GetType().GetMethod("GetAlpha", Type.EmptyTypes);
        if (method?.ReturnType != typeof(float))
        {
            return null;
        }

        var value = method.Invoke(canvasRenderer, null);
        return value is float floatValue ? floatValue : null;
    }

    private static string BuildRelativePath(Transform root, Transform target)
    {
        var builder = new StringBuilder(target.gameObject.name);
        var current = target.parent;
        while (current is not null && !ReferenceEquals(current, root))
        {
            builder.Insert(0, current.gameObject.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        var normalized = Escape(value);
        if (normalized.Length <= maxLength)
        {
            return normalized.Length == 0 ? "<empty>" : normalized;
        }

#pragma warning disable CA1845
        return normalized.Substring(0, maxLength) + "...";
#pragma warning restore CA1845
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
#else
    internal static bool TryBuildSnapshot(
        object? bookLineInstance,
        string source,
        string rendered,
        out string? logLine,
        string phase = "setData")
    {
        _ = bookLineInstance;
        _ = source;
        _ = rendered;
        _ = phase;
        logLine = null;
        return false;
    }
#endif
}
