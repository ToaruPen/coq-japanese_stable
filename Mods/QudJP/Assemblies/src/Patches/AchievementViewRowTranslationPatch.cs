using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AchievementViewRowTranslationPatch
{
    private const string Context = nameof(AchievementViewRowTranslationPatch);
    private const string HiddenCountTemplateKey = "{N} hidden achievements remaining";
    private const string HiddenCountToken = "{N}";
    private const string UnlockedPrefix = "Unlocked ";

    private static readonly Regex HiddenCountPattern = new(
        "^(?<count>\\d+) hidden achievements remaining$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.AchievementViewRow", "AchievementViewRow");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: AchievementViewRowTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "setData");
        if (method is null)
        {
            Trace.TraceError("QudJP: AchievementViewRowTranslationPatch.setData() not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateNameText(__instance);
            TranslateDescriptionText(__instance);
            TranslateDateText(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AchievementViewRowTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateNameText(object instance)
    {
        var nameText = UiBindingTranslationHelpers.GetMemberValue(instance, "Name");
        var current = UITextSkinReflectionAccessor.GetCurrentText(nameText, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=Name");
        var translated = TranslateHiddenCountText(current!, route);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        OwnerTextSetter.SetTranslatedText(nameText, current!, translated, Context, typeof(AchievementViewRowTranslationPatch));
    }

    private static void TranslateDescriptionText(object instance)
    {
        var descriptionText = UiBindingTranslationHelpers.GetMemberValue(instance, "Description");
        var current = UITextSkinReflectionAccessor.GetCurrentText(descriptionText, Context);
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var translated = StringHelpers.TranslateExactOrLowerAscii(current!);
        if (string.IsNullOrEmpty(translated) || string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=Description");
        DynamicTextObservability.RecordTransform(route, "AchievementViewRow.Description", current!, translated!);
        OwnerTextSetter.SetTranslatedText(descriptionText, current!, translated!, Context, typeof(AchievementViewRowTranslationPatch));
    }

    private static void TranslateDateText(object instance)
    {
        var dateText = UiBindingTranslationHelpers.GetMemberValue(instance, "Date");
        var current = UITextSkinReflectionAccessor.GetCurrentText(dateText, Context);
        if (string.IsNullOrEmpty(current) || !current!.StartsWith(UnlockedPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var translatedPrefix = StringHelpers.TranslateExactOrLowerAscii(UnlockedPrefix);
        if (string.IsNullOrEmpty(translatedPrefix))
        {
            return;
        }

        var translated = translatedPrefix! + current.Substring(UnlockedPrefix.Length);
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=Date");
        DynamicTextObservability.RecordTransform(route, "AchievementViewRow.DatePrefix", current, translated);
        OwnerTextSetter.SetTranslatedText(dateText, current, translated, Context, typeof(AchievementViewRowTranslationPatch));
    }

    private static string TranslateHiddenCountText(string source, string route)
    {
        var match = HiddenCountPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var template = StringHelpers.TranslateExactOrLowerAscii(HiddenCountTemplateKey);
        if (string.IsNullOrEmpty(template))
        {
            return source;
        }

        var translated = template!.Replace(HiddenCountToken, match.Groups["count"].Value);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "AchievementViewRow.HiddenCount", source, translated);
        }

        return translated;
    }
}
