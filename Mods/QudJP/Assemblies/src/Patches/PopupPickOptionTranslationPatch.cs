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

    public static void Prefix(ref string __0, ref string? __1, ref string __2, ref IReadOnlyList<string>? __4, object? __7)
    {
        try
        {
            __0 = TranslatePopupText(__0)!;
            __1 = TranslatePopupText(__1);
            __2 = TranslatePopupText(__2)!;
            __4 = TranslateStringList(__4);
            TranslatePopupMenuItemTextCollection(__7);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static string? TranslatePopupText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return PopupTranslationPatch.TranslatePopupTextForProducerRoute(text!, Context);
    }

    private static IReadOnlyList<string>? TranslateStringList(IReadOnlyList<string>? source)
    {
        if (source is null)
        {
            return null;
        }

        List<string>? translated = null;
        var anyChanged = false;
        for (var index = 0; index < source.Count; index++)
        {
            var originalText = source[index];
            if (originalText is null)
            {
                Trace.TraceWarning("QudJP: {0} encountered null option text.", Context);
                anyChanged = true;
            }

            var result = TranslateOrEmpty(originalText);
            if (!anyChanged && !string.Equals(originalText, result, StringComparison.Ordinal))
            {
                anyChanged = true;
            }

            if (!anyChanged)
            {
                continue;
            }

            if (translated is null)
            {
                translated = new List<string>(source.Count);
                for (var previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    translated.Add(TranslateOrEmpty(source[previousIndex]));
                }
            }

            translated.Add(result);
        }

        return anyChanged ? translated! : source;
    }

    private static string TranslateOrEmpty(string? originalText)
    {
        string text;
        if (originalText is null)
        {
            Trace.TraceWarning(
                "QudJP: PopupPickOptionTranslationPatch.TranslateOrEmpty encountered null text. Context={0}",
                Context);
            text = string.Empty;
        }
        else
        {
            text = originalText;
        }

        return PopupTranslationPatch.TranslatePopupTextForProducerRoute(text, Context);
    }

    private static void TranslatePopupMenuItemTextCollection(object? maybeList)
    {
        if (maybeList is null || maybeList is string || maybeList is not IEnumerable items)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            if (item.GetType().IsValueType)
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
        }
    }
}
