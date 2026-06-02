using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsLowLevelHackPopupTranslationPatch
{
    private const string Context = nameof(CyberneticsLowLevelHackPopupTranslationPatch);
    internal const string Family = "CyberneticsLowLevelHackPrompt";
    internal const string SourcePrompt =
        "Do you want to use a low-level hack? Low-level hacks make it more difficult to read the terminal output but reduce the chance of triggering security alerts.";
    internal const string TranslatedPrompt =
        "低レベルハックを使用しますか？低レベルハックを使用すると端末出力の解読が難しくなるが、セキュリティ警報を作動させる可能性が下がる。";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsTerminal2");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var askLowLevelHack = targetType is null || gameObjectType is null
            ? null
            : AccessTools.Method(targetType, "AskLowLevelHack", [gameObjectType]);
        if (askLowLevelHack is null)
        {
            Trace.TraceError("QudJP: {0}.AskLowLevelHack target not found.", Context);
        }
        else
        {
            targets.Add(askLowLevelHack);
        }

        return targets;
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
        _ = family;
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!string.Equals(stripped, SourcePrompt, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.Restore(TranslatedPrompt, spans);
        DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Family, source, translated);
        return true;
    }
}
