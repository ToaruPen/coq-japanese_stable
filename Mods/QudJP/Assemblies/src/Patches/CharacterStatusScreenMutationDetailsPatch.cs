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

            var currentText = UITextSkinReflectionAccessor.GetCurrentText(___mutationsDetails, nameof(CharacterStatusScreenMutationDetailsPatch));
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

            _ = UITextSkinReflectionAccessor.SetCurrentText(___mutationsDetails, translated, nameof(CharacterStatusScreenMutationDetailsPatch));
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
}
