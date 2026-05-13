using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PetGloamingTranslationPatch
{
    private const string Context = nameof(PetGloamingTranslationPatch);
    private static readonly Regex AstralTetherPattern = new(
        "^(?:The |the |A |a |An |an )?(?<owner>.+?)'s astral tether snaps and (?<possessive>.+?) binal specter substantiates as (?<specter>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WisdomRevealPattern = new(
        "^(?:The |the |A |a |An |an )?(?<owner>.+?) beats? (?<possessive>.+?) wings, and the shattered voices of a trillion worlds ride the current of air and harmonize into one, revealing the following wisdom:\\n\\n(?<wisdom>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StopGleamingPattern = new(
        "^(?:The |the |A |a |An |an )?(?<owner>.+?) stops? gleaming\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StartGleamingPattern = new(
        "^(?:The |the |A |a |An |an )?(?<owner>.+?) starts? to gleam with an (?<light>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} Event type not found.", Context);
            return targets;
        }

        var targetType = AccessTools.TypeByName("XRL.World.Parts.PetGloaming");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "FireEvent", new[] { eventType });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.FireEvent target not found.", Context, targetType.FullName);
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (!TryTranslatePetGloamingMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

        if (TryTranslatePetGloamingMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePetGloamingMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        return TryTranslateAstralTether(source, out translated)
            || TryTranslateWisdomReveal(source, out translated)
            || TryTranslateStopGleaming(source, out translated)
            || TryTranslateStartGleaming(source, out translated);
    }

    private static bool TryTranslateAstralTether(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = AstralTetherPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "owner")}の星幽の繋ぎ紐が切れ、二元の幻影が{RestoreCapture(match, spans, "specter")}として実体化した。";
        return true;
    }

    private static bool TryTranslateWisdomReveal(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = WisdomRevealPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "owner")}は翼を羽ばたかせた。砕けた一兆の世界の声が気流に乗って一つに調和し、次の叡智を明かした:\n\n{RestoreCapture(match, spans, "wisdom")}";
        return true;
    }

    private static bool TryTranslateStopGleaming(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StopGleamingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "owner")}は輝くのをやめた。";
        return true;
    }

    private static bool TryTranslateStartGleaming(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StartGleamingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "owner")}は{TranslateLight(match, spans)}で輝き始めた。";
        return true;
    }

    private static string TranslateLight(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["light"];
        var translated = string.Equals(group.Value, "unearthly light", StringComparison.Ordinal)
            ? "この世ならぬ光"
            : group.Value;
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
