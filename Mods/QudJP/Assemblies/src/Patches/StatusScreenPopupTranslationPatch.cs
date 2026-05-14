using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StatusScreenPopupTranslationPatch
{
    private const string Context = nameof(StatusScreenPopupTranslationPatch);

    private static readonly Regex CurrentStatPattern = new(
        "^Your (?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence) is (?<value>.+?)\\.(?:\\n\\n(?<tail>[\\s\\S]+))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ModifiedStatPattern = new(
        "^Your base (?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence) is (?<base>.+?), modified to (?<value>.+?)\\.(?:\\n\\n(?<tail>[\\s\\S]+))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttributeCostTailPattern = new(
        "^It will cost (?<cost>.+?) attribute point to increase (?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence) by 1\\.\\nDo you wish to increase this attribute\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IncreasedStatPattern = new(
        "^You have increased your (?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence) to (?<value>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GainedMutationPattern = new(
        "^You gain (?<name>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllAvailableMutationTermPattern = new(
        "^You have all available (?<term>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PsychicGlimmerDebugPattern = new(
        "^TODOJASON GLIMMER=(?<value>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly object SyncRoot = new();
    private static Dictionary<string, string>? mutationDisplayNames;

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var statusScreenType = AccessTools.TypeByName("XRL.UI.StatusScreen");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (statusScreenType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, statusScreenType, "BuyStat", new[] { gameObjectType, typeof(string) });
        AddTarget(targets, statusScreenType, "BuyRandomMutation", new[] { gameObjectType });
        AddTarget(targets, statusScreenType, "Show", new[] { gameObjectType });
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

    internal static void ResetForTests()
    {
        activeDepth = 0;
        lock (SyncRoot)
        {
            mutationDisplayNames = null;
        }
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
        if (!TryTranslateCore(source, stripped, spans, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslateCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        if (TryTranslateAttributePopup(source, stripped, spans, out translated))
        {
            return true;
        }

        if (TryTranslateIncreasedStat(source, stripped, spans, out translated))
        {
            return true;
        }

        if (TryTranslateGainedMutation(source, stripped, spans, out translated))
        {
            return true;
        }

        if (TryTranslateAllAvailableMutationTerm(source, stripped, spans, out translated))
        {
            return true;
        }

        if (TryTranslatePsychicGlimmerDebug(source, stripped, spans, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateAttributePopup(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var currentMatch = CurrentStatPattern.Match(stripped);
        if (currentMatch.Success)
        {
            if (TryTranslateCurrentStat(currentMatch, spans, out var currentTranslated))
            {
                translated = RestoreWhole(
                    currentTranslated,
                    source,
                    stripped,
                    spans);
                return true;
            }

            translated = source;
            return false;
        }

        var modifiedMatch = ModifiedStatPattern.Match(stripped);
        if (modifiedMatch.Success)
        {
            if (TryTranslateModifiedStat(modifiedMatch, spans, out var modifiedTranslated))
            {
                translated = RestoreWhole(
                    modifiedTranslated,
                    source,
                    stripped,
                    spans);
                return true;
            }

            translated = source;
            return false;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCurrentStat(Match match, IReadOnlyList<ColorSpan> spans, out string translated)
    {
        var stat = TranslateStat(match.Groups["stat"].Value);
        var value = Restore(match, spans, "value");
        if (!TryTranslateAttributeTail(match.Groups["tail"].Value, match.Groups["tail"].Index, spans, out var tail))
        {
            translated = string.Empty;
            return false;
        }

        translated = tail.Length == 0
            ? $"{stat}は{value}。"
            : $"{stat}は{value}。\n\n{tail}";
        return true;
    }

    private static bool TryTranslateModifiedStat(Match match, IReadOnlyList<ColorSpan> spans, out string translated)
    {
        var stat = TranslateStat(match.Groups["stat"].Value);
        var baseValue = Restore(match, spans, "base");
        var value = Restore(match, spans, "value");
        if (!TryTranslateAttributeTail(match.Groups["tail"].Value, match.Groups["tail"].Index, spans, out var tail))
        {
            translated = string.Empty;
            return false;
        }

        var heading = $"{stat}の基本値は{baseValue}で、{value}に修正されている。";
        translated = tail.Length == 0 ? heading : $"{heading}\n\n{tail}";
        return true;
    }

    private static bool TryTranslateAttributeTail(
        string tail,
        int tailStartIndex,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        if (tail.Length == 0)
        {
            translated = string.Empty;
            return true;
        }

        if (string.Equals(tail, "You may not raise an attribute above 100.", StringComparison.Ordinal))
        {
            translated = "属性を100より高く上げることはできない。";
            return true;
        }

        if (string.Equals(tail, "You have no attribute points to raise this attribute.", StringComparison.Ordinal))
        {
            translated = "この属性を上げるための属性ポイントがない。";
            return true;
        }

        var costMatch = AttributeCostTailPattern.Match(tail);
        if (costMatch.Success)
        {
            var stat = TranslateStat(costMatch.Groups["stat"].Value);
            var costGroup = costMatch.Groups["cost"];
            var cost = ColorAwareTranslationComposer.RestoreSlice(
                costGroup.Value,
                spans,
                tailStartIndex + costGroup.Index,
                costGroup.Length);
            translated = $"{stat}を1上げるには属性ポイントが{cost}ポイント必要だ。\nこの属性を上げますか？";
            return true;
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryTranslateIncreasedStat(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = IncreasedStatPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"{TranslateStat(match.Groups["stat"].Value)}を{Restore(match, spans, "value")}に上げた！",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateGainedMutation(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        const string prefix = "You gain ";
        const string suffix = "!";
        if (source.StartsWith(prefix, StringComparison.Ordinal) && source.EndsWith(suffix, StringComparison.Ordinal))
        {
            var sourceName = source.Substring(prefix.Length, source.Length - prefix.Length - suffix.Length);
            var sourceTranslatedName = TranslateMutationName(sourceName);
            translated = $"{sourceTranslatedName}を得た！";
            return true;
        }

        var sourceMatch = GainedMutationPattern.Match(source);
        if (sourceMatch.Success)
        {
            var sourceName = sourceMatch.Groups["name"].Value;
            var sourceTranslatedName = TranslateMutationName(sourceName);
            translated = $"{sourceTranslatedName}を得た！";
            return true;
        }

        var match = GainedMutationPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var name = TranslateMutationName(Restore(match, spans, "name"));
        translated = RestoreWhole($"{name}を得た！", source, stripped, spans);
        return true;
    }

    private static bool TryTranslateAllAvailableMutationTerm(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        _ = spans;
        var match = AllAvailableMutationTermPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"利用可能な{TranslateMutationTerm(match.Groups["term"].Value)}はすべて持っている。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslatePsychicGlimmerDebug(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = PsychicGlimmerDebugPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"TODOJASON サイキック・グリマー={Restore(match, spans, "value")}",
            source,
            stripped,
            spans);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWhole(
        string translated,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static string TranslateStat(string stat)
    {
        return stat switch
        {
            "Strength" => "筋力",
            "Toughness" => "頑健",
            "Willpower" => "意志力",
            "Agility" => "敏捷",
            "Ego" => "自我",
            "Intelligence" => "知力",
            _ => stat,
        };
    }

    private static string TranslateMutationTerm(string term)
    {
        return term switch
        {
            "mutation" or "mutations" => "変異",
            "defect" or "defects" => "欠陥",
            _ => term,
        };
    }

    internal static string TranslateMutationDisplayName(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => GetMutationDisplayNames().TryGetValue(visible, out var translated) ? translated : visible);
    }

    private static string TranslateMutationName(string source)
    {
        return TranslateMutationDisplayName(source);
    }

    private static Dictionary<string, string> GetMutationDisplayNames()
    {
        lock (SyncRoot)
        {
            if (mutationDisplayNames is not null)
            {
                return mutationDisplayNames;
            }

            mutationDisplayNames = LoadMutationDisplayNameMap("Mutations.jp.xml");
            MergeMutationDisplayNameMap(mutationDisplayNames, LoadMutationDisplayNameMap("HiddenMutations.jp.xml"));
            return mutationDisplayNames;
        }
    }

    private static Dictionary<string, string> LoadMutationDisplayNameMap(string relativePath)
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(relativePath);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return map;
        }

        try
        {
            var document = XDocument.Load(path, LoadOptions.None);
            if (document.Root is null)
            {
                return map;
            }

            foreach (var element in document.Root.Descendants("mutation"))
            {
                var name = element.Attribute("Name")?.Value;
                var displayName = element.Attribute("DisplayName")?.Value;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(displayName))
                {
                    map[name!] = displayName!;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            QudJP.RuntimeDiagnostics.LogImportant(
                $"QudJP: {Context} failed to load '{relativePath}': {ex.Message}");
        }

        return map;
    }

    private static void MergeMutationDisplayNameMap(
        IDictionary<string, string> target,
        IReadOnlyDictionary<string, string> source)
    {
        foreach (var pair in source)
        {
            target[pair.Key] = pair.Value;
        }
    }
}
