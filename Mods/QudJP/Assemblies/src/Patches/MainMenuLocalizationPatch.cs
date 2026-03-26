using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MainMenuLocalizationPatch
{
    private const string Context = nameof(MainMenuLocalizationPatch);
    private const string TargetTypeName = "Qud.UI.MainMenu";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.MainMenu type. Patch will not apply.");
            yield break;
        }

        var showMethod = AccessTools.Method(targetType, "Show");
        if (showMethod is not null)
        {
            yield return showMethod;
        }
        else
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.MainMenu.Show(). Patch will not apply.");
        }

        var updateMenuBarsMethod = AccessTools.Method(targetType, "UpdateMenuBars");
        if (updateMenuBarsMethod is not null)
        {
            yield return updateMenuBarsMethod;
        }
        else
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.MainMenu.UpdateMenuBars(). Patch will not apply.");
        }
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
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(
                leftOptions,
                ObservabilityHelpers.ComposeContext(nameof(MainMenuLocalizationPatch), "collection=LeftOptions"),
                "Text");

            var rightOptions = AccessCollectionField(targetType, __instance, "RightOptions");
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(
                rightOptions,
                ObservabilityHelpers.ComposeContext(Context, "collection=RightOptions"),
                "Text");

            TranslateHotkeyBarChoices(targetType, __instance);
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

    private static void TranslateHotkeyBarChoices(Type targetType, object? instance)
    {
        if (instance is null)
        {
            return;
        }

        var hotkeyBar = AccessTools.Field(targetType, "hotkeyBar")?.GetValue(instance);
        if (hotkeyBar is null)
        {
            return;
        }

        UITextSkinTranslationPatch.TranslateStringFieldsInCollection(
            AccessTools.Field(hotkeyBar.GetType(), "choices")?.GetValue(hotkeyBar),
            ObservabilityHelpers.ComposeContext(Context, "collection=HotkeyBarChoices"),
            "Description");
    }
}
