using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CudgelConkPopupTranslationPatch
{
    private const string Context = nameof(CudgelConkPopupTranslationPatch);

    private static readonly Regex NoHeadPattern =
        new Regex(
            "^(?<target>[\\s\\S]+?) (?:doesn't|don't) have anything like a head to conk\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ConfirmSelfConkPattern =
        new Regex(
            "^Are you sure you want to conk (?<target>[\\s\\S]+?) on (?<location>[\\s\\S]+)\\?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Skill.Cudgel_Conk");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target type.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "PerformConk", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.PerformConk() not found.", Context);
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

        var noHeadMatch = NoHeadPattern.Match(source);
        if (noHeadMatch.Success)
        {
            translated = string.Concat(
                noHeadMatch.Groups["target"].Value,
                "には殴る頭のようなものがない。");
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".NoHead", source, translated);
            return true;
        }

        var confirmSelfConkMatch = ConfirmSelfConkPattern.Match(source);
        if (confirmSelfConkMatch.Success)
        {
            translated = string.Concat(
                "本当に",
                confirmSelfConkMatch.Groups["target"].Value,
                "を",
                confirmSelfConkMatch.Groups["location"].Value,
                "にこん棒で殴りますか？");
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".ConfirmSelfConk", source, translated);
            return true;
        }

        translated = source;
        return false;
    }
}
