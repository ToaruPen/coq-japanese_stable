using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbilityBarUpdateAbilitiesTextPatch
{
    private static readonly Regex AbilityPagePattern = new Regex(
        "^ABILITIES\\npage (?<current>\\d+) of (?<total>\\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.AbilityBar", "AbilityBar");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: AbilityBarUpdateAbilitiesTextPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateAbilitiesText", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: AbilityBarUpdateAbilitiesTextPatch.UpdateAbilitiesText not found.");
        }

        return method;
    }

    public static void Postfix(object? ___AbilityCommandText, object? ___CycleCommandText)
    {
        try
        {
            TranslateAbilityCommandText(___AbilityCommandText);
            TranslateCycleCommandText(___CycleCommandText);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AbilityBarUpdateAbilitiesTextPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateAbilityCommandText(object? uiTextSkin)
    {
        var currentText = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(AbilityBarUpdateAbilitiesTextPatch));
        if (string.IsNullOrEmpty(currentText))
        {
            return;
        }

        var match = AbilityPagePattern.Match(currentText);
        if (match.Success)
        {
            var translatedHeader = Translator.Translate("ABILITIES");
            var translatedPage = Translator.Translate("page {0} of {1}");
            if (string.Equals(translatedHeader, "ABILITIES", StringComparison.Ordinal)
                || string.Equals(translatedPage, "page {0} of {1}", StringComparison.Ordinal))
            {
                return;
            }

            var translated = translatedHeader
                + "\n"
                + translatedPage
                    .Replace("{0}", match.Groups["current"].Value)
                    .Replace("{1}", match.Groups["total"].Value);
            if (string.Equals(translated, currentText, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(
                nameof(AbilityBarUpdateAbilitiesTextPatch),
                "AbilityBar.AbilitiesCommand",
                currentText,
                translated);
            _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(AbilityBarUpdateAbilitiesTextPatch));
            return;
        }

        var translatedHeaderOnly = Translator.Translate("ABILITIES");
        if (string.Equals(translatedHeaderOnly, "ABILITIES", StringComparison.Ordinal)
            || string.Equals(translatedHeaderOnly, currentText, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(
            nameof(AbilityBarUpdateAbilitiesTextPatch),
            "AbilityBar.AbilitiesCommand",
            currentText,
            translatedHeaderOnly);
        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translatedHeaderOnly, nameof(AbilityBarUpdateAbilitiesTextPatch));
    }

    private static void TranslateCycleCommandText(object? uiTextSkin)
    {
        var currentText = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(AbilityBarUpdateAbilitiesTextPatch));
        if (string.IsNullOrEmpty(currentText))
        {
            return;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            currentText,
            static visible => StringHelpers.TranslateExactOrLowerAsciiFallback(visible));
        if (string.Equals(translated, currentText, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(
            nameof(AbilityBarUpdateAbilitiesTextPatch),
            "AbilityBar.CycleCommand",
            currentText,
            translated);
        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(AbilityBarUpdateAbilitiesTextPatch));
    }
}
