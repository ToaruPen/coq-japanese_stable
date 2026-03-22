using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PickGameObjectScreenTranslationPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.PickGameObjectScreen", "PickGameObjectScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: PickGameObjectScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", new[] { typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: PickGameObjectScreenTranslationPatch.UpdateViewFromData(bool) not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var type = __instance.GetType();
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(
                AccessTools.Field(type, "defaultMenuOptions")?.GetValue(__instance),
                nameof(PickGameObjectScreenTranslationPatch),
                "Description");
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(
                AccessTools.Field(type, "getItemMenuOptions")?.GetValue(__instance),
                nameof(PickGameObjectScreenTranslationPatch),
                "Description");
            UITextSkinTranslationPatch.TranslateStringField(
                AccessTools.Field(type, "TAKE_ALL")?.GetValue(__instance),
                "Description",
                nameof(PickGameObjectScreenTranslationPatch));
            UITextSkinTranslationPatch.TranslateStringField(
                AccessTools.Field(type, "STORE_ITEM")?.GetValue(__instance),
                "Description",
                nameof(PickGameObjectScreenTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PickGameObjectScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
