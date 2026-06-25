using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SavesApiReadSaveJsonTranslationPatch
{
    private const string Context = nameof(SavesApiReadSaveJsonTranslationPatch);
    private const string TemplateKey = "Total size: {0}";
    private const string Prefix = "Total size: ";
    private static readonly Regex SaveDescriptionPattern =
        new Regex("^Level (?<level>\\d+) (?<subtype>.*?) \\[(?<mode>.+?)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SaveInfoPattern =
        new Regex("^(?<location>.+?), (?<time>.+?) turn (?<turn>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

            TranslateStringMember(__result, "Description", "Description", TranslateDescription);
            TranslateStringMember(__result, "Info", "Info", TranslateInfo);
            TranslateStringMember(__result, "Size", TemplateKey, TranslateSize);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SavesApiReadSaveJsonTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static string TranslateDescription(string current)
    {
        var match = SaveDescriptionPattern.Match(current);
        if (!match.Success)
        {
            return current;
        }

        var subtypeSource = match.Groups["subtype"].Value;
        if (string.IsNullOrWhiteSpace(subtypeSource))
        {
            return current;
        }

        var subtype = ChargenStructuredTextTranslator.Translate(subtypeSource);
        var mode = Translator.Translate(match.Groups["mode"].Value);
        var template = Translator.Translate("Level {0} {1} [{2}]");
        if (string.Equals(template, "Level {0} {1} [{2}]", StringComparison.Ordinal))
        {
            return current;
        }

        return ReplaceTemplatePlaceholders(template, match.Groups["level"].Value, subtype, mode);
    }

    private static string TranslateInfo(string current)
    {
        var match = SaveInfoPattern.Match(current);
        if (!match.Success)
        {
            return current;
        }

        var template = Translator.Translate("{0}, {1} turn {2}");
        if (string.Equals(template, "{0}, {1} turn {2}", StringComparison.Ordinal))
        {
            return current;
        }

        return ReplaceTemplatePlaceholders(
            template,
            match.Groups["location"].Value,
            match.Groups["time"].Value,
            match.Groups["turn"].Value);
    }

    private static string TranslateSize(string current)
    {
        if (current.Length == 0
            || !current.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return current;
        }

        var translatedTemplate = Translator.Translate(TemplateKey);
        return string.Equals(translatedTemplate, TemplateKey, StringComparison.Ordinal)
            ? current
            : ReplaceTemplatePlaceholders(translatedTemplate, current.Substring(Prefix.Length));
    }

    private static string ReplaceTemplatePlaceholders(string template, params string[] values)
    {
        var builder = new StringBuilder(template.Length);
        for (var index = 0; index < template.Length; index++)
        {
            if (index + 2 < template.Length
                && template[index] == '{'
                && template[index + 2] == '}'
                && char.IsDigit(template[index + 1]))
            {
                var valueIndex = template[index + 1] - '0';
                if (valueIndex < values.Length)
                {
                    builder.Append(values[valueIndex]);
                    index += 2;
                    continue;
                }
            }

            builder.Append(template[index]);
        }

        return builder.ToString();
    }

    private static void TranslateStringMember(object result, string memberName, string observabilityFamily, Func<string, string> translate)
    {
        var current = GetStringMember(result, memberName);
        if (current is null || current.Length == 0)
        {
            return;
        }

        var translated = translate(current);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        if (SetStringMember(result, memberName, translated))
        {
            DynamicTextObservability.RecordTransform(Context, observabilityFamily, current, translated);
        }
    }

    private static string? GetStringMember(object result, string memberName)
    {
        var type = result.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(string))
        {
            return property.GetValue(result) as string;
        }

        var field = AccessTools.Field(type, memberName);
        return field?.FieldType == typeof(string)
            ? field.GetValue(result) as string
            : null;
    }

    private static bool SetStringMember(object result, string memberName, string translated)
    {
        var type = result.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(result, translated);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field?.FieldType == typeof(string))
        {
            field.SetValue(result, translated);
            return true;
        }

        return false;
    }
}
