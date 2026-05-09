#if HAS_TMP
#if QUDJP_DEV_BUILD
#define QUDJP_HAS_TMP_DEV_BUILD
#endif
#endif

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
#if QUDJP_HAS_TMP_DEV_BUILD
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class InventoryLineTmpLifecycleObservability
{
#if QUDJP_HAS_TMP_DEV_BUILD
    private const int MaxLifecycleLogsPerStage = 48;
    private const int MaxDecisionLogsPerLine = 12;
    private const string TextShellLeafSegment = "TextShell/Text";

    private static readonly ConcurrentDictionary<string, int> LifecycleLogCountsByStage = new();
    private static readonly ConcurrentDictionary<int, int> DecisionLogCountsByLine = new();

    internal static void LogOriginalTmpLifecycle(
        object? lineInstance,
        string stage,
        string? sourceText = null,
        string? translatedText = null,
        bool forceMesh = false)
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled)
        {
            return;
        }

        RuntimeDiagnostics.LogVerboseProbe(() =>
        {
            if (lineInstance is not Component component)
            {
                return null;
            }

            var count = LifecycleLogCountsByStage.AddOrUpdate(stage, 1, static (_, current) => current + 1);
            if (count > MaxLifecycleLogsPerStage)
            {
                return null;
            }

            return BuildOriginalTmpLifecycleLog(component, stage, sourceText, translatedText, forceMesh);
        });
    }

    internal static void LogActiveRefreshDecision(
        object? lineInstance,
        bool isActiveItemLine,
        bool hasActiveReplacement,
        bool refreshSucceeded,
        bool scheduledRepair,
        string? currentText)
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled)
        {
            return;
        }

        RuntimeDiagnostics.LogVerboseProbe(() =>
        {
            if (lineInstance is not Component component)
            {
                return null;
            }

            var lineId = component.GetInstanceID();
            if (!TryReserveDecisionLogSlot(lineId))
            {
                return null;
            }

            var builder = new StringBuilder();
            builder.Append("[QudJP] InventoryLineActiveRefreshDecision/v1: ");
            builder.Append("root='");
            builder.Append(component.gameObject.name);
            builder.Append("' lineId=");
            builder.Append(lineId.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" isActiveItemLine=");
            builder.Append(isActiveItemLine ? "True" : "False");
            builder.Append(" hasActiveReplacement=");
            builder.Append(hasActiveReplacement ? "True" : "False");
            builder.Append(" refreshSucceeded=");
            builder.Append(refreshSucceeded ? "True" : "False");
            builder.Append(" scheduledRepair=");
            builder.Append(scheduledRepair ? "True" : "False");
            builder.Append(" currentText='");
            builder.Append(Truncate(currentText));
            builder.Append('\'');
            return builder.ToString();
        });
    }

    private static string BuildOriginalTmpLifecycleLog(
        Component component,
        string stage,
        string? sourceText,
        string? translatedText,
        bool forceMesh)
    {
        var builder = new StringBuilder();
        builder.Append("[QudJP] InventoryLineOriginalTmpLifecycle/v1: ");
        builder.Append("stage='");
        builder.Append(stage);
        builder.Append("' root='");
        builder.Append(component.gameObject.name);
        builder.Append("' lineId=");
        builder.Append(component.GetInstanceID().ToString(CultureInfo.InvariantCulture));
        builder.Append(" frame=");
        builder.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" forceMesh=");
        builder.Append(forceMesh ? "True" : "False");
        builder.Append(" source='");
        builder.Append(Truncate(sourceText));
        builder.Append("' translated='");
        builder.Append(Truncate(translatedText));
        builder.Append('\'');

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var matches = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            var relativePath = BuildRelativePath(component.transform, text.transform);
#pragma warning disable CA2249
            if (relativePath.IndexOf(TextShellLeafSegment, StringComparison.Ordinal) < 0)
