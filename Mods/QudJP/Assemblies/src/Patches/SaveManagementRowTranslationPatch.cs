using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SaveManagementRowTranslationPatch
{
    private const string Context = nameof(SaveManagementRowTranslationPatch);
    private const string LocationPrefix = "{{C|Location:}} ";
    private const string LastSavedPrefix = "{{C|Last saved:}} ";
    private static readonly Regex SaveTimePattern =
        new Regex("^(?<weekday>Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday), (?<month>January|February|March|April|May|June|July|August|September|October|November|December) (?<day>\\d{1,2}), (?<year>\\d{4}) at (?<time>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Dictionary<string, string> Weekdays = new(StringComparer.Ordinal)
    {
        ["Monday"] = "月曜日",
        ["Tuesday"] = "火曜日",
        ["Wednesday"] = "水曜日",
        ["Thursday"] = "木曜日",
        ["Friday"] = "金曜日",
        ["Saturday"] = "土曜日",
        ["Sunday"] = "日曜日",
    };
    private static readonly Dictionary<string, string> Months = new(StringComparer.Ordinal)
    {
        ["January"] = "1月",
        ["February"] = "2月",
        ["March"] = "3月",
        ["April"] = "4月",
        ["May"] = "5月",
        ["June"] = "6月",
        ["July"] = "7月",
        ["August"] = "8月",
        ["September"] = "9月",
        ["October"] = "10月",
        ["November"] = "11月",
        ["December"] = "12月",
    };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return FrameworkDataElementSetDataTargetResolver.Resolve(Context, "SaveManagementRow", "SaveManagementRow");
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null
                || UiBindingTranslationHelpers.GetMemberValue(__instance, "TextSkins") is not IList textSkins
                || textSkins.Count <= 2)
            {
                return;
            }

            TranslateColoredLabel(textSkins[1], LocationPrefix, "Location:", "SaveManagementRow.Location");
            TranslateColoredLabel(textSkins[2], LastSavedPrefix, "Last saved:", "SaveManagementRow.LastSaved", TranslateSaveTime);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SaveManagementRowTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateColoredLabel(
        object? uiTextSkin,
        string prefix,
        string labelKey,
        string observabilityFamily,
        Func<string, string>? translateValue = null)
    {
        var current = UITextSkinReflectionAccessor.GetCurrentText(uiTextSkin, Context);
        if (current is null
            || current.Length == 0
            || !current.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var translatedLabel = Translator.Translate(labelKey);
        if (string.Equals(translatedLabel, labelKey, StringComparison.Ordinal))
        {
            return;
        }

        var value = current!.Substring(prefix.Length);
        var translatedValue = translateValue is null ? value : translateValue(value);
        var translated = "{{C|" + translatedLabel + "}} " + translatedValue;
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, observabilityFamily, current, translated);
        OwnerTextSetter.SetTranslatedText(uiTextSkin, current, translated, Context, typeof(SaveManagementRowTranslationPatch));
    }

    private static string TranslateSaveTime(string current)
    {
        var match = SaveTimePattern.Match(current);
        if (!match.Success
            || !Weekdays.TryGetValue(match.Groups["weekday"].Value, out var weekday)
            || !Months.TryGetValue(match.Groups["month"].Value, out var month))
        {
            return current;
        }

        return weekday
            + ", "
            + month
            + " "
            + match.Groups["day"].Value
            + ", "
            + match.Groups["year"].Value
            + " "
            + match.Groups["time"].Value;
    }
}
