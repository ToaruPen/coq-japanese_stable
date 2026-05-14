using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BrainBrineCurseTranslationPatch
{
    private const string Context = nameof(BrainBrineCurseTranslationPatch);

    private static readonly Regex SkillPattern = new(
        "^You learn the skill (?<name>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MutationPattern = new(
        "^You gained the mutation (?<name>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DefectPattern = new(
        "^You gained the defect (?<name>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.BrainBrineCurse");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "GainChoice", [typeof(string)]);
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.GainChoice target not found.", Context, targetType.FullName);
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
        _ = family;
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslateCore(source, stripped, spans, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".RewardPopup",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var skillMatch = SkillPattern.Match(stripped);
        if (skillMatch.Success)
        {
            translated = $"{TranslateSkillName(skillMatch, spans)}を習得した！";
            return true;
        }

        var mutationMatch = MutationPattern.Match(stripped);
        if (mutationMatch.Success)
        {
            translated = $"変異{TranslateMutationName(mutationMatch, spans)}を得た！";
            return true;
        }

        var defectMatch = DefectPattern.Match(stripped);
        if (defectMatch.Success)
        {
            translated = $"欠陥{TranslateMutationName(defectMatch, spans)}を得た！";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateSkillName(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var source = Restore(match, spans, "name");
        var translated = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors(
            source,
            Context,
            recordTransform: false);
        return translated.changed ? translated.translated : source;
    }

    private static string TranslateMutationName(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return StatusScreenPopupTranslationPatch.TranslateMutationDisplayName(Restore(match, spans, "name"));
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