#pragma warning restore CA2249
            {
                continue;
            }

            AppendTmpEntry(builder, text, relativePath, matches);
            matches++;
        }

        if (matches == 0)
        {
            builder.Append("; matches=0");
        }

        return builder.ToString();
    }

    private static void AppendTmpEntry(
        StringBuilder builder,
        TextMeshProUGUI text,
        string relativePath,
        int index)
    {
        var preForceCharacters = text.textInfo.characterCount;
        var preForcePages = text.textInfo.pageCount;
        var rect = text.rectTransform.rect;
        var canvasRenderer = text.canvasRenderer;
        var canvasGroup = text.GetComponentInParent<CanvasGroup>();

        builder.Append("; original[");
        builder.Append(index.ToString(CultureInfo.InvariantCulture));
        builder.Append("] path='");
        builder.Append(relativePath);
        builder.Append("' object='");
        builder.Append(text.gameObject.name);
        builder.Append("' activeSelf=");
        builder.Append(text.gameObject.activeSelf ? "True" : "False");
        builder.Append(" activeInHierarchy=");
        builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
        builder.Append(" enabled=");
        builder.Append(text.enabled ? "True" : "False");
        builder.Append(" isActiveAndEnabled=");
        builder.Append(text.isActiveAndEnabled ? "True" : "False");
        builder.Append(" preForceChars=");
        builder.Append(preForceCharacters.ToString(CultureInfo.InvariantCulture));
        builder.Append(" preForcePages=");
        builder.Append(preForcePages.ToString(CultureInfo.InvariantCulture));
        builder.Append(" rect=");
        builder.Append(rect.width.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append('x');
        builder.Append(rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(" propsChanged=");
        builder.Append(text.havePropertiesChanged ? "True" : "False");
        builder.Append(" canvasRenderer.cull=");
        builder.Append(canvasRenderer is not null && canvasRenderer.cull ? "True" : "False");
        builder.Append(" canvasAlpha=");
        builder.Append(canvasRenderer is null ? "<unknown>" : canvasRenderer.GetAlpha().ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(" canvasGroupAlpha=");
        builder.Append(canvasGroup is null ? "<none>" : canvasGroup.alpha.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(" font='");
        builder.Append(text.font is null ? string.Empty : text.font.name);
        builder.Append("' material='");
        builder.Append(text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name);
        builder.Append("' maxVisibleCharacters=");
        builder.Append(text.maxVisibleCharacters.ToString(CultureInfo.InvariantCulture));
        builder.Append(" maxVisibleLines=");
        builder.Append(text.maxVisibleLines.ToString(CultureInfo.InvariantCulture));
        builder.Append(" pageToDisplay=");
        builder.Append(text.pageToDisplay.ToString(CultureInfo.InvariantCulture));
        builder.Append(" richText=");
        builder.Append(text.richText ? "True" : "False");
        builder.Append(" text='");
        builder.Append(Truncate(text.text));
        builder.Append('\'');
    }

    private static bool TryReserveDecisionLogSlot(int lineId)
    {
        while (true)
        {
            if (!DecisionLogCountsByLine.TryGetValue(lineId, out var current))
            {
                if (DecisionLogCountsByLine.TryAdd(lineId, 1))
                {
                    return true;
                }

                continue;
            }

            if (current >= MaxDecisionLogsPerLine)
            {
                return false;
            }

            if (DecisionLogCountsByLine.TryUpdate(lineId, current + 1, current))
            {
                return true;
            }
        }
    }

    private static string BuildRelativePath(Transform root, Transform target)
    {
        var segments = new System.Collections.Generic.Stack<string>();
        var current = target;
        while (current != root && current is not null)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", segments);
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

#pragma warning disable CA1845
        var truncated = value!.Length <= 96 ? value : value.Substring(0, 96) + "...";
#pragma warning restore CA1845
        return truncated.Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
#else
    internal static void LogOriginalTmpLifecycle(
        object? lineInstance,
        string stage,
        string? sourceText = null,
        string? translatedText = null,
        bool forceMesh = false)
    {
        _ = lineInstance;
        _ = stage;
        _ = sourceText;
        _ = translatedText;
        _ = forceMesh;
    }

    internal static void LogActiveRefreshDecision(
        object? lineInstance,
        bool isActiveItemLine,
        bool hasActiveReplacement,
        bool refreshSucceeded,
        bool scheduledRepair,
        string? currentText)
    {
        _ = lineInstance;
        _ = isActiveItemLine;
        _ = hasActiveReplacement;
        _ = refreshSucceeded;
        _ = scheduledRepair;
        _ = currentText;
    }
#endif
}
