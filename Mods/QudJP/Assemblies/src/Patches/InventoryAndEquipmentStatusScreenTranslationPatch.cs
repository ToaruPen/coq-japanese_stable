using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryAndEquipmentStatusScreenTranslationPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.InventoryAndEquipmentStatusScreen", "InventoryAndEquipmentStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenTranslationPatch.UpdateViewFromData() not found.");
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

            TranslateMenuOptionDescription(__instance, "CMD_OPTIONS");
            TranslateMenuOptionDescription(__instance, "SET_PRIMARY_LIMB");
            TranslateMenuOptionDescription(__instance, "SHOW_TOOLTIP");
            TranslateMenuOptionDescription(__instance, "QUICK_DROP");
            TranslateMenuOptionDescription(__instance, "QUICK_EAT");
            TranslateMenuOptionDescription(__instance, "QUICK_DRINK");
            TranslateMenuOptionDescription(__instance, "QUICK_APPLY");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateMenuOptionDescription(object instance, string fieldName)
    {
        var menuOption = AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance);
        if (menuOption is null)
        {
            return;
        }

        var descriptionField = AccessTools.Field(menuOption.GetType(), "Description");
        if (descriptionField is null || descriptionField.FieldType != typeof(string))
        {
            return;
        }

        var current = descriptionField.GetValue(menuOption) as string;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var sourceForTranslation = current!;
        var inputCommand = AccessTools.Field(menuOption.GetType(), "InputCommand")?.GetValue(menuOption) as string;
        if (!string.IsNullOrEmpty(inputCommand))
        {
            InventoryAndEquipmentStatusScreenTextTranslator.TryStripEmbeddedHotkeyLabel(sourceForTranslation, out sourceForTranslation);
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(sourceForTranslation);

        if (!string.Equals(translated, sourceForTranslation, StringComparison.Ordinal))
        {
            descriptionField.SetValue(menuOption, translated);
        }
    }
}
