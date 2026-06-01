using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsOnboardRecoilerPopupTranslationPatch
{
    private const string Context = nameof(CyberneticsOnboardRecoilerPopupTranslationPatch);
    internal const string Family = "CyberneticsOnboardRecoilerCooldown";
    internal const string SourcePrompt = "You can't recoil yet.";
    internal const string TranslatedPrompt = "まだリコイルできない。";

    [ThreadStatic]
    private static int activeDepth;
    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsOnboardRecoilerTeleporter");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var iEventType = AccessTools.TypeByName("XRL.World.IEvent");
        var actuateTeleport = targetType is null || gameObjectType is null || iEventType is null
            ? null
            : AccessTools.Method(targetType, "ActuateTeleport", [gameObjectType, iEventType]);
        if (actuateTeleport is null)
        {
            Trace.TraceError("QudJP: {0}.ActuateTeleport target not found.", Context);
        }
        else
        {
            targets.Add(actuateTeleport);
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
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(
                source,
                ref directMarkerPassThroughText,
                out translated))
        {
            return true;
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
