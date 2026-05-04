using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CreditsMenuBarsTranslationPatch
{
    private const string Context = nameof(CreditsMenuBarsTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("Qud.UI.Credits");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.Credits type. Patch will not apply.");
            yield break;
        }

        var method = AccessTools.Method(targetType, "UpdateMenuBars");
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: Failed to resolve Qud.UI.Credits.UpdateMenuBars(). Patch will not apply.");
    }

    public static void Postfix(object __instance)
    {
        try
        {
            TranslateHotkeyBarChoices(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CreditsMenuBarsTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateHotkeyBarChoices(object instance)
    {
        var hotkeyBar = AccessTools.Field(instance.GetType(), "hotkeyBar")?.GetValue(instance);
        if (hotkeyBar is null)
        {
            return;
        }

        var choices = AccessTools.Field(hotkeyBar.GetType(), "choices")?.GetValue(hotkeyBar);
        if (TranslateCollectionField(choices)
            && choices is IEnumerable hotkeyChoices)
        {
            InvokeBeforeShow(hotkeyBar, hotkeyChoices);
        }
    }

    private static bool TranslateCollectionField(object? maybeCollection)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable collection)
        {
            return false;
        }

        var changed = false;
        foreach (var item in collection)
        {
            changed |= TranslateDescriptionField(item);
        }

        return changed;
    }

    private static bool TranslateDescriptionField(object? item)
    {
        if (item is null)
        {
            return false;
        }

        var field = AccessTools.Field(item.GetType(), "Description");
        if (field is null || field.FieldType != typeof(string))
        {
            return false;
        }

        var current = field.GetValue(item) as string;
        if (string.IsNullOrEmpty(current))
        {
            return false;
        }

        var translated = MainMenuLocalizationPatch.TranslateProducerText(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "Credits.HotkeyBar", current, translated);
        field.SetValue(item, translated);
        return true;
    }

    private static void InvokeBeforeShow(object hotkeyBar, IEnumerable choices)
    {
        var beforeShow = AccessTools.Method(hotkeyBar.GetType(), "BeforeShow");
        if (beforeShow is null)
        {
            return;
        }

        var parameterCount = beforeShow.GetParameters().Length;
        if (parameterCount == 0)
        {
            _ = beforeShow.Invoke(hotkeyBar, null);
            return;
        }

        if (parameterCount == 1)
        {
            _ = beforeShow.Invoke(hotkeyBar, new object?[] { choices });
            return;
        }

        _ = beforeShow.Invoke(hotkeyBar, new object?[] { null, choices });
    }
}
