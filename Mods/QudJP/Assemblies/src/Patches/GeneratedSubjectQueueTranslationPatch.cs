using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GeneratedSubjectQueueTranslationPatch
{
    private const string Context = nameof(GeneratedSubjectQueueTranslationPatch);

    private static readonly Regex AttackPassesPattern = CreatePattern(
        "^(?<attacker>.+?)(?:'s|') attack passes harmlessly through (?<target>.+?)\\.$");

    private static readonly Regex MolecularCannonOfflinePattern = CreatePattern(
        "^(?<owner>.+?)(?:'s|') molecular cannon goes offline\\.$");

    private static readonly Regex StartsToFlickerPattern = CreatePattern(
        "^(?<subject>.+?) starts to flicker\\.$");

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var beforeApplyDamageEventType = AccessTools.TypeByName("XRL.World.BeforeApplyDamageEvent");
        if (beforeApplyDamageEventType is null)
        {
            Trace.TraceError("QudJP: {0}.HologramInvulnerability.HandleEvent parameter type not found.", Context);
        }
        else
        {
            foreach (var target in ResolveTarget(
                         "XRL.World.Parts.HologramInvulnerability",
                         "HandleEvent",
                         new[] { beforeApplyDamageEventType }))
            {
                yield return target;
            }
        }

        foreach (var target in ResolveTarget(
                     "XRL.World.Parts.Mutation.Decarbonizer",
                     "ShutDownTargeting",
                     Type.EmptyTypes))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget(
                     "XRL.World.Parts.PetEitherOr",
                     "trigger",
                     Type.EmptyTypes))
        {
            yield return target;
        }
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(message);
        if (!TryTranslate(stripped, spans, message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}.", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslate(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var attackMatch = AttackPassesPattern.Match(stripped);
        if (attackMatch.Success)
        {
            translated = RestoreWholeSourceBoundary(
                $"{Capture(attackMatch, spans, "attacker", stripped.Length)}の攻撃は{Capture(attackMatch, spans, "target", stripped.Length)}を無害に通り抜けた。",
                spans,
                stripped,
                source);
            return true;
        }

        var cannonMatch = MolecularCannonOfflinePattern.Match(stripped);
        if (cannonMatch.Success)
        {
            translated = RestoreWholeSourceBoundary(
                $"{Capture(cannonMatch, spans, "owner", stripped.Length)}の分子砲がオフラインになった。",
                spans,
                stripped,
                source);
            return true;
        }

        var flickerMatch = StartsToFlickerPattern.Match(stripped);
        if (flickerMatch.Success)
        {
            translated = RestoreWholeSourceBoundary(
                $"{Capture(flickerMatch, spans, "subject", stripped.Length)}がちらつき始めた。",
                spans,
                stripped,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    private static string RestoreWholeSourceBoundary(
        string visible,
        IReadOnlyList<ColorSpan> spans,
        string stripped,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            visible,
            spans,
            stripped.Length,
            source);
    }

    private static string Capture(Match match, IReadOnlyList<ColorSpan> spans, string groupName, int sourceLength)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(
            group.Value,
            WithoutWholeSourceBoundarySpans(spans, sourceLength),
            group).Trim();
    }

    private static IReadOnlyList<ColorSpan> WithoutWholeSourceBoundarySpans(IReadOnlyList<ColorSpan> spans, int sourceLength)
    {
        var hasWholeSourceBoundary = false;
        for (var index = 0; index < spans.Count; index++)
        {
            if (spans[index].Index == sourceLength)
            {
                hasWholeSourceBoundary = true;
                break;
            }
        }

        if (!hasWholeSourceBoundary)
        {
            return spans;
        }

        var filtered = new List<ColorSpan>();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index == 0 || span.Index == sourceLength)
            {
                continue;
            }

            filtered.Add(span);
        }

        return filtered;
    }

    private static Regex CreatePattern(string pattern)
    {
        return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
