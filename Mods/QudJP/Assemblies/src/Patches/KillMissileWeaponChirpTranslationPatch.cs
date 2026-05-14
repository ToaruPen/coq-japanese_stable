using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class KillMissileWeaponChirpTranslationPatch
{
    private const string Context = nameof(KillMissileWeaponChirpTranslationPatch);

    private static readonly Regex ChirpPattern = new(
        "^Something chirps (?<direction>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.AI.GoalHandlers.Kill");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "TryMissileWeapon", Type.EmptyTypes);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.TryMissileWeapon target not found.", Context);
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

        if (!TryTranslateChirp(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateChirp(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ChirpPattern.Match(stripped);
        if (!match.Success || !TryTranslateDirection(match.Groups["direction"], spans, out var direction))
        {
            translated = source;
            return false;
        }

        translated = $"{direction}で何かが鳴いた。";
        return true;
    }

    private static bool TryTranslateDirection(Group group, IReadOnlyList<ColorSpan> spans, out string translated)
    {
        translated = group.Value switch
        {
            "here" => "ここ",
            "to the north" => "北側",
            "to the south" => "南側",
            "to the east" => "東側",
            "to the west" => "西側",
            "to the northeast" => "北東側",
            "to the northwest" => "北西側",
            "to the southeast" => "南東側",
            "to the southwest" => "南西側",
            _ => string.Empty,
        };

        if (translated.Length > 0)
        {
            return true;
        }

        _ = spans;
        return false;
    }
}
