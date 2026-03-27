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

    public static void Postfix(
        object? __instance,
        object? element,
        object? ___mutationNameText,
        object? ___mutationRankText,
        object? ___mutationTypeText,
        object? ___mutationsDetails)
    {
        try
        {
            TranslateStaticMenuOption(__instance, "BUY_MUTATION");
            TranslateStaticMenuOption(__instance, "SHOW_EFFECTS");

            if (element is null)
            {
                return;
            }

            var mutation = GetMemberValue(element, "mutation");
            if (mutation is null)
            {
                return;
            }

            TranslateUiTextField(___mutationNameText, "mutationNameText");
            TranslateUiTextField(___mutationRankText, "mutationRankText");
            TranslateUiTextField(___mutationTypeText, "mutationTypeText");
            TranslateMutationDetailsField(mutation, ___mutationsDetails);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CharacterStatusScreenMutationDetailsPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateUiTextField(object? uiTextSkin, string fieldName)
    {
        var currentText = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(CharacterStatusScreenMutationDetailsPatch));
        if (string.IsNullOrEmpty(currentText))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(nameof(CharacterStatusScreenTranslationPatch), "field=" + fieldName);
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            currentText!,
            visible => CharacterStatusScreenTextTranslator.TryTranslateUiText(visible, route, out var candidate)
                ? candidate
                : visible);
        if (string.Equals(translated, currentText, StringComparison.Ordinal))
        {
            return;
        }

        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(CharacterStatusScreenMutationDetailsPatch));
    }

    private static void TranslateMutationDetailsField(object mutation, object? uiTextSkin)
    {
        var currentText = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(CharacterStatusScreenMutationDetailsPatch));
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

        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(CharacterStatusScreenMutationDetailsPatch));
    }

    private static void TranslateStaticMenuOption(object? instance, string fieldName)
    {
        if (instance is null)
        {
            return;
        }

        var field = AccessTools.Field(instance.GetType(), fieldName);
        var menuOption = field?.GetValue(null);
        if (menuOption is null)
        {
            return;
        }

        TranslateMenuOptionMember(menuOption, "Description", fieldName + ".Description");
        TranslateMenuOptionMember(menuOption, "KeyDescription", fieldName + ".KeyDescription");
    }

    private static void TranslateMenuOptionMember(object menuOption, string memberName, string routeSuffix)
    {
        var currentText = GetMemberValue(menuOption, memberName) as string;
        if (string.IsNullOrEmpty(currentText))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(nameof(CharacterStatusScreenTranslationPatch), "field=" + routeSuffix);
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            currentText!,
            visible => CharacterStatusScreenTextTranslator.TryTranslateUiText(visible, route, out var candidate)
                ? candidate
                : visible);
        if (string.Equals(translated, currentText, StringComparison.Ordinal))
        {
            return;
        }

        SetMemberValue(menuOption, memberName, translated);
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

    private static void SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = AccessTools.Field(type, memberName);
        field?.SetValue(instance, value);
    }
}
