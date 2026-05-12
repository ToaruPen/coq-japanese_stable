using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FloatingEquipmentPopupTranslationPatch
{
    private const string Context = nameof(FloatingEquipmentPopupTranslationPatch);
    private const string CeaseFamily = "FloatingEquipmentCease";
    private const string FallFamily = "FloatingEquipmentFall";

    private static readonly Regex FloatingStatusPattern = new(
        "^(?:The |the |A |a |An |an )?(?<subject>.+?) (?<verb>ceases?|falls?) (?<extra>floating near you|to the ground(?:; you (?:pick|scoop) .+? up)?)(?<endmark>[.!])?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        AddTarget(targets, "XRL.World.Parts.PoweredFloating", "CheckFloating");
        AddTarget(targets, "XRL.World.Parts.ModMagnetized", "CheckFloating");
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

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = FloatingStatusPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["subject"].Value,
            spans,
            match.Groups["subject"]).Trim();
        var verb = match.Groups["verb"].Value.StartsWith("cease", StringComparison.Ordinal) ? "cease" : "fall";
        var extra = match.Groups["extra"].Value;
        var endmark = match.Groups["endmark"].Success ? match.Groups["endmark"].Value : null;
        return MessageFrameTranslator.TryTranslateXDidY(subject, verb, extra, endmark, out translated);
    }

    private static string GetFamilySuffix(string source)
    {
        var stripped = ColorAwareTranslationComposer.GetVisibleText(source);
        return stripped.Contains("floating near you") ? CeaseFamily : FallFamily;
    }
}
