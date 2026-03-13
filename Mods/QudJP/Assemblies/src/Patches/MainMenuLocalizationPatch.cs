using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MainMenuLocalizationPatch
{
    private const string TargetTypeName = "Qud.UI.MainMenu";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(TargetTypeName + ":Show");
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.MainMenu.Show(). Patch will not apply.");
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            var targetType = __instance?.GetType();
            if (targetType is null)
            {
                Trace.TraceWarning("QudJP: MainMenuLocalizationPatch __instance is null, resolving type by name.");
                targetType = AccessTools.TypeByName(TargetTypeName);
            }

            if (targetType is null)
            {
                Trace.TraceError("QudJP: MainMenuLocalizationPatch target type is null. Skipping translation.");
                return;
            }

            var leftOptions = AccessCollectionField(targetType, __instance, "LeftOptions");
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(leftOptions, nameof(MainMenuLocalizationPatch), "Text");

            var rightOptions = AccessCollectionField(targetType, __instance, "RightOptions");
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(rightOptions, nameof(MainMenuLocalizationPatch), "Text");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MainMenuLocalizationPatch.Postfix failed: {0}", ex);
        }
    }

    private static object? AccessCollectionField(Type targetType, object? instance, string fieldName)
    {
        var field = AccessTools.Field(targetType, fieldName);
        if (field is null)
        {
            Trace.TraceWarning("QudJP: Field '{0}' not found on type '{1}'.", fieldName, targetType.FullName);
            return null;
        }

        if (field.IsStatic)
        {
            return field.GetValue(null);
        }

        if (instance is null)
        {
            Trace.TraceWarning("QudJP: Cannot access instance field '{0}' — instance is null.", fieldName);
            return null;
        }

        return field.GetValue(instance);
    }
}
