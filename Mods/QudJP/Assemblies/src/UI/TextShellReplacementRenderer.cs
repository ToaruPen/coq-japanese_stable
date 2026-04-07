#if HAS_TMP
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;
using UguiText = UnityEngine.UI.Text;
#endif

namespace QudJP;

internal static class TextShellReplacementRenderer
{
#if HAS_TMP
    internal enum ReplacementRenderAction
    {
        AttemptReplacement,
        PreserveActiveReplacement,
        DisableReplacement,
    }

    private const string ReplacementObjectName = "QudJPReplacementText";
    private const string LegacyReplacementObjectName = "QudJPReplacementLegacyText";
    private static readonly ConcurrentDictionary<string, byte> FailureProbeLogged = new();
    private static readonly ConcurrentDictionary<string, byte> DisableProbeLogged = new();

    internal static int TryRenderReplacementTexts(object? componentInstance, out string? logLine)
    {
        logLine = null;
        if (componentInstance is not Component component)
        {
            return 0;
        }

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var builder = new StringBuilder();
        builder.Append("[QudJP] InventoryLineReplacement/v1: root='");
        builder.Append(component.gameObject.name);
        builder.Append('\'');

        var replaced = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            var original = texts[index];
            var relativePath = BuildRelativePath(component.transform, original.transform);
            if (!IsTextShellLeaf(relativePath))
            {
                continue;
            }

            original.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (!original.enabled || !original.gameObject.activeInHierarchy || original.textInfo.characterCount > 0)
            {
                if (!original.enabled
                    && TryReuseActiveReplacement(original.transform.parent, relativePath, builder, ref replaced))
                {
                    continue;
                }

                if (TryBuildDisableProbe(component, relativePath, original, out var disableLog)
                    && disableLog is not null
                    && disableLog.Length > 0)
                {
                    UnityEngine.Debug.Log(disableLog);
                }

                TryDisableReplacement(original.transform.parent);
                continue;
            }

            var shell = original.transform.parent as RectTransform;
            if (shell is null)
            {
                continue;
            }

            var replacement = GetOrCreateReplacement(shell, original);
            if (replacement is null)
            {
                continue;
            }

            var creationStageLog = new StringBuilder();
            AppendCreationStageSnapshot(creationStageLog, "afterGetOrCreate", replacement);

            SyncReplacement(replacement, original);
            AppendCreationStageSnapshot(creationStageLog, "afterSync", replacement);
            replacement.text = original.text;
            AppendCreationStageSnapshot(creationStageLog, "afterTextAssign", replacement);
            replacement.gameObject.SetActive(true);
            AppendCreationStageSnapshot(creationStageLog, "afterSetActive", replacement);
            TryForceCanvasUpdate();
            AppendCreationStageSnapshot(creationStageLog, "afterSetActiveCanvasForce", replacement);
            replacement.enabled = true;
            AppendCreationStageSnapshot(creationStageLog, "afterEnable", replacement);
            if (replacement.textInfo.characterCount > 0)
            {
                original.enabled = false;
                StabilizeSuccessfulReplacement(replacement);
                replaced++;
                replacement.transform.SetAsLastSibling();
                builder.Append("; leaf[");
                builder.Append(replaced.ToString(CultureInfo.InvariantCulture));
                builder.Append("] path='");
                builder.Append(relativePath);
                builder.Append("' replacementKind='tmp' replacementChars=");
                builder.Append(replacement.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" replacementPageCount=");
                builder.Append(replacement.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" parent='");
                builder.Append(shell.name);
                builder.Append("' phase='afterEnable'");
                continue;
            }

            FontManager.ApplyToText(replacement);
            AppendCreationStageSnapshot(creationStageLog, "afterEnableFontApply", replacement);
            SyncReplacement(replacement, original);
            AppendCreationStageSnapshot(creationStageLog, "afterEnableResync", replacement);
            var originalEnabled = original.enabled;
            original.enabled = false;
            replacement.havePropertiesChanged = true;
            replacement.SetAllDirty();
            replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            AppendCreationStageSnapshot(creationStageLog, "afterOriginalDisableRefresh", replacement);
            original.enabled = originalEnabled;
            TryForceCanvasUpdate();
            AppendCreationStageSnapshot(creationStageLog, "afterEnableCanvasForce", replacement);
            replacement.havePropertiesChanged = true;
            replacement.SetAllDirty();
            AppendCreationStageSnapshot(creationStageLog, "afterDirty", replacement);
            replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            AppendCreationStageSnapshot(creationStageLog, "afterForceMesh", replacement);

            if (replacement.textInfo.characterCount == 0)
            {
                if (TryBuildReplacementFailureProbe(component, relativePath, original, replacement, creationStageLog.ToString(), out var failureLog)
                    && failureLog is not null
                    && failureLog.Length > 0)
                {
                    UnityEngine.Debug.Log(failureLog);
                }

                replacement.enabled = false;
                replacement.gameObject.SetActive(false);
                continue;
            }

            replaced++;
            original.enabled = false;
            replacement.transform.SetAsLastSibling();
            builder.Append("; leaf[");
            builder.Append(replaced.ToString(CultureInfo.InvariantCulture));
            builder.Append("] path='");
            builder.Append(relativePath);
            builder.Append("' replacementKind='tmp' replacementChars=");
            builder.Append(replacement.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" replacementPageCount=");
            builder.Append(replacement.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" parent='");
            builder.Append(shell.name);
            builder.Append('\'');
        }

