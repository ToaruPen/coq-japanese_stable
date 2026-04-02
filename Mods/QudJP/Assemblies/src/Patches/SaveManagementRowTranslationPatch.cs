using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SaveManagementRowTranslationPatch
{
    private const string Context = nameof(SaveManagementRowTranslationPatch);
    private const string LastSavedPrefix = "{{C|Last saved:}} ";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("SaveManagementRow", "SaveManagementRow");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: SaveManagementRowTranslationPatch target type not found.");
            return null;
        }

        var frameworkDataElementType = GameTypeResolver.FindType("XRL.UI.Framework.FrameworkDataElement", "FrameworkDataElement");
        var method = frameworkDataElementType is null
            ? null
            : AccessTools.Method(targetType, "setData", new[] { frameworkDataElementType });
        if (method is null)
        {
            Trace.TraceError("QudJP: SaveManagementRowTranslationPatch.setData(FrameworkDataElement) not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null
                || UiBindingTranslationHelpers.GetMemberValue(__instance, "TextSkins") is not IList textSkins
                || textSkins.Count <= 2)
            {
                return;
            }

            TranslateLastSaved(textSkins[2]);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SaveManagementRowTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateLastSaved(object? uiTextSkin)
    {
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (current is null
            || current.Length == 0
            || !current.StartsWith(LastSavedPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var translatedLabel = Translator.Translate("Last saved:");
        if (string.Equals(translatedLabel, "Last saved:", StringComparison.Ordinal))
        {
            return;
        }

        var translated = "{{C|" + translatedLabel + "}} " + current!.Substring(LastSavedPrefix.Length);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, "SaveManagementRow.LastSaved", current, translated);
        OwnerTextSetter.SetTranslatedText(uiTextSkin, current, translated, Context, typeof(SaveManagementRowTranslationPatch));
    }
}
