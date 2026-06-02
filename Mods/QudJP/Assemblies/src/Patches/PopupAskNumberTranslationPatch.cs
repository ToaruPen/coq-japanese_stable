using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupAskNumberTranslationPatch
{
    private const string Context = nameof(PopupAskNumberTranslationPatch);
    private const string TargetTypeName = "XRL.UI.Popup";
    private static readonly Regex LiquidLoaderSupplyPattern = new(
        "^Supply (?<host>.+?) with how many drams of your (?<liquid>.+?)\\? \\(max=(?<max>\\d+)\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MagazineAmmoLoaderSupplyPattern = new(
        "^Supply (?<host>.+?) with how many (?<ammo>.+?)\\? \\(max=(?<max>\\d+)\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var popupType = AccessTools.TypeByName(TargetTypeName);
        if (popupType is null)
        {
            Trace.TraceError($"QudJP: {Context} target type '{TargetTypeName}' not found.");
            return targets;
        }

        AddTarget(
            targets,
            AccessTools.Method(
                popupType,
                "AskNumber",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                }),
            "AskNumber");

        AddTarget(
            targets,
            AccessTools.Method(
                popupType,
                "AskNumberAsync",
                new[]
                {
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(string),
                    typeof(bool),
                }),
            "AskNumberAsync");

        if (targets.Count == 0)
        {
            Trace.TraceError($"QudJP: {Context} resolved zero target methods.");
        }

        return targets;
    }

    public static void Prefix(object[] __args)
    {
        try
        {
            if (__args.Length == 0 || __args[0] is not string message || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (TryTranslateLiquidLoaderSupplyPrompt(message, out var liquidSupplyTranslated))
            {
                __args[0] = liquidSupplyTranslated;
                return;
            }

            if (TryTranslateMagazineAmmoLoaderSupplyPrompt(message, out var magazineSupplyTranslated))
            {
                __args[0] = magazineSupplyTranslated;
                return;
            }

            if (TradeScreenUiTranslationPatch.TryTranslateTradeSomePrompt(message, out var tradeSomeTranslated))
            {
                __args[0] = tradeSomeTranslated;
                return;
            }

            if (SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage(
                    message,
                    Context,
                    "Popup.AskNumber",
                    out var ownerTranslated))
            {
                __args[0] = ownerTranslated;
                return;
            }

            __args[0] = PopupTranslationPatch.TranslatePopupTextForProducerRoute(message, Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static bool TryTranslateLiquidLoaderSupplyPrompt(string source, out string translated)
    {
        var match = LiquidLoaderSupplyPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var host = TranslateDisplayNameCapture(match.Groups["host"].Value);
        var liquid = TranslateLiquidCapture(match.Groups["liquid"].Value);
        translated = string.Concat(
            host,
            "へあなたの",
            liquid,
            "を何ドラム補給しますか？ (最大=",
            match.Groups["max"].Value,
            ")");
        DynamicTextObservability.RecordTransform(
            Context,
            "Popup.AskNumber.LiquidLoaderSupply",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateMagazineAmmoLoaderSupplyPrompt(string source, out string translated)
    {
        var match = MagazineAmmoLoaderSupplyPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var host = TranslateDisplayNameCapture(match.Groups["host"].Value);
        var ammo = TranslateMagazineAmmoCapture(match.Groups["ammo"].Value);

        translated = string.Concat(
            host,
            "へ",
            ammo,
            "をいくつ補給しますか？ (最大=",
            match.Groups["max"].Value,
            ")");
        DynamicTextObservability.RecordTransform(
            Context,
            "Popup.AskNumber.MagazineAmmoLoaderSupply",
            source,
            translated);
        return true;
    }

    private static string TranslateMagazineAmmoCapture(string source)
    {
        var trimmed = source.Trim();
        if (trimmed.StartsWith("of ", StringComparison.Ordinal))
        {
            return TranslateDisplayNameCapture(trimmed.Substring(3));
        }

        return TranslateDisplayNameCapture(source);
    }

    private static string TranslateDisplayNameCapture(string source)
    {
        return DisplayNameCaptureTranslator.TryTranslatePlaceholderValue(source, Context, out var translated)
            ? translated
            : source;
    }

    private static string TranslateLiquidCapture(string source)
    {
        var translated = LiquidVolumeFragmentTranslator.TranslateLiquidPhrasePreservingColors(source);
        return translated is null ? source : translated;
    }

    private static void AddTarget(List<MethodBase> targets, MethodInfo? method, string methodName)
    {
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceWarning("QudJP: {0} failed to resolve Popup.{1}.", Context, methodName);
    }
}
