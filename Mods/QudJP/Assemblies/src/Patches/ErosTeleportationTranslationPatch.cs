using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ErosTeleportationTranslationPatch
{
    private const string Context = nameof(ErosTeleportationTranslationPatch);

    private static readonly Regex ParticlePattern = new(
        @"^I'm coming, (?<leader>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YellMessagePattern = new(
        @"^E-Ros yells, {{W\|'I'm coming, (?<leader>.+)!'\}}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.ErosTeleportation");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        if (targetType is null || eventType is null || cellType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "Cast", [targetType, typeof(string), eventType, cellType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Cast(ErosTeleportation, string, Event, Cell) target not found.", Context);
            yield break;
        }

        yield return method;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        var match = YellMessagePattern.Match(message);
        if (!match.Success)
        {
            return false;
        }

        var translated = "E-Rosは{{W|「今行くよ、" + match.Groups["leader"].Value + "！」}}と叫んだ";
        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".Yell",
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

    internal static bool TryTranslateParticleText(ref string text)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var match = ParticlePattern.Match(text);
        if (!match.Success)
        {
            return false;
        }

        var translated = "今行くよ、" + match.Groups["leader"].Value + "！";
        DynamicTextObservability.RecordTransform(
            "GameObject.ParticleText",
            Context + ".FloatingSpeech",
            text,
            translated);
        text = translated;
        return true;
    }
}
