using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SixDayZealotTranslationPatch
{
    private const string Context = nameof(SixDayZealotTranslationPatch);

    private static readonly Regex YellMessagePattern = new(
        @"^The zealot yells (?<quote>{{W\|'.+'\}})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Lines = new(StringComparer.Ordinal)
    {
        ["Make an offering at the Argent Well! Pay homage to your Fathers!"] = "白銀の泉に捧げものをせよ！父祖を称えよ！",
        ["Cast down your artifacts! You are not worthy of their make!"] = "アーティファクトを打ち捨てよ！貴様にそれを持つ資格はない！",
        ["Piety compels you to deliver your sacred relics to the priests in the cathedral! Cleanse them of your filth!"] = "信仰心があるなら聖遺物を大聖堂の司祭に届けよ！貴様の穢れを清めるのだ！",
        ["The Machine commands that you exorcise robots and bring their sacred husks here!"] = "機械の御意志により、ロボットを祓い清め、聖なる殻をここへ持って来い！",
    };

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.SixDayZealot");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "ZealotDeclaim", [gameObjectType, typeof(bool)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ZealotDeclaim(GameObject, bool) target not found.", Context);
            yield break;
        }

        yield return method;
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

        if (!TryTranslateYellMessage(message, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslateParticleText(ref string text)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth)
            || string.IsNullOrEmpty(text)
            || !FloatingSpeechTranslationHelpers.TryTranslateWhiteWrappedParticle(text, Lines, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "GameObject.ParticleText",
            Context + ".FloatingSpeech",
            text,
            translated);
        text = translated;
        return true;
    }

    private static bool TryTranslateYellMessage(string source, out string translated, out string detail)
    {
        var match = YellMessagePattern.Match(source);
        if (!match.Success
            || !FloatingSpeechTranslationHelpers.TryTranslateWhiteQuotedFragment(match.Groups["quote"].Value, Lines, out var translatedQuote))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = "狂信者が" + translatedQuote + "と叫んだ";
        detail = "Yell";
        return true;
    }
}
