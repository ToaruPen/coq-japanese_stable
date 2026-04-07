using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class UITextSkinTemplateTranslator
{
    internal static void TranslateSinglePlaceholderText(
        object? uiTextSkin,
        Regex pattern,
        string templateKey,
        string placeholderToken,
        string context)
    {
        if (uiTextSkin is null)
        {
            Trace.TraceWarning("QudJP: [{0}] uiTextSkin is null for templateKey '{1}'", context, templateKey);
            return;
        }

        var current = QudJP.Patches.UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(UITextSkinTemplateTranslator));
        if (string.IsNullOrEmpty(current))
        {
            Trace.TraceWarning(
                "QudJP: [{0}] GetCurrentText returned null/empty for {1}, templateKey '{2}'",
                context,
                uiTextSkin.GetType().Name,
                templateKey);
            return;
        }

        var match = pattern.Match(current);
        if (!match.Success)
        {
            return;
        }

        using var _ = Translator.PushLogContext(context);
        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            Trace.TraceWarning(
                "QudJP: [{0}] Untranslated template '{1}' for {2}",
                context,
                templateKey,
                uiTextSkin.GetType().Name);
            return;
        }

        var translated = translatedTemplate.Replace(placeholderToken, match.Groups["rest"].Value);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(
            ObservabilityHelpers.ExtractPrimaryContext(context),
            templateKey,
            current,
            translated);
        QudJP.Patches.UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(UITextSkinTemplateTranslator));
    }
}
