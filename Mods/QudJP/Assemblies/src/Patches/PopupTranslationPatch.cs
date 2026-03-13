using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupTranslationPatch
{
    private const string TargetTypeName = "XRL.UI.Popup";

    // ShowBlock parameter count for game version 2.0.4
    private const int ShowBlockParameterCount = 8;

    // ShowOptionList parameter count for game version 2.0.4
    private const int ShowOptionListParameterCount = 19;

    // ShowOptionList argument indices (game version 2.0.4)
    private const int ShowOptionListIntroIndex = 4;
    private const int ShowOptionListSpacingTextIndex = 9;
    private const int ShowOptionListButtonsIndex = 14;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var showBlock = FindMethod(methodName: "ShowBlock", parameterCount: ShowBlockParameterCount);
        if (showBlock is not null)
        {
            yield return showBlock;
        }

        var showOptionList = FindMethod(methodName: "ShowOptionList", parameterCount: ShowOptionListParameterCount);
        if (showOptionList is not null)
        {
            yield return showOptionList;
        }
    }

    public static void Prefix(MethodBase __originalMethod, object[] __args)
    {
        try
        {
            if (__originalMethod is null || __args is null)
            {
                Trace.TraceError("QudJP: PopupTranslationPatch.Prefix received null originalMethod or args.");
                return;
            }

            if (__originalMethod.Name == "ShowBlock")
            {
                TranslateShowBlockArgs(__args);
                return;
            }

            if (__originalMethod.Name == "ShowOptionList")
            {
                TranslateShowOptionListArgs(__args);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PopupTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void TranslateShowBlockArgs(object[] args)
    {
        TranslateStringArg(args, index: 0);
        TranslateStringArg(args, index: 1);
    }

    private static void TranslateShowOptionListArgs(object[] args)
    {
        TranslateStringArg(args, index: 0);
        TranslateStringListArg(args, index: 1);
        TranslateStringArg(args, index: ShowOptionListIntroIndex);
        TranslateStringArg(args, index: ShowOptionListSpacingTextIndex);

        if (args.Length > ShowOptionListButtonsIndex)
        {
            UITextSkinTranslationPatch.TranslateStringFieldsInCollection(args[ShowOptionListButtonsIndex], nameof(PopupTranslationPatch), "text");
        }
    }

    private static void TranslateStringArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            return;
        }

        if (args[index] is string text)
        {
            args[index] = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(PopupTranslationPatch));
        }
    }

    private static void TranslateStringListArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            return;
        }

        if (args[index] is null || args[index] is string || args[index] is not IEnumerable enumerable)
        {
            return;
        }

        var translated = new List<string>();
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                translated.Add(string.Empty);
                continue;
            }

            if (item is not string text)
            {
                return;
            }

            translated.Add(UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(PopupTranslationPatch)));
        }

        args[index] = translated;
    }

    private static MethodBase? FindMethod(string methodName, int parameterCount)
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: PopupTranslationPatch target type '{TargetTypeName}' not found.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];
            if (method.Name == methodName && method.GetParameters().Length == parameterCount)
            {
                return method;
            }
        }

        Trace.TraceError(
            $"QudJP: PopupTranslationPatch method '{methodName}' with {parameterCount} params not found on '{TargetTypeName}'.");
        return null;
    }
}
