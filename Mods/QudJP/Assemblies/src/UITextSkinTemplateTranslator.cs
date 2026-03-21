using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

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

        var current = GetCurrentText(uiTextSkin);
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
        SetCurrentText(uiTextSkin, translated);
    }

    private static string? GetCurrentText(object uiTextSkin)
    {
        var textField = AccessTools.Field(uiTextSkin.GetType(), "text");
        if (textField?.FieldType == typeof(string))
        {
            return textField.GetValue(uiTextSkin) as string;
        }

        var textProperty = AccessTools.Property(uiTextSkin.GetType(), "Text");
        if (textProperty is null)
        {
            textProperty = AccessTools.Property(uiTextSkin.GetType(), "text");
        }

        if (textProperty is not null && textProperty.CanRead && textProperty.PropertyType == typeof(string))
        {
            return textProperty.GetValue(uiTextSkin) as string;
        }

        return null;
    }

    private static void SetCurrentText(object uiTextSkin, string translated)
    {
        var setText = AccessTools.Method(uiTextSkin.GetType(), "SetText", new[] { typeof(string) });
        if (setText is not null)
        {
            _ = setText.Invoke(uiTextSkin, new object[] { translated });
            return;
        }

        var textField = AccessTools.Field(uiTextSkin.GetType(), "text");
        if (textField?.FieldType == typeof(string))
        {
            textField.SetValue(uiTextSkin, translated);
            return;
        }

        var textProperty = AccessTools.Property(uiTextSkin.GetType(), "Text");
        if (textProperty is null)
        {
            textProperty = AccessTools.Property(uiTextSkin.GetType(), "text");
        }

        if (textProperty is not null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
        {
            textProperty.SetValue(uiTextSkin, translated);
        }
    }
}
