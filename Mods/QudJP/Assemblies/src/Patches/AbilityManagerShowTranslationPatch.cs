using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbilityManagerShowTranslationPatch
{
    private const string Context = nameof(AbilityManagerShowTranslationPatch);

    private static readonly Regex CooldownPattern = new(
        "^You must wait (?<duration>.+?) to use that ability again\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var targetType = AccessTools.TypeByName("XRL.UI.AbilityManager");
        if (gameObjectType is null || targetType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "Show", new[] { gameObjectType });
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.Show(GameObject) target not found.", Context);
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        var match = CooldownPattern.Match(message);
        if (match.Success)
        {
            var duration = match.Groups["duration"].Value;
            string cooldownTranslated;
            if (ActivatedAbilityCooldownTranslator.TryStripDirectMarkedCooldownDuration(
                    duration,
                    out var directMarkedCooldown))
            {
                cooldownTranslated = directMarkedCooldown;
            }
            else if (ActivatedAbilityCooldownTranslator.TryTranslateRawCooldown(duration, out var nestedRawCooldown))
            {
                cooldownTranslated = nestedRawCooldown;
            }
            else
            {
                cooldownTranslated = $"その能力を再び使うには{ActivatedAbilityCooldownTranslator.TranslateCooldownDuration(duration)}待つ必要がある。";
            }

            DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, cooldownTranslated);
            message = MessageFrameTranslator.MarkDirectTranslation(cooldownTranslated);
            return true;
        }

        if (ActivatedAbilityCooldownTranslator.TryTranslateRawCooldown(
            message,
            "MessageQueue.AddPlayerMessage",
            Context + ".NotUsableDescription",
            out var rawCooldown))
        {
            message = MessageFrameTranslator.MarkDirectTranslation(rawCooldown);
            return true;
        }

        return false;
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

        return ActivatedAbilityCooldownTranslator.TryTranslateRawCooldown(
            source,
            route,
            family + "." + Context + ".NotUsableDescription",
            out translated);
    }
}
