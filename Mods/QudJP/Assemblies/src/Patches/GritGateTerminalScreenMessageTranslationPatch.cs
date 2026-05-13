using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GritGateTerminalScreenMessageTranslationPatch
{
    private const string Context = nameof(GritGateTerminalScreenMessageTranslationPatch);
    private const string AlarmMessage = "Alarms blare across the enclave.";
    private const string AlarmContext = "XRL.UI.GritGateTerminalScreenMessage";
    private const string DictionaryFile = "ui-messagelog-world.ja.json";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.UI.GritGateTerminalScreenMessage");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target type.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "Activate", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Activate() not found.", Context);
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

        if (!string.Equals(message, AlarmMessage, StringComparison.Ordinal))
        {
            return false;
        }

        var translated = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
            message,
            AlarmContext,
            DictionaryFile);
        if (string.IsNullOrEmpty(translated) || string.Equals(translated, message, StringComparison.Ordinal))
        {
            return false;
        }

        var translatedText = translated!;
        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".ConstructorDelegateAlarm",
            message,
            translatedText);
        message = MessageFrameTranslator.MarkDirectTranslation(translatedText);
        return true;
    }
}
