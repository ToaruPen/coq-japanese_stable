using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class UITextSkinTranslationPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("XRL.UI.UITextSkin:SetText", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve UITextSkin.SetText(string). Patch will not apply.");
        }

        return method;
    }

    public static void Prefix(ref string text)
    {
        try
        {
            text = TranslatePreservingColors(text, nameof(UITextSkinTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: UITextSkinTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    internal static string TranslatePreservingColors(string? source, string? context = null)
    {
        using var _ = Translator.PushLogContext(context);

        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var (stripped, spans) = ColorCodePreserver.Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        var translated = Translator.Translate(stripped);
        return ColorCodePreserver.Restore(translated, spans);
    }

    internal static void TranslateStringField(object? instance, string fieldName, string? context = null)
    {
        if (instance is null || string.IsNullOrEmpty(fieldName))
        {
            return;
        }

        var field = AccessTools.Field(instance.GetType(), fieldName);
        if (field is null || field.FieldType != typeof(string))
        {
            return;
        }

        var current = field.GetValue(instance) as string;
        var translated = TranslatePreservingColors(current, context);
        if (!string.Equals(current, translated, StringComparison.Ordinal))
        {
            field.SetValue(instance, translated);
        }
    }

    internal static void TranslateStringFieldsInCollection(object? maybeCollection, string? context = null, params string[] fieldNames)
    {
        if (maybeCollection is null || maybeCollection is string || fieldNames is null || fieldNames.Length == 0)
        {
            return;
        }

        if (maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        foreach (var item in enumerable)
        {
            if (item is null)
            {
                continue;
            }

            for (var index = 0; index < fieldNames.Length; index++)
            {
                TranslateStringField(item, fieldNames[index], context);
            }
        }
    }
}
