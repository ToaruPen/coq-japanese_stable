using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StatusScreensScreenTranslationPatch
{
    private const string Context = nameof(StatusScreensScreenTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.StatusScreensScreen", "StatusScreensScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: StatusScreensScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: StatusScreensScreenTranslationPatch.UpdateViewFromData not found.");
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

            TranslateMenuOption(UiBindingTranslationHelpers.GetMemberValue(__instance, "SET_FILTER"), "SET_FILTER");
            TranslateMenuOptionList(__instance, "defaultMenuOptionOrder");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: StatusScreensScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateMenuOptionList(object instance, string memberName)
    {
        if (UiBindingTranslationHelpers.GetMemberValue(instance, memberName) is not IEnumerable options)
        {
            return;
        }

        var index = 0;
        foreach (var option in options)
        {
            TranslateMenuOption(option, memberName + "[" + index + "]");
            index++;
        }
    }

    private static void TranslateMenuOption(object? option, string routeSuffix)
    {
        if (option is null)
        {
            return;
        }

        TranslateMenuOptionMember(option, routeSuffix, "Description", "StatusScreensScreen.MenuOption");
        TranslateMenuOptionMember(option, routeSuffix, "KeyDescription", "StatusScreensScreen.MenuOption");
    }

    private static void TranslateMenuOptionMember(object option, string routeSuffix, string memberName, string family)
    {
        var current = UiBindingTranslationHelpers.GetStringMemberValue(option, memberName);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, routeSuffix + "." + memberName);
        var translated = UiBindingTranslationHelpers.TranslateVisibleText(current!, route, family);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            UiBindingTranslationHelpers.SetMemberValue(option, memberName, translated);
        }
    }
}
