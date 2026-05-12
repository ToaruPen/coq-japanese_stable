using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SupplyableIntegratedHostPopupTranslationPatch
{
    private const string Context = nameof(SupplyableIntegratedHostPopupTranslationPatch);
    private const string NoNeededSuppliesFamily = "SupplyableIntegratedHostNoNeededSupplies";
    private const string NoHeldSuppliesFamily = "SupplyableIntegratedHostNoHeldSupplies";

    private static readonly Regex NoNeededSuppliesPattern = new(
        "^(?:The |the |A |a |An |an )?(?<host>.+?) needs? no supplies\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoHeldSuppliesPattern = new(
        "^You have no supplies that (?:the |The |a |A |an |An )?(?<host>.+?) needs?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.SupplyableIntegratedHost");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "AttemptSupply", new[] { gameObjectType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.AttemptSupply(GameObject) target not found.", Context);
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

        if (!TryTranslateCore(source, out translated, out var familySuffix))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + familySuffix, source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated, out string familySuffix)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var noNeededSuppliesMatch = NoNeededSuppliesPattern.Match(stripped);
        if (noNeededSuppliesMatch.Success)
        {
            var host = noNeededSuppliesMatch.Groups["host"];
            translated = ColorAwareTranslationComposer.RestoreCapture(host.Value, spans, host).Trim()
                + "は補給品を必要としていない。";
            familySuffix = NoNeededSuppliesFamily;
            return true;
        }

        var noHeldSuppliesMatch = NoHeldSuppliesPattern.Match(stripped);
        if (noHeldSuppliesMatch.Success)
        {
            var host = noHeldSuppliesMatch.Groups["host"];
            translated = ColorAwareTranslationComposer.RestoreCapture(host.Value, spans, host).Trim()
                + "が必要とする補給品を持っていない。";
            familySuffix = NoHeldSuppliesFamily;
            return true;
        }

        translated = source;
        familySuffix = string.Empty;
        return false;
    }
}
