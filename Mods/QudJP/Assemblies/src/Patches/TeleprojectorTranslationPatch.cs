using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TeleprojectorTranslationPatch
{
    private const string Context = nameof(TeleprojectorTranslationPatch);

    private static readonly Regex AttunePattern = new(
        "^(?<device>.+?) attunes? to your physiology\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NothingToUplinkPattern = new(
        "^There is nothing there that (?<device>.+?) can uplink with\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TakeControlPattern = new(
        "^You take control of (?<target>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Teleprojector");
        var bootSequenceDoneEventType = AccessTools.TypeByName("XRL.World.BootSequenceDoneEvent");
        var mentalAttackEventType = AccessTools.TypeByName("XRL.World.MentalAttackEvent");
        if (targetType is null || bootSequenceDoneEventType is null || mentalAttackEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "HandleEvent", new[] { bootSequenceDoneEventType });
        AddTarget(targets, targetType, "ActivateTeleprojector", Type.EmptyTypes);
        AddTarget(targets, targetType, "RoboDom", new[] { mentalAttackEventType });
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
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslatePattern(
                AttunePattern,
                source,
                "device",
                device => $"{device}があなたの生理機能に同調した。",
                out translated)
            && !TryTranslatePattern(
                NothingToUplinkPattern,
                source,
                "device",
                device => $"そこには{device}がアップリンクできるものが何もない。",
                out translated)
            && !TryTranslatePattern(
                TakeControlPattern,
                source,
                "target",
                target => $"{target}を支配した！",
                out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        string groupName,
        Func<string, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var group = match.Groups[groupName];
        var value = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(value),
            spans,
            stripped.Length,
            source);
        return true;
    }
}
