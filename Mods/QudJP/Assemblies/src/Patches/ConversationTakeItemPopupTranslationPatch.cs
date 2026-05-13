using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ConversationTakeItemPopupTranslationPatch
{
    private const string Context = nameof(ConversationTakeItemPopupTranslationPatch);
    private const string CannotGivePrefix = "You cannot give ";
    private const string TakeVerb = " takes ";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Conversations.Parts.TakeItem");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "Execute", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Execute() not found.", Context);
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

        if (TryTranslateCannotGive(source, out translated)
            || TryTranslateTakeSuccess(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCannotGive(string source, out string translated)
    {
        if (!source.StartsWith(CannotGivePrefix, StringComparison.Ordinal)
            || !source.EndsWith("!", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var item = source.Substring(CannotGivePrefix.Length, source.Length - CannotGivePrefix.Length - 1);
        translated = $"{item}を渡せない！";
        return true;
    }

    private static bool TryTranslateTakeSuccess(string source, out string translated)
    {
        if (!source.EndsWith(".", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var separator = source.IndexOf(TakeVerb, StringComparison.Ordinal);
        if (separator <= 0)
        {
            translated = source;
            return false;
        }

        var actor = source.Substring(0, separator);
        var itemStart = separator + TakeVerb.Length;
        var item = source.Substring(itemStart, source.Length - itemStart - 1);
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(item))
        {
            translated = source;
            return false;
        }

        translated = $"{actor}は{item}を受け取った。";
        return true;
    }
}
