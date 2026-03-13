using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class OptionsLocalizationPatch
{
    private const string TargetTypeName = "Qud.UI.OptionsScreen";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(TargetTypeName + ":Show");
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.OptionsScreen.Show(). Patch will not apply.");
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            if (__instance is null)
            {
                Trace.TraceError("QudJP: OptionsLocalizationPatch.Postfix received null __instance. Skipping translation.");
                return;
            }

            var type = __instance.GetType();

            var menuItemsField = AccessTools.Field(type, "menuItems");
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(menuItemsField?.GetValue(__instance), nameof(OptionsLocalizationPatch), "Title", "HelpText");

            var filteredMenuItemsField = AccessTools.Field(type, "filteredMenuItems");
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(filteredMenuItemsField?.GetValue(__instance), nameof(OptionsLocalizationPatch), "Title", "HelpText");

            var defaultMenuOptionsField = AccessTools.Field(type, "defaultMenuOptions");
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(defaultMenuOptionsField?.GetValue(null), nameof(OptionsLocalizationPatch), "Description");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: OptionsLocalizationPatch.Postfix failed: {0}", ex);
        }
    }
}
