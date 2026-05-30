using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DisassemblyStartTranslationPatch
{
    private const string Context = nameof(DisassemblyStartTranslationPatch);
    private static readonly Regex ReverseEngineerPromptPattern = new(
        "^Do you want to try to reverse engineer (?<item>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StartDisassemblingPattern = new(
        "^You start disassembling (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisassembleEurekaBuildReceiptPattern = new(
        "^You disassemble\\s+(?:(?:the|your)\\s+)?(?<item>.+?)\\.\\s+Eureka! You may now build\\s+(?<build>.+?)\\.\\s+You receive tinkering bits <(?<bits>.+?)>\\.*!?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Tinkering.Disassembly");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        foreach (var methodName in new[] { "Continue", "End" })
        {
            var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
            if (method is not null)
            {
                targets.Add(method);
                continue;
            }

            Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
        }

        return targets;
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (!TryTranslateStartDisassemblingMessage(message, out var translated)
            && !TryTranslateDisassembleEurekaBuildReceiptMessage(message, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateReverseEngineerPrompt(source, out translated)
            || TryTranslateDisassembleEurekaBuildReceiptMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateReverseEngineerPrompt(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ReverseEngineerPromptPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "item")}をリバースエンジニアリングしてみる？";
        return true;
    }

    private static bool TryTranslateStartDisassemblingMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StartDisassemblingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "item")}の分解を始めた。";
        return true;
    }

    private static bool TryTranslateDisassembleEurekaBuildReceiptMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = DisassembleEurekaBuildReceiptPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var item = TranslateDisplayNameCapture(RestoreCapture(match, spans, "item"));
        var build = TranslateDisplayNameCapture(RestoreCapture(match, spans, "build"));
        var bits = RestoreCapture(match, spans, "bits");
        translated = $"{item}を分解し、修理ビット<{bits}>を受け取った。ひらめいた！ {build}を作れるようになった。";
        return true;
    }

    private static string TranslateDisplayNameCapture(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => GetDisplayNameRouteTranslator.TranslatePreservingColors(visible, Context));
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
