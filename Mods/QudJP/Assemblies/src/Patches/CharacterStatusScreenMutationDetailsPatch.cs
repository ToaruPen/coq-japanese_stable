using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CharacterStatusScreenMutationDetailsPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.CharacterStatusScreen", "CharacterStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenMutationDetailsPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "HandleHighlightMutation", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenMutationDetailsPatch.HandleHighlightMutation not found.");
        }

        return method;
    }

    public static void Postfix(object? element, object? ___mutationsDetails)
    {
        try
        {
            if (element is null || ___mutationsDetails is null)
            {
                return;
            }

            var mutation = GetMemberValue(element, "mutation");
            if (mutation is null)
            {
                return;
            }

            var currentText = GetCurrentText(___mutationsDetails);
            if (string.IsNullOrEmpty(currentText))
            {
                return;
            }

            if (!CharacterStatusScreenTextTranslator.TryTranslateMutationDetails(
                    mutation,
                    currentText!,
                    ObservabilityHelpers.ComposeContext(nameof(CharacterStatusScreenTranslationPatch), "field=mutationsDetails"),
                    out var translated))
            {
                return;
            }

            SetCurrentText(___mutationsDetails, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenMutationDetailsPatch.Postfix failed: {0}", ex);
        }
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
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
            Trace.TraceWarning(
                "QudJP: CharacterStatusScreenMutationDetailsPatch.GetCurrentText falling back to property 'text' for {0}.",
                uiTextSkin.GetType().FullName);
            textProperty = AccessTools.Property(uiTextSkin.GetType(), "text");
        }

        return textProperty is not null && textProperty.CanRead && textProperty.PropertyType == typeof(string)
            ? textProperty.GetValue(uiTextSkin) as string
            : null;
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
            Trace.TraceWarning(
                "QudJP: CharacterStatusScreenMutationDetailsPatch.SetCurrentText falling back to property 'text' for {0}.",
                uiTextSkin.GetType().FullName);
            textProperty = AccessTools.Property(uiTextSkin.GetType(), "text");
        }

        if (textProperty is not null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
        {
            textProperty.SetValue(uiTextSkin, translated);
        }
    }
}