        if (replaced == 0)
        {
            builder.Append("; replaced=0");
        }

        logLine = builder.ToString();
        return replaced;
    }

    internal static bool ShouldPreserveActiveReplacementForTests(bool originalEnabled, bool originalActiveInHierarchy)
    {
        return !originalEnabled || !originalActiveInHierarchy;
    }

    internal static ReplacementRenderAction DecideRenderActionForTests(
        bool originalEnabled,
        bool originalActiveInHierarchy,
        int originalCharacterCount)
    {
        return DecideRenderAction(originalEnabled, originalActiveInHierarchy, originalCharacterCount);
    }

    internal static TextOverflowModes GetReplacementOverflowModeForTests()
    {
        return TextOverflowModes.Overflow;
    }

    internal static bool IsReplacementTextNameForTests(string objectName)
    {
        return string.Equals(objectName, ReplacementObjectName, StringComparison.Ordinal);
    }

    internal static bool TryBuildReplacementLifecycleSnapshot(
        TextMeshProUGUI replacement,
        string probeName,
        string trigger,
        out string? logLine)
    {
        logLine = null;
        if (!string.Equals(replacement.gameObject.name, ReplacementObjectName, StringComparison.Ordinal))
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": frame=");
        builder.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" trigger='");
        builder.Append(trigger);
        builder.Append("' root='");
        var owningInventoryLineName = FindOwningInventoryLineName(replacement.transform);
        if (owningInventoryLineName is null)
        {
            Trace.TraceWarning("QudJP: Could not resolve owning inventory line for replacement lifecycle probe.");
            builder.Append("<unknown>");
        }
        else
        {
            builder.Append(owningInventoryLineName);
        }
        builder.Append("' path='");
        builder.Append(BuildAbsolutePath(replacement.transform));
        builder.Append("' ");
        AppendCreationStageSnapshot(builder, "state", replacement);
        logLine = builder.ToString();
        return true;
    }

    internal static bool TryBuildReplacementState(object? componentInstance, string probeName, out string? logLine)
    {
        logLine = null;
        if (componentInstance is not Component component)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(probeName);
        builder.Append(": root='");
        builder.Append(component.gameObject.name);
        builder.Append('\'');

        var texts = component.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        var matches = 0;
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            if (!string.Equals(text.gameObject.name, ReplacementObjectName, StringComparison.Ordinal))
            {
                continue;
            }

            builder.Append("; replacement[");
            builder.Append(matches.ToString(CultureInfo.InvariantCulture));
            builder.Append("] path='");
            builder.Append(BuildRelativePath(component.transform, text.transform));
            builder.Append("' rect=");
            builder.Append(text.rectTransform.rect.width.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(text.rectTransform.rect.height.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" pos=");
            builder.Append(text.rectTransform.anchoredPosition.x.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(text.rectTransform.anchoredPosition.y.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" chars=");
            builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pageCount=");
            builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" active=");
            builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(" enabled=");
            builder.Append(text.enabled ? "True" : "False");
            builder.Append(" propsChanged=");
            builder.Append(text.havePropertiesChanged ? "True" : "False");
            builder.Append(" isAwake=");
            builder.Append(GetBoolFieldValue(text, "m_isAwake")?.ToString() ?? "<unknown>");
            builder.Append(" registered=");
            builder.Append(GetBoolFieldValue(text, "m_isRegisteredForEvents")?.ToString() ?? "<unknown>");
            builder.Append(" ignoreActiveState=");
            builder.Append(GetBoolFieldValue(text, "m_ignoreActiveState")?.ToString() ?? "<unknown>");
            builder.Append(" hasCanvas=");
            builder.Append(GetFieldValue(text, "m_canvas") is null ? "False" : "True");
            builder.Append(" font='");
            builder.Append(text.font is null ? string.Empty : text.font.name);
            builder.Append("' internalFont='");
            builder.Append(GetTmpFontFieldName(text, "m_fontAsset"));
            builder.Append("' material='");
            builder.Append(text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name);
            builder.Append("' internalSharedMaterial='");
            builder.Append(GetMaterialFieldName(text, "m_sharedMaterial"));
            builder.Append("' canvasA=");
            builder.Append(TryGetCanvasAlpha(text)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
            builder.Append(" subMeshes=");
            builder.Append(text.GetComponentsInChildren<TMP_SubMeshUI>(includeInactive: true).Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" text='");
            builder.Append(text.text);
            builder.Append('\'');
            matches++;
        }

        var legacyTexts = component.GetComponentsInChildren<UguiText>(includeInactive: true);
        for (var index = 0; index < legacyTexts.Length; index++)
        {
            var text = legacyTexts[index];
            if (!string.Equals(text.gameObject.name, LegacyReplacementObjectName, StringComparison.Ordinal))
            {
                continue;
            }

            builder.Append("; legacyReplacement[");
            builder.Append(matches.ToString(CultureInfo.InvariantCulture));
            builder.Append("] path='");
            builder.Append(BuildRelativePath(component.transform, text.transform));
            builder.Append("' rect=");
            builder.Append(text.rectTransform.rect.width.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(text.rectTransform.rect.height.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" pos=");
            builder.Append(text.rectTransform.anchoredPosition.x.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(text.rectTransform.anchoredPosition.y.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" active=");
            builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
            builder.Append(" enabled=");
            builder.Append(text.enabled ? "True" : "False");
            builder.Append(" text='");
            builder.Append(text.text);
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

    private static void TryDisableReplacement(Transform? shell)
    {
        if (shell is null)
        {
            return;
        }

        var replacement = shell.Find(ReplacementObjectName);

        if (replacement is null)
        {
            return;
        }

        var replacementText = replacement.GetComponent<TextMeshProUGUI>();
        if (replacementText is not null)
        {
            replacementText.enabled = false;
        }

        replacement.gameObject.SetActive(false);
        TryDisableLegacyReplacement(shell);
    }

    private static bool TryReuseActiveReplacement(
        Transform? shell,
        string relativePath,
        StringBuilder builder,
        ref int replaced)
    {
        if (shell is null)
        {
            return false;
        }

        var replacementTransform = shell.Find(ReplacementObjectName);
        if (replacementTransform is null)
        {
            return false;
        }

        var replacement = replacementTransform.GetComponent<TextMeshProUGUI>();
        if (replacement is null
            || !replacement.gameObject.activeInHierarchy
            || !replacement.enabled
            || string.IsNullOrEmpty(replacement.text))
        {
            return false;
        }

        replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
        var recovered = false;
        if (replacement.textInfo.characterCount == 0)
        {
            recovered = TryRecoverDegradedReplacement(replacement);
        }

        if (replacement.textInfo.characterCount == 0 && !recovered)
        {
            return false;
        }

        replaced++;
        replacement.transform.SetAsLastSibling();
        builder.Append("; leaf[");
        builder.Append(replaced.ToString(CultureInfo.InvariantCulture));
        builder.Append("] path='");
        builder.Append(relativePath);
        builder.Append("' replacementKind='tmp' replacementChars=");
        builder.Append(replacement.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" replacementPageCount=");
        builder.Append(replacement.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" parent='");
        builder.Append(shell.name);
        builder.Append("' phase='");
        builder.Append(recovered ? "reuseActiveRecovered" : "reuseActive");
        builder.Append('\'');
        return true;
    }

    private static bool TryRecoverDegradedReplacement(TextMeshProUGUI replacement)
    {
        if (!replacement.enabled || !replacement.gameObject.activeInHierarchy || string.IsNullOrEmpty(replacement.text))
        {
            return false;
        }

        var originalText = replacement.text;
        if (replacement.transform.parent is RectTransform parentRect)
        {
            TryForceLayoutRebuild(parentRect);
        }

        TryForceLayoutRebuild(replacement.rectTransform);
        TryForceCanvasUpdate();

        replacement.havePropertiesChanged = true;
        replacement.SetAllDirty();
        replacement.text = originalText;
        replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        if (replacement.textInfo.characterCount > 0)
        {
            StabilizeSuccessfulReplacement(replacement);
            return true;
        }

        FontManager.ForcePrimaryFont(replacement);
        if (replacement.font is not null)
        {
            replacement.fontSharedMaterial = replacement.font.material;
        }

        replacement.havePropertiesChanged = true;
        replacement.SetAllDirty();
        replacement.text = originalText;
        TryForceCanvasUpdate();
        replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        if (replacement.textInfo.characterCount > 0)
        {
            StabilizeSuccessfulReplacement(replacement);
            return true;
        }

        return false;
    }

    private static void StabilizeSuccessfulReplacement(TextMeshProUGUI replacement)
    {
        replacement.havePropertiesChanged = false;
        TrySetBoolFieldValue(replacement, "m_isLayoutDirty", false);
        TrySetBoolFieldValue(replacement, "m_VertsDirty", false);
        TrySetBoolFieldValue(replacement, "m_MaterialDirty", false);
        TrySetBoolFieldValue(replacement, "m_ignoreActiveState", false);
        TryInvokeLifecycleMethod(replacement, "RecalculateClipping");
        TryInvokeLifecycleMethod(replacement, "RecalculateMasking");
        TryForceCanvasUpdate();
    }

    private static ReplacementRenderAction DecideRenderAction(
        bool originalEnabled,
        bool originalActiveInHierarchy,
        int originalCharacterCount)
    {
        if (originalEnabled && originalActiveInHierarchy && originalCharacterCount == 0)
        {
            return ReplacementRenderAction.AttemptReplacement;
        }

        return ShouldPreserveActiveReplacementForTests(originalEnabled, originalActiveInHierarchy)
            ? ReplacementRenderAction.PreserveActiveReplacement
            : ReplacementRenderAction.DisableReplacement;
    }

    private static void TryDisableLegacyReplacement(Transform? parent)
    {
        if (parent is null)
        {
            return;
        }

        var replacement = parent.Find(LegacyReplacementObjectName);
        if (replacement is null)
        {
            return;
        }

        var replacementText = replacement.GetComponent<UguiText>();
        if (replacementText is not null)
        {
            replacementText.enabled = false;
        }

        replacement.gameObject.SetActive(false);
    }

    private static TextMeshProUGUI? GetOrCreateReplacement(RectTransform shell, TextMeshProUGUI original)
    {
        var existing = shell.Find(ReplacementObjectName);

        if (existing is not null)
        {
            if (!ReferenceEquals(existing.parent, shell))
            {
                existing.SetParent(shell, false);
            }

            return existing.GetComponent<TextMeshProUGUI>();
        }

        var gameObject = new GameObject(ReplacementObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        gameObject.layer = original.gameObject.layer;
        gameObject.transform.SetParent(shell, false);
        gameObject.SetActive(false);
        var rectTransform = (RectTransform)gameObject.transform;
        rectTransform.anchorMin = original.rectTransform.anchorMin;
        rectTransform.anchorMax = original.rectTransform.anchorMax;
        rectTransform.anchoredPosition = original.rectTransform.anchoredPosition;
        rectTransform.sizeDelta = original.rectTransform.sizeDelta;
        rectTransform.pivot = original.rectTransform.pivot;
        rectTransform.localScale = original.rectTransform.localScale;
        rectTransform.localRotation = original.rectTransform.localRotation;

        var replacement = gameObject.GetComponent<TextMeshProUGUI>();
        if (replacement is null)
        {
            return null;
        }

        replacement.raycastTarget = original.raycastTarget;
        replacement.enabled = false;
        return replacement;
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
        replacement.maxVisibleCharacters = original.maxVisibleCharacters;
        replacement.maxVisibleLines = original.maxVisibleLines;
        replacement.pageToDisplay = original.pageToDisplay;
        if (original.fontSharedMaterial is not null)
        {
            replacement.fontSharedMaterial = original.fontSharedMaterial;
        }

        replacement.text = original.text;
        FontManager.ApplyToText(replacement);

    }

    private static bool IsTextShellLeaf(string relativePath)
    {
#pragma warning disable CA2249
        return relativePath.IndexOf("TextShell/Text", StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
    }

    private static bool TryBuildReplacementFailureProbe(
        Component root,
        string relativePath,
        TextMeshProUGUI original,
        TextMeshProUGUI replacement,
        string creationStageLog,
        out string? logLine)
    {
        var key = root.GetInstanceID().ToString(CultureInfo.InvariantCulture) + ":" + relativePath;
        if (!FailureProbeLogged.TryAdd(key, 0))
        {
            logLine = null;
            return false;
        }

        var originalText = replacement.text;
        var replacementFontMaterial = replacement.font is null ? null : replacement.font.material;
        var replacementFontMaterialName = replacementFontMaterial is null ? string.Empty : replacementFontMaterial.name;
        var replacementInternalFontName = GetTmpFontFieldName(replacement, "m_fontAsset");
        var replacementInternalSharedMaterialName = GetMaterialFieldName(replacement, "m_sharedMaterial");
        var sharedEqualsFontMaterial = replacementFontMaterial is not null
            && replacement.fontSharedMaterial is not null
            && ReferenceEquals(replacement.fontSharedMaterial, replacementFontMaterial);

        replacement.text = "TEST";
        replacement.havePropertiesChanged = true;
        replacement.SetAllDirty();
        replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        var sentinelChars = replacement.textInfo.characterCount;
        var sentinelPages = replacement.textInfo.pageCount;
        replacement.text = originalText;
        replacement.havePropertiesChanged = true;
        replacement.SetAllDirty();
        replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

        RunFontMaterialDiagnosticRetry(
            replacement,
            originalText,
            out var materialRetryCurrentChars,
            out var materialRetryCurrentPages,
            out var materialRetrySentinelChars,
            out var materialRetrySentinelPages);

        var builder = new StringBuilder();
        builder.Append("[QudJP] InventoryLineReplacementFailure/v1: root='");
        builder.Append(root.gameObject.name);
        builder.Append("' path='");
        builder.Append(relativePath);
        builder.Append("' original={enabled=");
        builder.Append(original.enabled ? "True" : "False");
        builder.Append(", activeSelf=");
        builder.Append(original.gameObject.activeSelf ? "True" : "False");
        builder.Append(", activeInHierarchy=");
        builder.Append(original.gameObject.activeInHierarchy ? "True" : "False");
        builder.Append(", chars=");
        builder.Append(original.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(", pageCount=");
        builder.Append(original.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(", rect=");
        builder.Append(original.rectTransform.rect.width.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append('x');
        builder.Append(original.rectTransform.rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(", parent='");
        builder.Append(original.transform.parent is null ? string.Empty : original.transform.parent.name);
        builder.Append("', font='");
        builder.Append(original.font is null ? string.Empty : original.font.name);
        builder.Append("', material='");
        builder.Append(original.fontSharedMaterial is null ? string.Empty : original.fontSharedMaterial.name);
        builder.Append("'} replacement={enabled=");
        builder.Append(replacement.enabled ? "True" : "False");
        builder.Append(", activeSelf=");
        builder.Append(replacement.gameObject.activeSelf ? "True" : "False");
        builder.Append(", activeInHierarchy=");
        builder.Append(replacement.gameObject.activeInHierarchy ? "True" : "False");
        builder.Append(", chars=");
        builder.Append(replacement.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(", pageCount=");
        builder.Append(replacement.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(", sentinelChars=");
        builder.Append(sentinelChars.ToString(CultureInfo.InvariantCulture));
        builder.Append(", sentinelPages=");
        builder.Append(sentinelPages.ToString(CultureInfo.InvariantCulture));
        builder.Append(", rect=");
        builder.Append(replacement.rectTransform.rect.width.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append('x');
        builder.Append(replacement.rectTransform.rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(", parent='");
        builder.Append(replacement.transform.parent is null ? string.Empty : replacement.transform.parent.name);
        builder.Append("', font='");
        builder.Append(replacement.font is null ? string.Empty : replacement.font.name);
        builder.Append("', material='");
        builder.Append(replacement.fontSharedMaterial is null ? string.Empty : replacement.fontSharedMaterial.name);
        builder.Append("', fontMaterial='");
        builder.Append(replacementFontMaterialName);
        builder.Append("', internalFont='");
        builder.Append(replacementInternalFontName);
        builder.Append("', internalSharedMaterial='");
        builder.Append(replacementInternalSharedMaterialName);
        builder.Append("', sharedEqualsFontMaterial=");
        builder.Append(sharedEqualsFontMaterial ? "True" : "False");
        builder.Append(", materialRetryCurrentChars=");
        builder.Append(materialRetryCurrentChars.ToString(CultureInfo.InvariantCulture));
        builder.Append(", materialRetryCurrentPages=");
        builder.Append(materialRetryCurrentPages.ToString(CultureInfo.InvariantCulture));
        builder.Append(", materialRetrySentinelChars=");
        builder.Append(materialRetrySentinelChars.ToString(CultureInfo.InvariantCulture));
        builder.Append(", materialRetrySentinelPages=");
        builder.Append(materialRetrySentinelPages.ToString(CultureInfo.InvariantCulture));
        builder.Append("'}");
        if (creationStageLog.Length > 0)
        {
            builder.Append(" creationStages={");
            builder.Append(creationStageLog);
            builder.Append('}');
        }

        logLine = builder.ToString();
        return true;
    }

    private static void AppendCreationStageSnapshot(StringBuilder builder, string stageName, TextMeshProUGUI text)
    {
        var subMeshes = text.GetComponentsInChildren<TMP_SubMeshUI>(includeInactive: true);
        if (builder.Length > 0)
        {
            builder.Append("; ");
        }

        builder.Append(stageName);
        builder.Append("={activeSelf=");
        builder.Append(text.gameObject.activeSelf ? "True" : "False");
        builder.Append(", active=");
        builder.Append(text.gameObject.activeInHierarchy ? "True" : "False");
        builder.Append(", enabled=");
        builder.Append(text.enabled ? "True" : "False");
        builder.Append(", sibling=");
        builder.Append(text.transform.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
        builder.Append('/');
        builder.Append(text.transform.parent is null ? "<none>" : text.transform.parent.childCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(", propsChanged=");
        builder.Append(text.havePropertiesChanged ? "True" : "False");
        builder.Append(", layoutDirty=");
        builder.Append(GetBoolFieldValue(text, "m_isLayoutDirty")?.ToString() ?? "<unknown>");
        builder.Append(", vertsDirty=");
        builder.Append(GetBoolFieldValue(text, "m_VertsDirty")?.ToString() ?? "<unknown>");
        builder.Append(", materialDirty=");
        builder.Append(GetBoolFieldValue(text, "m_MaterialDirty")?.ToString() ?? "<unknown>");
        builder.Append(", chars=");
        builder.Append(text.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(", visibleChars=");
        builder.Append(CountVisibleCharacters(text).ToString(CultureInfo.InvariantCulture));
        builder.Append(", pages=");
        builder.Append(text.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(", rect=");
        builder.Append(text.rectTransform.rect.width.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append('x');
        builder.Append(text.rectTransform.rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(", worldCenter=");
        AppendVector3(builder, text.rectTransform.TransformPoint(text.rectTransform.rect.center));
        builder.Append(", marginW=");
        builder.Append(TryGetFloatFieldValue(text, "m_marginWidth")?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(", marginH=");
        builder.Append(TryGetFloatFieldValue(text, "m_marginHeight")?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(", overflow=");
        builder.Append(text.overflowMode);
        builder.Append(", wrap=");
        builder.Append(text.textWrappingMode);
        builder.Append(", maxChars=");
        builder.Append(text.maxVisibleCharacters.ToString(CultureInfo.InvariantCulture));
        builder.Append(", maxLines=");
        builder.Append(text.maxVisibleLines.ToString(CultureInfo.InvariantCulture));
        builder.Append(", pageToDisplay=");
        builder.Append(text.pageToDisplay.ToString(CultureInfo.InvariantCulture));
        builder.Append(", maskable=");
        builder.Append(text.maskable ? "True" : "False");
        builder.Append(", alpha=");
        builder.Append(text.alpha.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(", colorA=");
        builder.Append(text.color.a.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(", isAwake=");
        builder.Append(GetBoolFieldValue(text, "m_isAwake")?.ToString() ?? "<unknown>");
        builder.Append(", registered=");
        builder.Append(GetBoolFieldValue(text, "m_isRegisteredForEvents")?.ToString() ?? "<unknown>");
        builder.Append(", ignoreActiveState=");
        builder.Append(GetBoolFieldValue(text, "m_ignoreActiveState")?.ToString() ?? "<unknown>");
        builder.Append(", textTruncated=");
        builder.Append(GetBoolFieldValue(text, "m_isTextTruncated")?.ToString() ?? "<unknown>");
        builder.Append(", autoSizePointSet=");
        builder.Append(GetBoolFieldValue(text, "m_IsAutoSizePointSizeSet")?.ToString() ?? "<unknown>");
        builder.Append(", inputParsingRequired=");
        builder.Append(GetBoolFieldValue(text, "m_isInputParsingRequired")?.ToString() ?? "<unknown>");
        builder.Append(", hasCanvas=");
        builder.Append(GetFieldValue(text, "m_canvas") is null ? "False" : "True");
        builder.Append(", renderMode=");
        builder.Append(GetFieldValue(text, "m_renderMode")?.ToString() ?? "<unknown>");
        builder.Append(", firstUnicode=");
        builder.Append(TryGetFirstTextProcessingUnicode(text)?.ToString(CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(", canvasA=");
        builder.Append(TryGetCanvasAlpha(text)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(", canvasCull=");
        builder.Append(TryGetCanvasCull(text)?.ToString() ?? "<unknown>");
        builder.Append(", parentCanvasGroupA=");
        builder.Append(TryGetParentCanvasGroupAlpha(text.transform)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(", stencil=");
        builder.Append(TryGetMaterialInt(text.fontSharedMaterial, "_Stencil")?.ToString(CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(", faceA=");
        builder.Append(TryGetFaceColorAlpha(text.fontSharedMaterial)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "<unknown>");
        builder.Append(", font='");
        builder.Append(text.font is null ? string.Empty : text.font.name);
        builder.Append("', internalFont='");
        builder.Append(GetTmpFontFieldName(text, "m_fontAsset"));
        builder.Append("', material='");
        builder.Append(text.fontSharedMaterial is null ? string.Empty : text.fontSharedMaterial.name);
        builder.Append("', internalSharedMaterial='");
        builder.Append(GetMaterialFieldName(text, "m_sharedMaterial"));
        builder.Append("', subMeshes=");
        builder.Append(subMeshes.Length.ToString(CultureInfo.InvariantCulture));
        AppendFirstVisibleCharacterSnapshot(builder, text);
        if (subMeshes.Length > 0)
        {
            var subMesh = subMeshes[0];
            builder.Append(", sub0Font='");
            builder.Append(subMesh.fontAsset is null ? string.Empty : subMesh.fontAsset.name);
            builder.Append("', sub0Material='");
            builder.Append(subMesh.sharedMaterial is null ? string.Empty : subMesh.sharedMaterial.name);
            builder.Append('\'');
        }

        builder.Append('}');
    }

    private static void RunFontMaterialDiagnosticRetry(
        TextMeshProUGUI replacement,
        string originalText,
        out int currentChars,
        out int currentPages,
        out int sentinelChars,
        out int sentinelPages)
    {
        currentChars = -1;
        currentPages = -1;
        sentinelChars = -1;
        sentinelPages = -1;

        var fontMaterial = replacement.font is null ? null : replacement.font.material;
        if (fontMaterial is null)
        {
            return;
        }

        var originalSharedMaterial = replacement.fontSharedMaterial;
        try
        {
            replacement.fontSharedMaterial = fontMaterial;
            replacement.text = originalText;
            replacement.havePropertiesChanged = true;
            replacement.SetAllDirty();
            replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            currentChars = replacement.textInfo.characterCount;
            currentPages = replacement.textInfo.pageCount;

            replacement.text = "TEST";
            replacement.havePropertiesChanged = true;
            replacement.SetAllDirty();
            replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            sentinelChars = replacement.textInfo.characterCount;
            sentinelPages = replacement.textInfo.pageCount;
        }
        finally
        {
            replacement.fontSharedMaterial = originalSharedMaterial;
            replacement.text = originalText;
            replacement.havePropertiesChanged = true;
            replacement.SetAllDirty();
            replacement.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        }
    }

    private static string GetTmpFontFieldName(TMP_Text text, string fieldName)
    {
        var fieldValue = GetFieldValue(text, fieldName);
        return fieldValue is TMP_FontAsset fontAsset ? fontAsset.name : string.Empty;
    }

    private static string GetMaterialFieldName(TMP_Text text, string fieldName)
    {
        var fieldValue = GetFieldValue(text, fieldName);
        return fieldValue is Material material ? material.name : string.Empty;
    }

    private static bool? GetBoolFieldValue(object instance, string fieldName)
    {
        var fieldValue = GetFieldValue(instance, fieldName);
        return fieldValue is bool boolValue ? boolValue : null;
    }

    private static object? GetFieldValue(object instance, string fieldName)
    {
        var type = instance.GetType();
        while (type is not null)
        {
#pragma warning disable S3011
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
#pragma warning restore S3011
            if (field is not null)
            {
#pragma warning disable S3011
                return field.GetValue(instance);
#pragma warning restore S3011
            }

            type = type.BaseType;
        }

        return null;
    }

    private static void TrySetBoolFieldValue(object instance, string fieldName, bool value)
    {
        var type = instance.GetType();
        while (type is not null)
        {
#pragma warning disable S3011
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
#pragma warning restore S3011
            if (field is not null && field.FieldType == typeof(bool))
            {
#pragma warning disable S3011
                field.SetValue(instance, value);
#pragma warning restore S3011
                return;
            }

            type = type.BaseType;
        }
    }

    private static void TryInvokeLifecycleMethod(object instance, string methodName)
    {
        var type = instance.GetType();
        while (type is not null)
        {
#pragma warning disable S3011
            var method = type.GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
#pragma warning restore S3011
            if (method is not null)
            {
#pragma warning disable S3011
                method.Invoke(instance, null);
#pragma warning restore S3011
                return;
            }

            type = type.BaseType;
        }
    }

    private static float? TryGetFloatFieldValue(object instance, string fieldName)
    {
        return GetFieldValue(instance, fieldName) switch
        {
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            _ => null,
        };
    }

    private static int? TryGetMaterialInt(Material? material, string propertyName)
    {
        return material is not null && material.HasProperty(propertyName) ? material.GetInt(propertyName) : null;
    }

    private static uint? TryGetFirstTextProcessingUnicode(TMP_Text text)
    {
        if (GetFieldValue(text, "m_TextProcessingArray") is not Array processingArray || processingArray.Length == 0)
        {
            return null;
        }

        var first = processingArray.GetValue(0);
        if (first is null)
        {
            return null;
        }

        var unicodeValue = GetFieldValue(first, "unicode");
        return unicodeValue switch
        {
            uint uintValue => uintValue,
            int intValue when intValue >= 0 => (uint)intValue,
            _ => null,
        };
    }

    private static int CountVisibleCharacters(TMP_Text text)
    {
        var count = 0;
        for (var index = 0; index < text.textInfo.characterCount; index++)
        {
            if (text.textInfo.characterInfo[index].isVisible)
            {
                count++;
            }
        }

        return count;
    }

    private static void AppendFirstVisibleCharacterSnapshot(StringBuilder builder, TMP_Text text)
    {
        for (var index = 0; index < text.textInfo.characterCount; index++)
        {
            var character = text.textInfo.characterInfo[index];
            if (!character.isVisible)
            {
                continue;
            }

            builder.Append(", firstVisibleChar='");
            builder.Append(char.ConvertFromUtf32(character.character));
            builder.Append("' charIndex=");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
            builder.Append(" quad=");
            builder.Append(character.bottomLeft.x.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(character.bottomLeft.y.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append("->");
            builder.Append(character.topRight.x.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(character.topRight.y.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" worldQuad=");
            AppendVector3(builder, text.rectTransform.TransformPoint(character.bottomLeft));
            builder.Append("->");
            AppendVector3(builder, text.rectTransform.TransformPoint(character.topRight));
            return;
        }

        builder.Append(", firstVisibleChar=<none>");
    }

    private static void AppendVector3(StringBuilder builder, Vector3 value)
    {
        builder.Append(value.x.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.y.ToString("0.###", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.z.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static float? TryGetFaceColorAlpha(Material? material)
    {
        if (material is null || !material.HasProperty("_FaceColor"))
        {
            return null;
        }

        return material.GetColor("_FaceColor").a;
    }

    private static float? TryGetCanvasAlpha(TMP_Text text)
    {
        var canvasRendererProperty = text.GetType().GetProperty(
            "canvasRenderer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var canvasRenderer = canvasRendererProperty?.GetValue(text);
        if (canvasRenderer is null)
        {
            return null;
        }

        var getAlphaMethod = canvasRenderer.GetType().GetMethod(
            "GetAlpha",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (getAlphaMethod?.Invoke(canvasRenderer, null) is float alpha)
        {
            return alpha;
        }

        return null;
    }

    private static bool? TryGetCanvasCull(TMP_Text text)
    {
        var canvasRendererProperty = text.GetType().GetProperty(
            "canvasRenderer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var canvasRenderer = canvasRendererProperty?.GetValue(text);
        if (canvasRenderer is null)
        {
            return null;
        }

        var cullProperty = canvasRenderer.GetType().GetProperty(
            "cull",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return cullProperty?.GetValue(canvasRenderer) as bool?;
    }

    private static float? TryGetParentCanvasGroupAlpha(Transform transform)
    {
        var current = transform.parent;
        while (current is not null)
        {
            var component = current.GetComponent("CanvasGroup");
            if (component is not null)
            {
                var alphaProperty = component.GetType().GetProperty(
                    "alpha",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (alphaProperty?.GetValue(component) is float alpha)
                {
                    return alpha;
                }
            }

            current = current.parent;
        }

        return null;
    }

    private static void TryForceCanvasUpdate()
    {
        try
        {
            var canvasType = Type.GetType("UnityEngine.Canvas, UnityEngine.CoreModule", throwOnError: false);
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
            Trace.TraceWarning("QudJP: TextShellReplacementRenderer.TryForceCanvasUpdate failed: {0}", ex.Message);
        }
    }

    private static void TryForceLayoutRebuild(RectTransform rectTransform)
    {
        try
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: TextShellReplacementRenderer.TryForceLayoutRebuild failed: {0}", ex.Message);
        }
    }

    private static bool TryBuildDisableProbe(
        Component root,
        string relativePath,
        TextMeshProUGUI original,
        out string? logLine)
    {
        var key = root.GetInstanceID().ToString(CultureInfo.InvariantCulture)
            + ":"
            + relativePath
            + ":"
            + (original.enabled ? "1" : "0")
            + (original.gameObject.activeInHierarchy ? "1" : "0")
            + ":"
            + original.textInfo.characterCount.ToString(CultureInfo.InvariantCulture);
        if (!DisableProbeLogged.TryAdd(key, 0))
        {
            logLine = null;
            return false;
        }

        var builder = new StringBuilder();
        builder.Append("[QudJP] InventoryLineReplacementDisable/v1: root='");
        builder.Append(root.gameObject.name);
        builder.Append("' path='");
        builder.Append(relativePath);
        builder.Append("' originalEnabled=");
        builder.Append(original.enabled ? "True" : "False");
        builder.Append(" originalActiveSelf=");
        builder.Append(original.gameObject.activeSelf ? "True" : "False");
        builder.Append(" originalActive=");
        builder.Append(original.gameObject.activeInHierarchy ? "True" : "False");
        builder.Append(" originalChars=");
        builder.Append(original.textInfo.characterCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" originalPages=");
        builder.Append(original.textInfo.pageCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(" originalText='");
        builder.Append(original.text);
        builder.Append('\'');
        logLine = builder.ToString();
        return true;
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

    private static string BuildAbsolutePath(Transform target)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        var current = target;
        while (current is not null)
        {
            stack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", stack.ToArray());
    }

    private static string? FindOwningInventoryLineName(Transform target)
    {
        var current = target;
        while (current is not null)
        {
            var components = current.GetComponents<Component>();
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index];
                if (component is not null
                    && string.Equals(component.GetType().FullName, "Qud.UI.InventoryLine", StringComparison.Ordinal))
                {
                    return current.gameObject.name;
                }
            }

            current = current.parent;
        }

        return null;
    }
#else
    internal static int TryRenderReplacementTexts(object? componentInstance, out string? logLine)
    {
        _ = componentInstance;
        logLine = null;
        return 0;
    }
#endif
}
