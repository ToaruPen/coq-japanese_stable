using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicAmputateLimbTranslationPatch
{
    private const string Context = nameof(PhysicAmputateLimbTranslationPatch);

    private static readonly Regex NoLimbsPattern = new(
        "^(?<subject>.+?) (?:has|have) no limbs\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Skill.Physic_AmputateLimb");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.FireEvent(Event) target not found.", Context);
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
        var match = NoLimbsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{NormalizeSubject(RestoreCapture(match, spans, "subject"))}には四肢がない",
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, family + "." + Context + ".NoLimbs", source, translated);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string NormalizeSubject(string value)
    {
        var direct = StringHelpers.StripLeadingEnglishArticle(
            value.Trim(),
            includeCapitalizedDefiniteArticle: true);
        if (!string.Equals(direct, value, StringComparison.Ordinal))
        {
            return IsSecondPersonSubject(direct) ? "あなた" : direct;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(value);
        var withoutArticle = StringHelpers.StripLeadingEnglishArticle(
            visible,
            includeCapitalizedDefiniteArticle: true);
        var replacement = IsSecondPersonSubject(visible) || IsSecondPersonSubject(withoutArticle)
            ? "あなた"
            : withoutArticle;

        return string.Equals(replacement, visible, StringComparison.Ordinal)
            ? value
            : ColorAwareTranslationComposer.TranslatePreservingColors(value, _ => replacement);
    }

    private static bool IsSecondPersonSubject(string value)
    {
        return string.Equals(value, "You", StringComparison.OrdinalIgnoreCase);
    }
}
