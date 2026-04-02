using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HelpScreenTranslationPatch
{
    private const string Context = nameof(HelpScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.HelpScreen", "HelpScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: HelpScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateMenuBars", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: HelpScreenTranslationPatch.UpdateMenuBars() not found.");
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

            if (UiBindingTranslationHelpers.GetMemberValue(__instance, "keyMenuOptions") is not IEnumerable menuOptions)
            {
                return;
            }

            var changed = TranslateMenuOptions(menuOptions);
            if (!changed)
            {
                return;
            }

            var hotkeyBar = UiBindingTranslationHelpers.GetMemberValue(__instance, "hotkeyBar");
            if (hotkeyBar is not null)
            {
                InvokeBeforeShow(hotkeyBar, menuOptions);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: HelpScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static bool TranslateMenuOptions(IEnumerable menuOptions)
    {
        var changed = false;
        var index = 0;
        foreach (var option in menuOptions)
        {
            if (option is null)
            {
                index++;
                continue;
            }

            var current = UiBindingTranslationHelpers.GetStringMemberValue(option, "Description");
            if (string.IsNullOrEmpty(current))
            {
                index++;
                continue;
            }

            var route = ObservabilityHelpers.ComposeContext(Context, "keyMenuOptions[" + index + "].Description");
            var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, "HelpScreen.MenuOption");
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                UiBindingTranslationHelpers.SetMemberValue(option, "Description", translated);
                changed = true;
            }

            index++;
        }

        return changed;
    }

    private static void InvokeBeforeShow(object hotkeyBar, IEnumerable menuOptions)
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
            _ = beforeShow.Invoke(hotkeyBar, new object?[] { menuOptions });
            return;
        }

        _ = beforeShow.Invoke(hotkeyBar, new object?[] { null, menuOptions });
    }
}
