using System;
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
        return AccessTools.Method(TargetTypeName + ":Show");
    }

    public static void Postfix(object __instance)
    {
        var targetType = __instance?.GetType() ?? AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            return;
        }

        var leftOptions = AccessCollectionField(targetType, __instance, "LeftOptions");
        UITextSkinTranslationPatch.TranslateStringFieldsInCollection(leftOptions, "Text");

        var rightOptions = AccessCollectionField(targetType, __instance, "RightOptions");
        UITextSkinTranslationPatch.TranslateStringFieldsInCollection(rightOptions, "Text");
    }

    private static object? AccessCollectionField(Type targetType, object? instance, string fieldName)
    {
        var field = AccessTools.Field(targetType, fieldName);
        if (field is null)
        {
            return null;
        }

        if (field.IsStatic)
        {
            return field.GetValue(null);
        }

        if (instance is null)
        {
            return null;
        }

        return field.GetValue(instance);
    }
}
