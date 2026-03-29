using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TradeScreenUpdateTotalsTranslationPatch
{
    private const string Context = nameof(TradeScreenUpdateTotalsTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.TradeScreen", "TradeScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: TradeScreenUpdateTotalsTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateTotals");
        if (method is null)
        {
            Trace.TraceError("QudJP: TradeScreenUpdateTotalsTranslationPatch.UpdateTotals() not found.");
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

            TranslateTotalLabels(__instance);
            TranslateFreeDramsLabels(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TradeScreenUpdateTotalsTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateTotalLabels(object instance)
    {
        var totalLabels = GetMemberValue(instance, "totalLabels");
        if (totalLabels is not Array totalLabelsArray || totalLabelsArray.Length < 2)
        {
            return;
        }

        TranslateLabelText(
            totalLabelsArray.GetValue(0),
            " drams",
            "TradeScreen.TotalLabel0");
        TranslateLabelText(
            totalLabelsArray.GetValue(1),
            " drams",
            "TradeScreen.TotalLabel1");
    }

    private static void TranslateFreeDramsLabels(object instance)
    {
        var freeDramsLabels = GetMemberValue(instance, "freeDramsLabels");
        if (freeDramsLabels is not Array freeDramsArray || freeDramsArray.Length < 2)
        {
            return;
        }

        TranslateLabelText(
            freeDramsArray.GetValue(1),
            "lbs.",
            "TradeScreen.FreeDramsWeight");
    }

    private static void TranslateLabelText(object? label, string token, string family)
    {
        if (label is null)
        {
            return;
        }

        var rawText = UITextSkinReflectionAccessor.GetCurrentText(label, Context);
        if (rawText is not { Length: > 0 } currentText)
        {
            return;
        }

        var translatedToken = Translator.Translate(token);
        if (string.Equals(translatedToken, token, StringComparison.Ordinal))
        {
            return;
        }

        var tokenIndex = currentText.IndexOf(token, StringComparison.Ordinal);
        if (tokenIndex < 0)
        {
            return;
        }

#pragma warning disable CA1845 // net48 does not support AsSpan
        var translated = currentText.Substring(0, tokenIndex) + translatedToken + currentText.Substring(tokenIndex + token.Length);
#pragma warning restore CA1845
        if (string.Equals(translated, currentText, StringComparison.Ordinal))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, family, currentText, translated);
        _ = UITextSkinReflectionAccessor.SetCurrentText(label, translated, Context);
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var field = AccessTools.Field(type, memberName);
        if (field is not null)
        {
            return field.GetValue(instance);
        }

        var property = AccessTools.Property(type, memberName);
        return property is not null && property.CanRead ? property.GetValue(instance) : null;
    }
}
