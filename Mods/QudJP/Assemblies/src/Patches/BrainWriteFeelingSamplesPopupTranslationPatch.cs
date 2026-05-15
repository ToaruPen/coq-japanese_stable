using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BrainWriteFeelingSamplesPopupTranslationPatch
{
    private const string Context = nameof(BrainWriteFeelingSamplesPopupTranslationPatch);
    private const string Detail = "WriteFeelingSamples";

    private static readonly Regex WrittenPattern = new(
        "^(?<count>\\d+) feelings written to (?<file>.+?) in (?<path>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var brainType = AccessTools.TypeByName("XRL.World.Parts.Brain");
        if (brainType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(brainType, "WriteFeelingSamples", [typeof(bool)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.WriteFeelingSamples(bool) not found.", Context);
        }

        return method;
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

        var match = WrittenPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{match.Groups["path"].Value}の{match.Groups["file"].Value}に{match.Groups["count"].Value}件の感情を書き出した！";
        DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + "." + Detail, source, translated);
        return true;
    }
}
