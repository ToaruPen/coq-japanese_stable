using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RealityStabilizedInterdictTranslationPatch
{
    private const string Context = nameof(RealityStabilizedInterdictTranslationPatch);
    private const string NormalityBase = "You cannot alter spacetime through the normality lattice in the local region";
    private const string DualBase =
        "You cannot alter spacetime through either the normality lattice in your local region or the local region you're trying to interact with";

    private static readonly Regex PurposeSuffixPattern = new Regex(
        "^(.*), in order to (.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.RealityStabilized");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "ShowGenericInterdictMessage", gameObjectType, eventType);
        AddTarget(targets, targetType, "ShowDistantInterdictMessage", gameObjectType, eventType);
        AddTarget(targets, targetType, "ShowDualInterdictMessage", gameObjectType, eventType);
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0
            || string.IsNullOrEmpty(source)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _)
            || !TryTranslateCore(source, out translated))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string name, params Type[] parameters)
    {
        var method = AccessTools.Method(targetType, name, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1} target not found.", Context, name);
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var baseText = source;
        string? purpose = null;
        var purposeMatch = PurposeSuffixPattern.Match(source);
        if (purposeMatch.Success)
        {
            baseText = purposeMatch.Groups[1].Value;
            purpose = purposeMatch.Groups[2].Value;
        }
        else if (baseText.EndsWith(".", StringComparison.Ordinal))
        {
            baseText = baseText.Substring(0, baseText.Length - 1);
        }

        string? translatedBase = null;
        if (string.Equals(baseText, NormalityBase, StringComparison.Ordinal))
        {
            translatedBase = "局所領域のノーマリティ格子により、時空を変えることはできない";
        }
        else if (string.Equals(baseText, DualBase, StringComparison.Ordinal))
        {
            translatedBase = "あなたの局所領域か干渉しようとしている局所領域のノーマリティ格子により、時空を変えることはできない";
        }

        if (translatedBase is null)
        {
            translated = source;
            return false;
        }

        translated = purpose is null
            ? translatedBase + "。"
            : translatedBase + "。目的: " + purpose + "。";
        return true;
    }
}
