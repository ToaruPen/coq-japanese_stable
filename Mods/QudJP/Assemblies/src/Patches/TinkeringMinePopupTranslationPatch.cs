using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkeringMinePopupTranslationPatch
{
    private const string Context = nameof(TinkeringMinePopupTranslationPatch);

    private static readonly Regex DisarmConfirmationPattern = new(
        "^Failing to disarm (?<target>.+?) will detonate (?<pronoun>.+?)\\. You estimate you have (?<estimate>about a|less than a) (?<chance>.+?) chance of success\\. Do you want to make the attempt\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Tinkering_Mine");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (targetType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var handleEvent = AccessTools.Method(targetType, "HandleEvent", [inventoryActionEventType]);
        if (handleEvent is not null)
        {
            yield return handleEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) not found.", Context);
        }
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;
        translated = source;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = DisarmConfirmationPattern.Match(stripped);
        if (!match.Success)
        {
            return false;
        }

        var target = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["target"].Value,
            spans,
            match.Groups["target"]).Trim();
        target = DisplayNameCaptureTranslator.TranslatePreservingColors(target, Context);
        var chance = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["chance"].Value,
            spans,
            match.Groups["chance"]).Trim();
        var chancePhrase = string.Equals(match.Groups["estimate"].Value, "less than a", StringComparison.Ordinal)
            ? $"{chance}未満"
            : $"およそ{chance}";

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{target}の解除に失敗すると爆発する。成功率は{chancePhrase}だと見積もっている。試みますか？",
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".DisarmConfirmation",
            source,
            translated);
        return true;
    }
}
