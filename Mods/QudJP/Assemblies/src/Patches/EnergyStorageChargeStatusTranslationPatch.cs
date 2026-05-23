using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EnergyStorageChargeStatusTranslationPatch
{
    private static readonly Dictionary<string, string> ChargeStatusTranslations = new(StringComparer.Ordinal)
    {
        { "Drained", "空" },
        { "Very Low", "残量わずか" },
        { "Low", "残量少" },
        { "Used", "残量半分" },
        { "Fresh", "残量多" },
        { "Full", "残量十分" },
        { "Run Down", "巻き切れ" },
        { "Very Run Down", "巻きがかなり弱い" },
        { "Fairly Run Down", "巻きがやや弱い" },
        { "Somewhat Run Down", "巻きが少し弱い" },
        { "Well-Wound", "よく巻かれている" },
        { "Fully Wound", "完全に巻かれている" },
        { "Stopped", "停止" },
        { "Very Slow", "非常に遅い" },
        { "Somewhat Slow", "やや遅い" },
        { "Fairly Fast", "かなり速い" },
        { "Nearly Full Speed", "ほぼ最高速" },
        { "Full Speed", "最高速" },
        { "Slack", "ゆるい" },
        { "Very Slack", "非常にゆるい" },
        { "Fairly Slack", "かなりゆるい" },
        { "Somewhat Slack", "ややゆるい" },
        { "Tense", "張っている" },
        { "Fully Tensed", "完全に張っている" },
        { "Dark", "暗い" },
        { "Very Dim", "非常に薄暗い" },
        { "Somewhat Dim", "やや薄暗い" },
        { "Somewhat Bright", "やや明るい" },
        { "Fairly Bright", "かなり明るい" },
        { "Bright", "明るい" },
        { "Gray", "灰色" },
        { "Dark Gray", "暗灰色" },
        { "Murky Black", "濁った黒" },
        { "Black", "黒" },
        { "Deep Black", "深い黒" },
        { "Pure Black", "純黒" },
        { "Exhausted", "消耗" },
        { "Flagging", "弱り気味" },
        { "Enervated", "気力低下" },
        { "Fatigued", "疲労" },
        { "Lively", "元気" },
        { "Vigorous", "活力十分" },
    };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.World.Capabilities.EnergyStorage",
            "EnergyStorage");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: EnergyStorageChargeStatusTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "GetChargeStatus",
            new[] { typeof(int), typeof(int), typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: EnergyStorageChargeStatusTranslationPatch.GetChargeStatus(int, int, string) not found.");
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (TryTranslateChargeStatus(__result, out var translated))
            {
                __result = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: EnergyStorageChargeStatusTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static bool TryTranslateChargeStatus(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        if (!ChargeStatusTranslations.TryGetValue(visible, out var translatedVisible))
        {
            return false;
        }

        translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            _ => translatedVisible);
        return true;
    }
}
