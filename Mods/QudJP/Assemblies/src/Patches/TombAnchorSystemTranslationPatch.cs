using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TombAnchorSystemTranslationPatch
{
    private const string Context = nameof(TombAnchorSystemTranslationPatch);

    private static readonly Regex BellTollsPattern = new(
        "^&MThe Bell of Rest tolls! The dead will be recalled in (?<rounds>.+?) rounds\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.ITombAnchorSystem");
        var zoneType = AccessTools.TypeByName("XRL.World.Zone");
        if (targetType is null || zoneType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "OnEndTurn", Type.EmptyTypes);
        AddTarget(targets, targetType, "Recall", new[] { zoneType });
        AddTarget(targets, targetType, "AnchorCall", Type.EmptyTypes);
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
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

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "TombAnchorSystem.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var match = BellTollsPattern.Match(source);
        if (match.Success)
        {
            translated = $"&M安息の鐘が鳴る！死者は{match.Groups["rounds"].Value}ラウンド後に呼び戻される。";
            return true;
        }

        translated = source switch
        {
            "You've been recalled to a resting place." => "安息の場所へ呼び戻された。",
            "You were not recalled as you're already in a resting place." => "すでに安息の地にいるため呼び戻されなかった。",
            _ => string.Empty,
        };

        return translated.Length > 0;
    }
}
