using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FungalSporeInfectionTranslationPatch
{
    private const string Context = nameof(FungalSporeInfectionTranslationPatch);

    private static readonly Regex ContractedPattern = new(
        "^You've contracted (?<infection>.+?) on your (?<part>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YourSporeCloudPattern = new(
        "^Your (?<part>.+?) spews? a cloud of spores\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VisibleSubjectSporeCloudPattern = new(
        "^(?<subject>.+?) (?<part>.+?) spews? a cloud of spores\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.FungalSporeInfection");
        var gasFungalSporesType = AccessTools.TypeByName("XRL.World.Parts.GasFungalSpores");
        var paxType = AccessTools.TypeByName("XRL.World.Parts.PaxInfection");
        var puffType = AccessTools.TypeByName("XRL.World.Parts.PuffInfection");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var bodyPartType = AccessTools.TypeByName("XRL.World.Anatomy.BodyPart");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        if (gameObjectType is not null && bodyPartType is not null)
        {
            AddTarget(targets, targetType, "ApplyFungalInfection", new[] { gameObjectType, typeof(string), bodyPartType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ApplyFungalInfection parameter type not found.", Context);
        }

        if (gameObjectType is not null)
        {
            AddTargetByTypeName(targets, gasFungalSporesType, "XRL.World.Parts.GasFungalSpores", "ApplyGas", new[] { gameObjectType });
        }

        if (eventType is not null)
        {
            AddTarget(targets, targetType, "FireEvent", new[] { eventType });
            AddTargetByTypeName(targets, paxType, "XRL.World.Parts.PaxInfection", "FireEvent", new[] { eventType });
            AddTargetByTypeName(targets, puffType, "XRL.World.Parts.PuffInfection", "FireEvent", new[] { eventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.FireEvent Event type not found.", Context);
        }

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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        try
        {
            if (activeDepth <= 0 || string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
            {
                message = markedText;
                return true;
            }

            if (!TryTranslateQueuedCore(message, out var translated))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
            message = MessageFrameTranslator.MarkDirectTranslation(translated);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TryTranslateQueuedMessage failed: {1}", Context, ex);
            return false;
        }
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        translated = source;
        try
        {
            if (activeDepth <= 0 || string.IsNullOrEmpty(source))
            {
                return false;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
            {
                translated = markedText;
                return true;
            }

            if (!TryTranslatePopupCore(source, out translated))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TryTranslatePopupMessage failed: {1}", Context, ex);
            translated = source;
            return false;
        }
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

    private static void AddTargetByTypeName(
        List<MethodBase> targets,
        Type? targetType,
        string typeName,
        string methodName,
        Type[] parameters)
    {
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}.", Context, typeName);
            return;
        }

        AddTarget(targets, targetType, methodName, parameters);
    }

    private static bool TryTranslateQueuedCore(string source, out string translated)
    {
        if (string.Equals(source, "Your skin itches.", StringComparison.Ordinal))
        {
            translated = "肌がむずむずする。";
            return true;
        }

        if (TryTranslateSporeCloud(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateSporeCloud(string source, out string translated)
    {
        if (TryTranslateSporeCloudPattern(
            YourSporeCloudPattern,
            source,
            (match, spans) => $"あなたの{Restore(match, spans, "part")}から胞子の雲が噴き出した。",
            out translated))
        {
            return true;
        }

        return TryTranslateSporeCloudPattern(
            VisibleSubjectSporeCloudPattern,
            source,
            (match, spans) => $"{JoinSubjectAndPart(Restore(match, spans, "subject"), Restore(match, spans, "part"))}から胞子の雲が噴き出した。",
            out translated);
    }

    private static string JoinSubjectAndPart(string subject, string part)
    {
        if (part.Length >= 2 && part[0] == '&' && char.IsLetter(part[1]))
        {
            return subject + part.Substring(0, 2) + " " + part.Substring(2);
        }

        return subject + " " + part;
    }

    private static bool TryTranslateSporeCloudPattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslatePopupCore(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ContractedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{Restore(match, spans, "part")}に{Restore(match, spans, "infection")}を発症した。",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group);
    }
}
