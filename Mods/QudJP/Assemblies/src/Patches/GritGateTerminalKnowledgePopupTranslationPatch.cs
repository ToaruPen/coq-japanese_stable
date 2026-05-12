using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GritGateTerminalKnowledgePopupTranslationPatch
{
    private const string Context = nameof(GritGateTerminalKnowledgePopupTranslationPatch);
    private const string SourceHeader = "Ereshkigal delivers insight from the Thin World:\n\n";
    private const string TranslatedHeader = "エレシュキガルは薄界からの洞察を授ける:\n\n";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType(
            "XRL.UI.GritGateTerminalScreenKnowledge",
            "GritGateTerminalScreenKnowledge");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
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

        if (!source.StartsWith(SourceHeader, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var body = source.Substring(SourceHeader.Length);
        if (body.Length == 0)
        {
            translated = source;
            return false;
        }

        translated = TranslatedHeader + body;
        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }
}
