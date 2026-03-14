using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FactionsStatusScreenTranslationPatch
{
    private static readonly Regex VillageLabelPattern =
        new Regex("^The villagers of (?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VillageNeutralPattern =
        new Regex("^The villagers of (?<name>.+) don't care about you, but aggressive ones will attack you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VillageGossipPattern =
        new Regex("^The villagers of (?<name>.+) are interested in hearing gossip that's about them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ReputationValuePattern =
        new Regex("^Reputation:\\s+(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.FactionsStatusScreen", "FactionsStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: FactionsStatusScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: FactionsStatusScreenTranslationPatch.UpdateViewFromData not found.");
        }

        return method;
    }

    public static void Postfix(object? ___rawData, object? ___sortedData)
    {
        try
        {
            TranslateFactionLineCollection(___rawData);
            TranslateFactionLineCollection(___sortedData);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: FactionsStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateFactionLineCollection(object? maybeCollection)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        foreach (var item in enumerable)
        {
            if (item is null)
            {
                continue;
            }

            TranslateFactionLineField(item, "label");
        }
    }

    private static void TranslateFactionLineField(object item, string fieldName)
    {
        var field = AccessTools.Field(item.GetType(), fieldName);
        if (field is null || field.FieldType != typeof(string))
        {
            return;
        }

        var current = field.GetValue(item) as string;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = TranslateFactionText(current!);
        if (!string.Equals(current, translated, StringComparison.Ordinal))
        {
            field.SetValue(item, translated);
        }
    }

    private static string TranslateFactionText(string source)
    {
        using var _ = Translator.PushLogContext(nameof(FactionsStatusScreenTranslationPatch));

        if (TryTranslateTemplate(source, VillageNeutralPattern, "The villagers of {0} don't care about you, but aggressive ones will attack you.", out var translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, VillageGossipPattern, "The villagers of {0} are interested in hearing gossip that's about them.", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, VillageLabelPattern, "The villagers of {0}", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, ReputationValuePattern, "Reputation: {0}", out translated, groupName: "value"))
        {
            return translated;
        }

        return Translator.Translate(source);
    }

    private static bool TryTranslateTemplate(string source, Regex pattern, string templateKey, out string translated, string groupName = "name")
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedTemplate.Replace("{0}", match.Groups[groupName].Value);
        return true;
    }
}
