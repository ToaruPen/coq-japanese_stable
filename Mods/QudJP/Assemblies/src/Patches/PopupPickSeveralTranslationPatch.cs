using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupPickSeveralTranslationPatch
{
    private const string Context = nameof(PopupPickSeveralTranslationPatch);

    private static readonly Regex SelectionLimitPattern = new(
        "^You cannot select more than (?<amount>.+?) options!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var popupType = AccessTools.TypeByName("XRL.UI.Popup");
        if (popupType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: XRL.UI.Popup.", Context);
            yield break;
        }

        var method = AccessTools.Method(popupType, "PickSeveral");
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.XRL.UI.Popup.PickSeveral target not found.", Context);
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

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (TryTranslateSelectionLimit(source, out translated))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + ".SelectionLimit",
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateSelectionLimit(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SelectionLimitPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var amount = NormalizeCardinal(ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["amount"].Value,
            spans,
            match.Groups["amount"]).Trim());
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"選択肢は{amount}個までしか選べない！",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string NormalizeCardinal(string source)
    {
        return source.Trim() switch
        {
            "zero" => "0",
            "one" => "1",
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "six" => "6",
            "seven" => "7",
            "eight" => "8",
            "nine" => "9",
            "ten" => "10",
            var other => other,
        };
    }
}
