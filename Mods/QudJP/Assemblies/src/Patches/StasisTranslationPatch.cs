using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StasisTranslationPatch
{
    private const string Context = nameof(StasisTranslationPatch);

    private static readonly Regex PlayerAttackPattern = new(
        "^Your attack bounces harmlessly off of (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorAttackPattern = new(
        "^(?<actor>.+?) attack bounces harmlessly off of (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Stasis");
        var beforeApplyDamageEventType = AccessTools.TypeByName("XRL.World.BeforeApplyDamageEvent");
        if (targetType is null || beforeApplyDamageEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [beforeApplyDamageEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(BeforeApplyDamageEvent) not found.", Context);
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

        if (!TryTranslateAttackBounce(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static bool TryTranslateAttackBounce(string source, out string translated)
    {
        var match = PlayerAttackPattern.Match(source);
        if (match.Success)
        {
            translated = $"あなたの攻撃は{match.Groups["target"].Value}に当たって無害に跳ね返った。";
            return true;
        }

        match = ActorAttackPattern.Match(source);
        if (match.Success)
        {
            translated = $"{TrimPossessive(match.Groups["actor"].Value)}の攻撃は{match.Groups["target"].Value}に当たって無害に跳ね返った。";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TrimPossessive(string source)
    {
        return source.EndsWith("'s", StringComparison.Ordinal)
            ? source[..^2]
            : source;
    }
}
