using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbsorbablePsychePopupTranslationPatch
{
    private const string Context = nameof(AbsorbablePsychePopupTranslationPatch);
    private const string ConfirmationPrefix =
        "At the moment of victory, your swelling ego curves the psychic aether and causes the psyche of ";
    private const string ConfirmationSuffix =
        " to collide with your own. As the weaker of the two, its binding energy is exceeded and it explodes. Would you like to encode its psionic bits on the holographic boundary of your own psyche?\n\n(+1 Ego permanently)";
    private const string EncodePrefix = "You encode the psyche of ";
    private const string EncodeSuffix = " and gain +{{C|1}} {{Y|Ego}}!";
    private const string RadiatePrefix = "You pause as the psyche of ";
    private const string RadiateSuffix = " radiates into nothingness.";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.AbsorbablePsyche");
        var eventType = AccessTools.TypeByName("XRL.World.BeforeDeathRemovalEvent");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(BeforeDeathRemovalEvent) not found.", Context);
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

    internal static void ResetForTests()
    {
        activeDepth = 0;
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

        if (TryTranslateConfirmation(source, out translated)
            || TryTranslateEncode(source, out translated)
            || TryTranslateRadiate(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateConfirmation(string source, out string translated)
    {
        if (!TryCaptureBetween(source, ConfirmationPrefix, ConfirmationSuffix, out var name))
        {
            translated = source;
            return false;
        }

        translated = "勝利の瞬間、膨張する自我が精神のエーテルをゆがませ、"
            + name
            + "の精神をあなた自身の精神に衝突させる。弱い方であるその精神は束縛エネルギーを超えて爆発する。"
            + "そのサイオニック片をあなた自身の精神を囲むホログラフィック境界に刻みつけますか？\n\n"
            + "（恒久的に自我 +1）";
        return true;
    }

    private static bool TryTranslateEncode(string source, out string translated)
    {
        if (!TryCaptureBetween(source, EncodePrefix, EncodeSuffix, out var name))
        {
            translated = source;
            return false;
        }

        translated = name + "の精神を刻みつけ、自我が+{{C|1}}上昇した！";
        return true;
    }

    private static bool TryTranslateRadiate(string source, out string translated)
    {
        if (!TryCaptureBetween(source, RadiatePrefix, RadiateSuffix, out var name))
        {
            translated = source;
            return false;
        }

        translated = name + "の精神が無へと放射されていくのを見届ける。";
        return true;
    }

    private static bool TryCaptureBetween(string source, string prefix, string suffix, out string capture)
    {
        if (!source.StartsWith(prefix, StringComparison.Ordinal)
            || !source.EndsWith(suffix, StringComparison.Ordinal))
        {
            capture = string.Empty;
            return false;
        }

        capture = source.Substring(prefix.Length, source.Length - prefix.Length - suffix.Length);
        return capture.Length > 0;
    }
}
