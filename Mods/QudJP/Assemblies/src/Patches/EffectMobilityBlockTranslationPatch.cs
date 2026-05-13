using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EffectMobilityBlockTranslationPatch
{
    private const string Context = nameof(EffectMobilityBlockTranslationPatch);
    private static readonly Regex MobilityBlockPattern = new(
        "^You are (?<status>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} Event type not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Effects.Immobilized", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.Stuck", "FireEvent", new[] { eventType });
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (!TryTranslateMobilityBlockMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateMobilityBlockMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateMobilityBlockMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var normalizedSource = source.Replace("&y!", "!");
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(normalizedSource);
        var match = MobilityBlockPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var statusGroup = match.Groups["status"];
        if (!TryTranslateStatus(statusGroup.Value, out var status, out var suffix))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(status, spans, statusGroup) + suffix;
        return true;
    }

    private static bool TryTranslateStatus(string source, out string translated, out string suffix)
    {
        switch (source.Trim())
        {
            case "immobilized":
                translated = "移動不能";
                suffix = "だ！";
                return true;
            case "stuck":
                translated = "拘束";
                suffix = "されている！";
                return true;
            default:
                translated = string.Empty;
                suffix = string.Empty;
                return false;
        }
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
