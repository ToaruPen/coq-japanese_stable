using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsBaseItemShortDescriptionTranslationPatch
{
    private const string Context = nameof(CyberneticsBaseItemShortDescriptionTranslationPatch);
    private const string Family = "CyberneticsBaseItem.ShortDescriptionMetadata";

    private static readonly Regex RulesPattern = new(
        "\\{\\{rules\\|(?<body>.*?)\\}\\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TargetBodyPartsPattern = new(
        "^Target body parts: (?<slots>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LicensePointsPattern = new(
        "^License points: (?<value>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> SlotNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Face"] = "顔",
        ["Body"] = "胴",
        ["Head"] = "頭",
        ["Back"] = "背中",
        ["Feet"] = "足",
        ["Arm"] = "腕",
        ["Hand"] = "手",
        ["Hands"] = "手",
        ["Tail"] = "尾",
    };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsBaseItem");
        var shortDescriptionEventType = AccessTools.TypeByName("XRL.World.GetShortDescriptionEvent");
        if (targetType is null || shortDescriptionEventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve CyberneticsBaseItem or GetShortDescriptionEvent.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [shortDescriptionEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(GetShortDescriptionEvent) not found.", Context);
        }

        return method;
    }

    public static void Prefix(object? E, out int __state)
    {
        try
        {
            __state = TryGetPostfixBuilder(E, out var postfix) ? postfix.Length : 0;
        }
        catch (Exception ex)
        {
            __state = 0;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static void Postfix(object? E, int __state)
    {
        try
        {
            if (!TryGetPostfixBuilder(E, out var postfix) || postfix.Length <= __state)
            {
                return;
            }

            var appended = postfix.ToString(__state, postfix.Length - __state);
            var translated = TranslateAppendedText(appended);
            if (string.Equals(appended, translated, StringComparison.Ordinal))
            {
                return;
            }

            postfix.Remove(__state, postfix.Length - __state);
            postfix.Append(translated);
            DynamicTextObservability.RecordTransform(Context, Family, appended, translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateAppendedText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var unmarked = MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var stripped)
            ? stripped
            : source;
        return RulesPattern.Replace(unmarked, match =>
        {
            var body = match.Groups["body"].Value;
            var translatedBody = TranslateRulesBody(body);
            return string.Equals(body, translatedBody, StringComparison.Ordinal)
                ? match.Value
                : "{{rules|" + translatedBody + "}}";
        });
    }

    private static string TranslateRulesBody(string source)
    {
        if (string.Equals(source, "Destroyed when uninstalled.", StringComparison.Ordinal))
        {
            return "アンインストール時に破壊される。";
        }

        var targetBodyPartsMatch = TargetBodyPartsPattern.Match(source);
        if (targetBodyPartsMatch.Success)
        {
            return "対象身体部位: " + TranslateSlots(targetBodyPartsMatch.Groups["slots"].Value);
        }

        var licensePointsMatch = LicensePointsPattern.Match(source);
        if (licensePointsMatch.Success)
        {
            return "ライセンスポイント: " + licensePointsMatch.Groups["value"].Value;
        }

        if (string.Equals(source, "Only compatible with True Kin genotypes", StringComparison.Ordinal))
        {
            return "真性人類の遺伝子型にのみ対応";
        }

        return source;
    }

    private static string TranslateSlots(string source)
    {
        var parts = source.Split(',');
        for (var index = 0; index < parts.Length; index++)
        {
            var trimmed = parts[index].Trim();
            parts[index] = SlotNames.TryGetValue(trimmed, out var translated) ? translated : trimmed;
        }

        return string.Join(", ", parts);
    }

    private static bool TryGetPostfixBuilder(object? eventObject, out StringBuilder postfix)
    {
        postfix = null!;
        if (eventObject is null)
        {
            return false;
        }

        var property = AccessTools.Property(eventObject.GetType(), "Postfix");
        if (property?.GetValue(eventObject) is StringBuilder propertyBuilder)
        {
            postfix = propertyBuilder;
            return true;
        }

        var field = AccessTools.Field(eventObject.GetType(), "Postfix");
        if (field?.GetValue(eventObject) is StringBuilder fieldBuilder)
        {
            postfix = fieldBuilder;
            return true;
        }

        return false;
    }
}
