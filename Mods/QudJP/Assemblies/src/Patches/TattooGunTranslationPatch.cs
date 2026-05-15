using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TattooGunTranslationPatch
{
    private const string Context = nameof(TattooGunTranslationPatch);

    private static readonly Regex MarkOfDeathPattern = new(
        "^You tattoo the mark of death on (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TattooPattern = new(
        "^You tattoo (?<tattoo>.+?) on (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var targetType = AccessTools.TypeByName("XRL.World.Parts.TattooGun");
        if (gameObjectType is null || targetType is null)
        {
            Trace.TraceError("QudJP: {0} target or GameObject type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "AttemptTattoo", [gameObjectType]);
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.AttemptTattoo target not found.", Context, targetType.FullName);
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

        if (!TryTranslateTattooMessage(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".SuccessPopup",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateTattooMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var markMatch = MarkOfDeathPattern.Match(stripped);
        if (markMatch.Success)
        {
            translated = $"あなたは{TranslateTarget(markMatch, spans)}に死の印を入れ墨した。";
            return true;
        }

        var tattooMatch = TattooPattern.Match(stripped);
        if (!tattooMatch.Success)
        {
            translated = source;
            return false;
        }

        translated = $"あなたは{TranslateTarget(tattooMatch, spans)}に{Restore(tattooMatch, spans, "tattoo")}を入れ墨した。";
        return true;
    }

    private static string TranslateTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var target = Restore(match, spans, "target");
        return target.StartsWith("your ", StringComparison.Ordinal)
            ? $"あなたの{target.Substring("your ".Length)}"
            : target;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
