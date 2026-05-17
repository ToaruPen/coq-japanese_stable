using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
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
            EnsurePageTabMenuOptions(__instance);
            TranslateMenuOptionList(__instance, "defaultMenuOptionOrder");
            UiBindingTranslationHelpers.SetMemberValue(__instance, "updateMenuBar", true);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: StatusScreensScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void EnsurePageTabMenuOptions(object instance)
    {
        if (UiBindingTranslationHelpers.GetMemberValue(instance, "defaultMenuOptionOrder") is not IList options)
        {
            return;
        }

        EnsureMenuOption(options, "Page Left", "Previous tab");
        EnsureMenuOption(options, "Page Right", "Next tab");
    }

    private static void EnsureMenuOption(IList options, string inputCommand, string description)
    {
        if (options.Cast<object>().Any(option => string.Equals(
            UiBindingTranslationHelpers.GetStringMemberValue(option, "InputCommand"),
            inputCommand,
            StringComparison.Ordinal)))
        {
            return;
        }

        var optionType = options.Count > 0 ? options[0]!.GetType() : null;
        if (optionType is null)
        {
            return;
        }

        var option = CreateMenuOption(optionType, description, inputCommand);
        if (option is not null)
        {
            options.Add(option);
        }
    }

    private static object? CreateMenuOption(Type optionType, string description, string inputCommand)
    {
        object? option;
        var constructor = AccessTools.Constructor(optionType, new[] { typeof(string), typeof(string), typeof(string) });
        if (constructor is not null)
        {
            option = constructor.Invoke(new object?[] { description, inputCommand, null });
        }
        else
        {
            option = Activator.CreateInstance(optionType);
        }

        if (option is null)
        {
            return null;
        }

        UiBindingTranslationHelpers.SetMemberValue(option, "Description", description);
        UiBindingTranslationHelpers.SetMemberValue(option, "InputCommand", inputCommand);
        return option;
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
