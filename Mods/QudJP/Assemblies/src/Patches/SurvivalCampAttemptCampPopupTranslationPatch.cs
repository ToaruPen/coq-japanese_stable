using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SurvivalCampAttemptCampPopupTranslationPatch
{
    private const string Context = nameof(SurvivalCampAttemptCampPopupTranslationPatch);

    private static readonly Regex ExistingCampfireNavigationPattern = new(
        "^There (?<be>is|are) already (?:(?:a|an|some|the) )?(?<campfire>.+?) (?<direction>to the north|to the south|to the east|to the west|to the northeast|to the northwest|to the southeast|to the southwest)\\. Do you want to go to (?<pronoun>it|them)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Skill.Survival_Camp");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} GameObject type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "AttemptCamp", [gameObjectType]);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.AttemptCamp target not found.", Context);
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

        if (!TryTranslateExistingCampfireNavigation(source, out translated))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".ExistingCampfireNavigation",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateExistingCampfireNavigation(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ExistingCampfireNavigationPattern.Match(stripped);
        if (!match.Success || !TryTranslateDirection(match.Groups["direction"].Value, out var direction))
        {
            translated = source;
            return false;
        }

        var campfire = RestoreCapture(match, spans, "campfire");
        translated = $"{direction}にすでに{campfire}がある。そこへ向かう？";
        return true;
    }

    private static bool TryTranslateDirection(string direction, out string translated)
    {
        translated = direction switch
        {
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

        return translated.Length > 0;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
