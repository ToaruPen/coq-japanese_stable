using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PowerEntryRequirementPopupTranslationPatch
{
    private const string Context = nameof(PowerEntryRequirementPopupTranslationPatch);
    private static readonly Regex AlreadyHaveSkillPattern = new(
        "^You may not learn this skill if you already have (?<entry>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HaveEntryPattern = new(
        "^You may not learn this skill if you have (?<entry>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex UntilHaveEntryPattern = new(
        "^You may not learn this skill until you have (?<entry>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AttributeRequirementPattern = new(
        "^Your (?<attribute>.+?) isn't high enough to buy (?<entry>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, string> AttributeNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "筋力",
            ["Toughness"] = "頑健",
            ["Willpower"] = "意志力",
            ["Agility"] = "敏捷",
            ["Ego"] = "自我",
            ["Intelligence"] = "知力",
        };

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} GameObject type not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Skills.PowerEntry", "MeetsRequirements", new[] { gameObjectType, typeof(bool) });
        AddTarget(targets, "XRL.World.Skills.PowerEntryRequirement", "MeetsRequirement", new[] { gameObjectType, typeof(bool) });
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

        if (TryTranslatePrerequisitePopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePrerequisitePopup(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = AlreadyHaveSkillPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"すでに{TranslateEntry(match, spans)}を習得しているため、このスキルは習得できない。";
            return true;
        }

        match = HaveEntryPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{TranslateEntry(match, spans)}を持っているため、このスキルは習得できない。";
            return true;
        }

        match = UntilHaveEntryPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{TranslateEntry(match, spans)}を習得するまで、このスキルは習得できない。";
            return true;
        }

        match = AttributeRequirementPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{TranslateEntry(match, spans)}を習得するには{TranslateAttribute(match, spans)}が足りない！";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateEntry(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["entry"];
        var entry = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        try
        {
            var translated = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors(
                entry,
                Context,
                recordTransform: false);
            return translated.changed ? translated.translated : entry;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: {0} skill/power name lookup failed for {1}: {2}", Context, entry, ex);
            return entry;
        }
    }

    private static string TranslateAttribute(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["attribute"];
        var visible = group.Value.Trim();
        var translated = AttributeNames.TryGetValue(visible, out var mapped) ? mapped : visible;
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
