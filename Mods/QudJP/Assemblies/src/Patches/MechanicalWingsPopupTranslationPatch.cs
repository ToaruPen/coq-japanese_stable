using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MechanicalWingsPopupTranslationPatch
{
    private const string Context = nameof(MechanicalWingsPopupTranslationPatch);
    private const string StartupFamily = "MechanicalWingsStartup";
    private const string UnresponsiveFamily = "MechanicalWingsUnresponsive";

    private static readonly Regex StatusPattern = new(
        "^(?:The |the |A |a |An |an )?(?<subject>.+?) (?:is|are) (?<extra>still starting up|unresponsive)(?<endmark>[.!])?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.MechanicalWings");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "TryStartup", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.TryStartup target not found.", Context);
            return targets;
        }

        targets.Add(method);
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

        if (!TryTranslateCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + GetFamilySuffix(source), source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StatusPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["subject"].Value,
            spans,
            match.Groups["subject"]).Trim();
        var extra = match.Groups["extra"].Value;
        var endmark = match.Groups["endmark"].Success ? match.Groups["endmark"].Value : null;
        return MessageFrameTranslator.TryTranslateXDidY(subject, "are", extra, endmark, out translated);
    }

    private static string GetFamilySuffix(string source)
    {
        var stripped = ColorAwareTranslationComposer.GetVisibleText(source);
        return stripped.Contains("still starting up")
            ? StartupFamily
            : UnresponsiveFamily;
    }
}
