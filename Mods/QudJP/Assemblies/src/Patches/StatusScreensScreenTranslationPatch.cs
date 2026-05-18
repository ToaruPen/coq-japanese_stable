using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StatusScreensScreenTranslationPatch
{
    private const string Context = nameof(StatusScreensScreenTranslationPatch);
    private const string PageLeftCommand = "Page Left";
    private const string PageRightCommand = "Page Right";
    private const string PreviousTabDescription = "Previous tab";
    private const string NextTabDescription = "Next tab";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.StatusScreensScreen", "StatusScreensScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: StatusScreensScreenTranslationPatch target type not found.");
            yield break;
        }

        foreach (var methodName in new[] { "UpdateViewFromData", "UpdateActiveScreen" })
        {
            var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
            if (method is null)
            {
                Trace.TraceError("QudJP: StatusScreensScreenTranslationPatch.{0} not found.", methodName);
                continue;
            }

            yield return method;
        }
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
            EnsurePageNavigationMenuOptions(__instance);
            TranslateContextMenuOptionList(__instance, "screenGlobalContext", "menuOptionDescriptions");
            LogMenuOptionsProbe(__instance);
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

    private static void EnsurePageNavigationMenuOptions(object instance)
    {
        var context = UiBindingTranslationHelpers.GetMemberValue(instance, "screenGlobalContext");
        if (context is null)
        {
            return;
        }

        var options = UiBindingTranslationHelpers.GetMemberValue(context, "menuOptionDescriptions");
        if (options is null)
        {
            options = CreateMenuOptionList(context, "menuOptionDescriptions");
            if (options is null)
            {
                return;
            }

            UiBindingTranslationHelpers.SetMemberValue(context, "menuOptionDescriptions", options);
        }

        if (options is not IList list)
        {
            return;
        }

        AddMenuOptionIfMissing(list, PageLeftCommand, PreviousTabDescription);
        AddMenuOptionIfMissing(list, PageRightCommand, NextTabDescription);
    }

    private static object? CreateMenuOptionList(object context, string memberName)
    {
        var memberType = GetMemberType(context.GetType(), memberName);
        var optionType = GetCollectionElementType(memberType);
        if (optionType is null)
        {
            optionType = GameTypeResolver.FindType("XRL.UI.Framework.MenuOption", "MenuOption");
        }
        if (optionType is null)
        {
            return null;
        }

        return Activator.CreateInstance(typeof(List<>).MakeGenericType(optionType));
    }

    private static Type? GetMemberType(Type type, string memberName)
    {
        var property = AccessTools.Property(type, memberName);
        if (property is not null)
        {
            return property.PropertyType;
        }

        return AccessTools.Field(type, memberName)?.FieldType;
    }

    private static Type? GetCollectionElementType(Type? collectionType)
    {
        if (collectionType is null)
        {
            return null;
        }

        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType && collectionType.GetGenericArguments().Length == 1)
        {
            return collectionType.GetGenericArguments()[0];
        }

        foreach (var interfaceType in collectionType.GetInterfaces())
        {
            if (interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return interfaceType.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static void AddMenuOptionIfMissing(IList options, string inputCommand, string description)
    {
        foreach (var option in options)
        {
            if (option is not null
                && string.Equals(
                    UiBindingTranslationHelpers.GetStringMemberValue(option, "InputCommand"),
                    inputCommand,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        var optionType = GetCollectionElementType(options.GetType());
        if (optionType is null)
        {
            optionType = GameTypeResolver.FindType("XRL.UI.Framework.MenuOption", "MenuOption");
        }
        if (optionType is null)
        {
            return;
        }

        var newOption = Activator.CreateInstance(optionType);
        if (newOption is null)
        {
            return;
        }

        UiBindingTranslationHelpers.SetMemberValue(newOption, "InputCommand", inputCommand);
        UiBindingTranslationHelpers.SetMemberValue(newOption, "Description", description);
        options.Add(newOption);
    }

    private static void TranslateContextMenuOptionList(object instance, string contextMemberName, string optionListMemberName)
    {
        var context = UiBindingTranslationHelpers.GetMemberValue(instance, contextMemberName);
        if (context is null)
        {
            return;
        }

        if (UiBindingTranslationHelpers.GetMemberValue(context, optionListMemberName) is not IEnumerable options)
        {
            return;
        }

        var index = 0;
        foreach (var option in options)
        {
            TranslateMenuOption(option, contextMemberName + "." + optionListMemberName + "[" + index + "]");
            index++;
        }
    }

    private static void LogMenuOptionsProbe(object instance)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
        {
            var defaultOptions = FormatMenuOptionCollection(
                UiBindingTranslationHelpers.GetMemberValue(instance, "defaultMenuOptionOrder"));
            var context = UiBindingTranslationHelpers.GetMemberValue(instance, "screenGlobalContext");
            var screenGlobalOptions = context is null
                ? "<no-context>"
                : FormatMenuOptionCollection(UiBindingTranslationHelpers.GetMemberValue(context, "menuOptionDescriptions"));

            return "[QudJP] StatusScreensMenuOptionsProbe/v1: default="
                + defaultOptions
                + " screenGlobal="
                + screenGlobalOptions;
        });
    }

    private static string FormatMenuOptionCollection(object? maybeOptions)
    {
        if (maybeOptions is not IEnumerable options)
        {
            return "<none>";
        }

        var parts = new List<string>();
        foreach (var option in options)
        {
            if (option is null)
            {
                continue;
            }

            parts.Add("{cmd="
                + ProbeValue(UiBindingTranslationHelpers.GetStringMemberValue(option, "InputCommand"))
                + ",desc="
                + ProbeValue(UiBindingTranslationHelpers.GetStringMemberValue(option, "Description"))
                + ",key="
                + ProbeValue(UiBindingTranslationHelpers.GetStringMemberValue(option, "KeyDescription"))
                + "}");
        }

        return parts.Count == 0 ? "[]" : string.Join(",", parts);
    }

    private static string ProbeValue(string? value)
    {
        if (value is null)
        {
            return "'<null>'";
        }

        return "'" + value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("'", "\\'") + "'";
    }
}
