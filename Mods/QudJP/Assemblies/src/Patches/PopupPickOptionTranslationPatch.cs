using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupPickOptionTranslationPatch
{
    private const string Context = nameof(PopupPickOptionTranslationPatch);
    private const string TargetTypeName = "XRL.UI.Popup";
    private const int TitleIndex = 0;
    private const int IntroIndex = 1;
    private const int SpacingTextIndex = 2;
    private const int OptionsIndex = 4;
    private const int ButtonsIndex = 7;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: {Context} target type '{TargetTypeName}' not found.");
            return null;
        }

        var renderableType = AccessTools.TypeByName("ConsoleLib.Console.IRenderable");
        var qudMenuItemType = AccessTools.TypeByName("Qud.UI.QudMenuItem");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var location2DType = AccessTools.TypeByName("Genkit.Location2D");

        MethodInfo? method = null;
        if (renderableType is not null
            && qudMenuItemType is not null
            && gameObjectType is not null
            && location2DType is not null)
        {
            method = AccessTools.Method(
                targetType,
                "PickOption",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(IReadOnlyList<string>),
                    typeof(IReadOnlyList<char>),
                    typeof(IReadOnlyList<>).MakeGenericType(renderableType),
                    typeof(IReadOnlyList<>).MakeGenericType(qudMenuItemType),
                    gameObjectType,
                    renderableType,
                    typeof(Action<int>),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    location2DType,
                    typeof(string),
                });
        }

        if (method is null)
        {
            Trace.TraceError("QudJP: PopupPickOptionTranslationPatch.PickOption() signature not found.");
            return null;
        }

        return method;
    }

    public static void Prefix(object[] __args)
    {
        try
        {
            if (__args is null)
            {
                Trace.TraceError($"QudJP: {Context}.Prefix received null args.");
                return;
            }

            TranslateStringArg(__args, TitleIndex);
            TranslateStringArg(__args, IntroIndex);
            TranslateStringArg(__args, SpacingTextIndex);
            TranslateStringListArg(__args, OptionsIndex);
            TranslatePopupMenuItemTextCollection(__args, ButtonsIndex);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static void TranslateStringArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length || args[index] is not string text)
        {
            return;
        }

        args[index] = PopupTranslationPatch.TranslatePopupTextForProducerRoute(text, Context);
    }

    private static void TranslateStringListArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length || args[index] is null || args[index] is string || args[index] is not IEnumerable enumerable)
        {
            return;
        }

        var translated = new List<string>();
        var anyChanged = false;
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

            var result = PopupTranslationPatch.TranslatePopupTextForProducerRoute(text, Context);
            if (!anyChanged && !string.Equals(text, result, StringComparison.Ordinal))
            {
                anyChanged = true;
            }

            translated.Add(result);
        }

        if (anyChanged)
        {
            args[index] = translated;
        }
    }

    private static void TranslatePopupMenuItemTextCollection(object[] args, int index)
    {
        if (index < 0 || index >= args.Length || args[index] is null || args[index] is string || args[index] is not IList list)
        {
            return;
        }

        for (var itemIndex = 0; itemIndex < list.Count; itemIndex++)
        {
            var item = list[itemIndex];
            if (item is null)
            {
                continue;
            }

            var textField = AccessTools.Field(item.GetType(), "text");
            if (textField is null || textField.FieldType != typeof(string))
            {
                continue;
            }

            var current = textField.GetValue(item) as string;
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            var translated = PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(current!, Context);
            if (string.Equals(current, translated, StringComparison.Ordinal))
            {
                continue;
            }

            textField.SetValue(item, translated);
            list[itemIndex] = item;
        }
    }
}
