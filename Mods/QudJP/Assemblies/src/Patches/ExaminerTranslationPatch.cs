using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ExaminerTranslationPatch
{
    private const string Context = nameof(ExaminerTranslationPatch);

    private static readonly Regex UnderstandPattern =
        new Regex("^You now understand (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DiscoverHiddenPattern =
        new Regex("^You discover something about (?<target>.+?) that was hidden!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PuzzledPattern =
        new Regex("^You are puzzled by (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrokePattern =
        new Regex("^You think you broke (?<target>.+?)\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IdentifyPattern =
        new Regex("^You identify (?<prior>.+?) as (?<known>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CommitMemoryPattern =
        new Regex("^You commit the distinguishing characteristics of (?<target>.+?) to memory\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProgressKnownVarietyPattern =
        new Regex("^You make some progress understanding (?<target>.+?)\\. (?<seems>.*? to be) (?<known>.+?), and you think (?<itis>.+?) probably a variety of (?<variety>.+?); you believe you would be able to recognize an ordinary (?<ordinary>.+?) of (?<scope>that|those) now\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProgressVarietyPattern =
        new Regex("^You make some progress understanding (?<target>.+?)\\. You think (?<itis>.+?) probably a variety of (?<variety>.+?), and you believe you would be able to recognize an ordinary (?<ordinary>.+?) of (?<scope>that|those) now\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProgressKnownPattern =
        new Regex("^You make some progress understanding (?<target>.+?)\\. (?<seems>.*? to be) (?<known>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProgressOnlyPattern =
        new Regex("^You make some progress understanding (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrokenPattern =
        new Regex("^Whatever (?<subject>.+?) (?:is|are), (?<state>.+?) broken\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OwnedExaminePattern =
        new Regex("^(?<owner>.+?)(?: ?(?:is|are)) not owned by you, and examining (?<target>.+?) risks damaging (?<riskTarget>.+?)\\. Are you sure you want to do so\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerOwnedExaminePattern =
        new Regex("^(?<container>.+?)(?: ?(?:is|are)) not owned by you, and examining (?<item>.+?) inside (?<inside>.+?) risks causing damage\\. Are you sure you want to do so\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LeadingZeroWidthMarkupPrefixPattern =
        new Regex("^(?:\\{\\{[^|}]+\\|\\}\\}\\s*)+(?<rest>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LeadingOpenColorPrefixPattern =
        new Regex("^\\{\\{[^|}]+\\|\\s+(?<rest>(?:your|Your|a|an|the|some|A|An|The|Some)\\s+.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] EnglishArticlePrefixes =
    [
        "a ",
        "an ",
        "the ",
        "some ",
        "A ",
        "An ",
        "The ",
        "Some ",
    ];

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var examinerType = AccessTools.TypeByName("XRL.World.Parts.Examiner");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (examinerType is null || gameObjectType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Examiner, GameObject, or InventoryActionEvent.", Context);
            yield break;
        }

        var handleEvent = AccessTools.Method(examinerType, "HandleEvent", [inventoryActionEventType]);
        if (handleEvent is not null)
        {
            yield return handleEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) not found.", Context);
        }

        foreach (var methodName in new[]
                 {
                     "ResultSuccess",
                     "ResultExceptionalSuccess",
                     "ResultFailure",
                     "ResultFakeConfusionFailure",
                     "ResultCriticalFailure",
                 })
        {
            var method = AccessTools.Method(examinerType, methodName, [gameObjectType]);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}(GameObject) not found.", Context, methodName);
            }
        }

        var partialSuccess = AccessTools.Method(examinerType, "ResultPartialSuccess", [gameObjectType, typeof(int)]);
        if (partialSuccess is not null)
        {
            yield return partialSuccess;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ResultPartialSuccess(GameObject, int) not found.", Context);
        }

        foreach (var methodName in new[]
                 {
                     "MakeUnderstood",
                     "MakePartiallyUnderstood",
                 })
        {
            var method = AccessTools.Method(examinerType, methodName, [typeof(bool)]);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}(bool) not found.", Context, methodName);
            }
        }
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslate(UnderstandPattern, static target => target + "を理解した。", source, stripped, spans, route, family, "Understand", out translated)
            || TryTranslate(DiscoverHiddenPattern, static target => target + "について隠されていたことを発見した！", source, stripped, spans, route, family, "DiscoverHidden", out translated)
            || TryTranslate(PuzzledPattern, static target => target + "のことがわからない。", source, stripped, spans, route, family, "Puzzled", out translated)
            || TryTranslate(BrokePattern, static target => target + "を壊してしまった気がする。", source, stripped, spans, route, family, "Broke", out translated)
            || TryTranslateIdentify(source, stripped, spans, route, family, out translated)
            || TryTranslateCommitMemory(source, stripped, spans, route, family, out translated)
            || TryTranslateProgressKnownVariety(source, stripped, spans, route, family, out translated)
            || TryTranslateProgressVariety(source, stripped, spans, route, family, out translated)
            || TryTranslateProgressKnown(source, stripped, spans, route, family, out translated)
            || TryTranslateProgressOnly(source, stripped, spans, route, family, out translated)
            || TryTranslateBroken(source, stripped, spans, route, family, out translated)
            || TryTranslateOwnedExamine(source, stripped, spans, route, family, out translated)
            || TryTranslateContainerOwnedExamine(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateIdentify(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = IdentifyPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            TranslateDisplayNameObject(match, spans, "prior")
            + "を"
            + TranslateDisplayNameObject(match, spans, "known")
            + "だと鑑定した。",
            stripped,
            spans);
        Record(route, family, "Identify", source, translated);
        return true;
    }

    private static bool TryTranslateCommitMemory(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = CommitMemoryPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            TranslateDisplayNameObject(match, spans, "target") + "の特徴を記憶した。",
            stripped,
            spans);
        Record(route, family, "CommitMemory", source, translated);
        return true;
    }

    private static bool TryTranslateProgressKnownVariety(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ProgressKnownVarietyPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = TranslateDisplayNameObject(match, spans, "target");
        var known = TranslateDisplayNameObject(match, spans, "known");
        var variety = TranslateDisplayNameObject(match, spans, "variety");
        var ordinary = TranslateOrdinaryRecognitionTarget(match, spans, variety);
        translated = RestoreWholeSourceBoundary(
            target + "の理解が少し進んだ。それは" + known + "で、おそらく" + variety + "の一種だ。これで普通の" + ordinary + "なら見分けられるはずだ。",
            stripped,
            spans);
        Record(route, family, "ProgressKnownVariety", source, translated);
        return true;
    }

    private static bool TryTranslateProgressVariety(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ProgressVarietyPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = TranslateDisplayNameObject(match, spans, "target");
        var variety = TranslateDisplayNameObject(match, spans, "variety");
        var ordinary = TranslateOrdinaryRecognitionTarget(match, spans, variety);
        translated = RestoreWholeSourceBoundary(
            target + "の理解が少し進んだ。おそらく" + variety + "の一種だ。これで普通の" + ordinary + "なら見分けられるはずだ。",
            stripped,
            spans);
        Record(route, family, "ProgressVariety", source, translated);
        return true;
    }

    private static bool TryTranslateProgressKnown(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ProgressKnownPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            TranslateDisplayNameObject(match, spans, "target")
            + "の理解が少し進んだ。それは"
            + TranslateDisplayNameObject(match, spans, "known")
            + "だ。",
            stripped,
            spans);
        Record(route, family, "ProgressKnown", source, translated);
        return true;
    }

    private static bool TryTranslateProgressOnly(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ProgressOnlyPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            TranslateDisplayNameObject(match, spans, "target") + "の理解が少し進んだ。",
            stripped,
            spans);
        Record(route, family, "ProgressOnly", source, translated);
        return true;
    }

    private static bool TryTranslateBroken(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        if (!BrokenPattern.IsMatch(stripped))
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary("それが何であれ、壊れている...", stripped, spans);
        Record(route, family, "Broken", source, translated);
        return true;
    }

    private static bool TryTranslateOwnedExamine(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = OwnedExaminePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            TranslateObject(match, spans, "owner")
            + "はあなたのものではない。調べると"
            + TranslateObject(match, spans, "riskTarget")
            + "を傷つけるおそれがある。それでもそうしますか？",
            stripped,
            spans);
        Record(route, family, "OwnedExamine", source, translated);
        return true;
    }

    private static bool TryTranslateContainerOwnedExamine(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ContainerOwnedExaminePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            TranslateObject(match, spans, "container")
            + "はあなたのものではない。"
            + TranslateObject(match, spans, "inside")
            + "の中にある"
            + TranslateObject(match, spans, "item")
            + "を調べると損傷を引き起こすおそれがある。それでもそうしますか？",
            stripped,
            spans);
        Record(route, family, "ContainerOwnedExamine", source, translated);
        return true;
    }

    private static bool TryTranslate(
        Regex pattern,
        Func<string, string> build,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string detail,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(build(RestoreCapture(match, spans, "target")), stripped, spans);
        Record(route, family, detail, source, translated);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            group.Value,
            spans,
            group).Trim();
    }

    private static string TranslateObject(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return TranslatePronounOrObject(RestoreCaptureWithoutLeadingArticle(match, spans, groupName));
    }

    private static string RestoreCaptureWithoutLeadingArticle(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        return StripLeadingEnglishArticlePreservingMarkup(restored);
    }

    private static string StripLeadingEnglishArticlePreservingMarkup(string source)
    {
        var trimmed = source.Trim();
        for (var i = 0; i < EnglishArticlePrefixes.Length; i++)
        {
            var article = EnglishArticlePrefixes[i];
            if (trimmed.StartsWith(article, StringComparison.Ordinal))
            {
                return trimmed.Substring(article.Length);
            }
        }

        return trimmed;
    }

    private static string StripLeadingEnglishPossessiveOrArticlePreservingMarkup(string source)
    {
        var trimmed = StripLeadingZeroWidthMarkupPrefix(source).TrimStart();
        trimmed = StripLeadingEnglishArticlePreservingMarkup(trimmed);
        if (trimmed.StartsWith("your ", StringComparison.Ordinal))
        {
            return StripLeadingZeroWidthMarkupPrefix(trimmed.Substring("your ".Length)).TrimStart();
        }

        if (trimmed.StartsWith("Your ", StringComparison.Ordinal))
        {
            return StripLeadingZeroWidthMarkupPrefix(trimmed.Substring("Your ".Length)).TrimStart();
        }

        return StripLeadingZeroWidthMarkupPrefix(trimmed).TrimStart();
    }

    private static string TranslateOrdinaryRecognitionTarget(Match match, IReadOnlyList<ColorSpan> spans, string pronounFallback)
    {
        var ordinary = match.Groups["ordinary"].Value.Trim();
        return string.Equals(ordinary, "one", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ordinary, "ones", StringComparison.OrdinalIgnoreCase)
            ? pronounFallback
            : TranslateDisplayNameObject(match, spans, "ordinary");
    }

    private static string StripLeadingZeroWidthMarkupPrefix(string value)
    {
        var current = value;
        while (true)
        {
            var match = LeadingZeroWidthMarkupPrefixPattern.Match(current);
            if (!match.Success)
            {
                var openMatch = LeadingOpenColorPrefixPattern.Match(current);
                return openMatch.Success ? openMatch.Groups["rest"].Value.TrimStart() : current;
            }

            current = match.Groups["rest"].Value.TrimStart();
        }
    }

    private static string TranslateDisplayNameObject(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        var cleaned = StripLeadingEnglishPossessiveOrArticlePreservingMarkup(restored);
        return GetDisplayNameRouteTranslator.TranslatePreservingColors(cleaned, nameof(GetDisplayNamePatch));
    }

    private static string TranslatePronounOrObject(string source)
    {
        return source.Trim() switch
        {
            "it" or "It" => "それ",
            "them" or "Them" or "they" or "They" => "それら",
            "him" or "Him" or "he" or "He" => "彼",
            "her" or "Her" or "she" or "She" => "彼女",
            var value => value,
        };
    }

    private static string RestoreWholeSourceBoundary(
        string translatedCore,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
