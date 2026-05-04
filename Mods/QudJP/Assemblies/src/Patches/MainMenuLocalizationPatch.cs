using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
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
            TranslateCollectionField(leftOptions, "Text", "MainMenu.LeftOptions");

            var rightOptions = AccessCollectionField(targetType, __instance, "RightOptions");
            TranslateCollectionField(rightOptions, "Text", "MainMenu.RightOptions");

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

        var choices = AccessTools.Field(hotkeyBar.GetType(), "choices")?.GetValue(hotkeyBar);
        if (TranslateCollectionField(
            choices,
            "Description",
            "MainMenu.HotkeyBar")
            && choices is IEnumerable hotkeyChoices)
        {
            InvokeBeforeShow(hotkeyBar, hotkeyChoices);
        }
    }

    private static bool TranslateCollectionField(object? maybeCollection, string fieldName, string family)
    {
        if (maybeCollection is null || string.IsNullOrEmpty(fieldName) || maybeCollection is string)
        {
            return false;
        }

        if (maybeCollection is not IEnumerable collection)
        {
            return false;
        }

        var changed = false;
        foreach (var item in collection)
        {
            changed |= TranslateField(item, fieldName, family);
        }

        return changed;
    }

    private static bool TranslateField(object? item, string fieldName, string family)
    {
        if (item is null)
        {
            return false;
        }

        var field = AccessTools.Field(item.GetType(), fieldName);
        if (field is null || field.FieldType != typeof(string))
        {
            return false;
        }

        var current = field.GetValue(item) as string;
        if (string.IsNullOrEmpty(current))
        {
            return false;
        }

        var translated = TranslateProducerText(current!);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, family, current, translated);
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

    internal static string TranslateProducerText(string source)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0
            || UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(stripped, Context))
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => TryTranslateVisibleText(visible, out var translated)
                ? translated
                : visible);
    }

    private static bool TryTranslateVisibleText(string visible, out string translated)
    {
        if (string.Equals(visible, "Mod", StringComparison.Ordinal))
        {
            translated = visible;
            return true;
        }

        return StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out translated);
    }
}
