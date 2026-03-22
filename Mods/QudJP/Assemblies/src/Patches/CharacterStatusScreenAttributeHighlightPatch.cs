using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharacterStatusScreenAttributeHighlightPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.CharacterStatusScreen", "CharacterStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenAttributeHighlightPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "HandleHighlightAttribute", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenAttributeHighlightPatch.HandleHighlightAttribute not found.");
        }

        return method;
    }

    public static void Postfix(
        object? ___primaryAttributesDetails,
        object? ___secondaryAttributesDetails,
        object? ___resistanceAttributesDetails)
    {
        try
        {
            TranslateField(___primaryAttributesDetails, "primaryAttributesDetails");
            TranslateField(___secondaryAttributesDetails, "secondaryAttributesDetails");
            TranslateField(___resistanceAttributesDetails, "resistanceAttributesDetails");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenAttributeHighlightPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateField(object? uiTextSkin, string fieldName)
    {
        var currentText = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(CharacterStatusScreenAttributeHighlightPatch));
        if (string.IsNullOrEmpty(currentText))
        {
            return;
        }

        if (Translator.TryGetTranslation(currentText!, out var translated)
            && !string.Equals(translated, currentText, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(
                nameof(CharacterStatusScreenAttributeHighlightPatch),
                "AttributeHighlight.Exact",
                currentText,
                translated);
            _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(CharacterStatusScreenAttributeHighlightPatch));
            return;
        }

        if (!CharacterStatusScreenTextTranslator.TryTranslateUiText(
                currentText!,
                ObservabilityHelpers.ComposeContext(nameof(CharacterStatusScreenAttributeHighlightPatch), "field=" + fieldName),
                out translated))
        {
            return;
        }

        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(CharacterStatusScreenAttributeHighlightPatch));
    }
}
