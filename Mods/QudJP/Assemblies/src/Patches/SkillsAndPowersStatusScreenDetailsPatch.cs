using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SkillsAndPowersStatusScreenDetailsPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.SkillsAndPowersStatusScreen", "SkillsAndPowersStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: SkillsAndPowersStatusScreenDetailsPatch target type not found.");
            return null;
        }

        var nodeType = GameTypeResolver.FindType("XRL.UI.SPNode", "SPNode");
        var method = nodeType is null
            ? null
            : AccessTools.Method(targetType, "UpdateDetailsFromNode", new[] { nodeType });
        if (method is null)
        {
            Trace.TraceError("QudJP: SkillsAndPowersStatusScreenDetailsPatch.UpdateDetailsFromNode not found.");
        }

        return method;
    }

    public static void Postfix(
        object? ___detailsText,
        object? ___skillNameText,
        object? ___learnedText,
        object? ___requirementsText,
        object? ___requiredSkillsText,
        object? ___requiredSkillsHeader)
    {
        try
        {
            TranslateTextField(___detailsText, "detailsText", SkillsAndPowersStatusScreenTranslationPatch.TryTranslateDetailText);
            TranslateTextField(___skillNameText, "skillNameText", SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors);
            TranslateTextField(___learnedText, "learnedText", SkillsAndPowersStatusScreenTranslationPatch.TryTranslateLearnedStatusText);
            TranslateTextField(___requirementsText, "requirementsText", SkillsAndPowersStatusScreenTranslationPatch.TryTranslateRequirementsOwnerText);
            TranslateTextField(___requiredSkillsText, "requiredSkillsText", SkillsAndPowersStatusScreenTranslationPatch.TryTranslateRequiredSkillsOwnerText);
            TranslateTextField(___requiredSkillsHeader, "requiredSkillsHeader", SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors);
            TranslateGameObjectChildText(___requiredSkillsHeader, "requiredSkillsHeader", SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SkillsAndPowersStatusScreenDetailsPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateTextField(
        object? uiTextSkin,
        string fieldName,
        Func<string, string, bool, (bool changed, string translated)> translator)
    {
        var currentText = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, nameof(SkillsAndPowersStatusScreenDetailsPatch));
        if (string.IsNullOrEmpty(currentText))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(nameof(SkillsAndPowersStatusScreenDetailsPatch), "field=" + fieldName);
        var (changed, translated) = translator(currentText!, route, true);
        if (!changed)
        {
            return;
        }

        _ = UITextSkinReflectionAccessor.SetCurrentText(uiTextSkin, translated, nameof(SkillsAndPowersStatusScreenDetailsPatch));
    }

    private static void TranslateGameObjectChildText(
        object? value,
        string fieldName,
        Func<string, string, bool, (bool changed, string translated)> translator)
    {
        var gameObject = value switch
        {
            GameObject direct => direct,
            Component component => component.gameObject,
            _ => null,
        };
        if (gameObject is null)
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(nameof(SkillsAndPowersStatusScreenDetailsPatch), "field=" + fieldName + ".childText");
        var texts = gameObject.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (var index = 0; index < texts.Length; index++)
        {
            var text = texts[index];
            if (text is null || string.IsNullOrEmpty(text.text))
            {
                continue;
            }

            var (changed, translated) = translator(text.text, route, true);
            if (changed)
            {
                text.text = translated;
            }
        }
    }
}
