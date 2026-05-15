using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EngraverTranslationPatch
{
    private const string Context = nameof(EngraverTranslationPatch);
    private static readonly Regex MarkOfDeathPattern = new(
        "^You engrave the mark of death on (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EngravingPattern = new(
        "^You engrave (?<engraving>.+?) on (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Engraver");
        if (gameObjectType is null || targetType is null)
        {
            Trace.TraceError("QudJP: {0} target or GameObject type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "AttemptEngrave", new[] { gameObjectType });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.AttemptEngrave target not found.", Context, targetType.FullName);
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
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateEngraveMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateEngraveMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var markMatch = MarkOfDeathPattern.Match(stripped);
        if (markMatch.Success)
        {
            translated = $"あなたは{TranslateTarget(markMatch, spans)}に死の印を刻んだ。";
            return true;
        }

        var engravingMatch = EngravingPattern.Match(stripped);
        if (!engravingMatch.Success)
        {
            translated = source;
            return false;
        }

        translated = $"あなたは{TranslateTarget(engravingMatch, spans)}に{RestoreCapture(engravingMatch, spans, "engraving")}を刻んだ。";
        return true;
    }

    private static string TranslateTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var target = RestoreCapture(match, spans, "target");
        return target.StartsWith("your ", StringComparison.Ordinal)
            ? $"あなたの{target.Substring("your ".Length)}"
            : target;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
