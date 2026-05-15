using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BrainThinkTranslationPatch
{
    private const string Context = nameof(BrainThinkTranslationPatch);
    private const string Detail = "Think";

    private static readonly Regex ThoughtPattern = new(
        "^(?<actor>.+?) thinks: '(?<thought>[\\s\\S]*)'$",
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

        var method = AccessTools.Method(brainType, "Think", [typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Think(string) not found.", Context);
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        var match = ThoughtPattern.Match(message);
        if (!match.Success)
        {
            return false;
        }

        var translated = $"{match.Groups["actor"].Value}は考える:「{match.Groups["thought"].Value}」";
        DynamicTextObservability.RecordTransform(Context, Detail, message, translated);
        message = translated;
        return true;
    }
}
