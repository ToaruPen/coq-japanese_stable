using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SavesApiReadSaveJsonTranslationPatch
{
    private const string Context = nameof(SavesApiReadSaveJsonTranslationPatch);
    private const string TemplateKey = "Total size: {0}";
    private const string Prefix = "Total size: ";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("Qud.API.SavesAPI");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: SavesApiReadSaveJsonTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "ReadSaveJson", new[] { typeof(string), typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: SavesApiReadSaveJsonTranslationPatch.ReadSaveJson(string, string) not found.");
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            var current = GetSize(__result);
            if (current is null
                || current.Length == 0
                || !current.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return;
            }

            var translatedTemplate = Translator.Translate(TemplateKey);
            if (string.Equals(translatedTemplate, TemplateKey, StringComparison.Ordinal))
            {
                return;
            }

            var translated = translatedTemplate.Replace("{0}", current.Substring(Prefix.Length));
            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                return;
            }

            if (!SetSize(__result, translated))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, TemplateKey, current, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SavesApiReadSaveJsonTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static string? GetSize(object result)
    {
        var type = result.GetType();
        var property = AccessTools.Property(type, "Size");
        if (property is not null && property.CanRead && property.PropertyType == typeof(string))
        {
            return property.GetValue(result) as string;
        }

        var field = AccessTools.Field(type, "Size");
        return field?.FieldType == typeof(string)
            ? field.GetValue(result) as string
            : null;
    }

    private static bool SetSize(object result, string translated)
    {
        var type = result.GetType();
        var property = AccessTools.Property(type, "Size");
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(result, translated);
            return true;
        }

        var field = AccessTools.Field(type, "Size");
        if (field?.FieldType == typeof(string))
        {
            field.SetValue(result, translated);
            return true;
        }

        return false;
    }
}
