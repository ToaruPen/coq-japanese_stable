using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class JoppaZealotTranslationPatch
{
    private const string Context = nameof(JoppaZealotTranslationPatch);

    private static readonly Regex YellMessagePattern = new(
        @"^(?<actor>.+?) yells?, (?<quote>{{W\|'.+'\}})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Lines = new(StringComparer.Ordinal)
    {
        ["Who ventures into the Great Salt Desert, and nearer the Six Day Stilt?"] = "大塩砂漠へ足を踏み入れ、六日のスティルトに近づく者は誰だ？",
        ["Hmm, what of your artifacts? Make an offering of them to Shekhinah at the Sacred Well."] = "ふむ、お前のアーティファクトはどうした？それらを聖なる井戸のシェキーナへ捧げよ。",
        ["The beauty! My stomach is in stirs."] = "なんという美しさだ！腹の底がかき乱される。",
        ["Is it a dybbuk that possesses the robot? It should be sacred and still."] = "ロボットに憑いているのはディブクか？それは神聖で静止しているべきものだ。",
    };

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.JoppaZealot");
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

        var actor = FloatingSpeechTranslationHelpers.NormalizeActorForJapaneseFrame(match.Groups["actor"].Value);
        translated = actor + "は" + translatedQuote + "と叫んだ";
        detail = "Yell";
        return true;
    }
}
