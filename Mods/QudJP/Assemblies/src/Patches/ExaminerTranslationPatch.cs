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

    private static readonly Regex BrokenPattern =
        new Regex("^Whatever (?<subject>.+?) (?:is|are), (?<state>.+?) broken\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OwnedExaminePattern =
        new Regex("^(?<owner>.+?)(?: ?(?:is|are)) not owned by you, and examining (?<target>.+?) risks damaging (?<riskTarget>.+?)\\. Are you sure you want to do so\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerOwnedExaminePattern =
        new Regex("^(?<container>.+?)(?: ?(?:is|are)) not owned by you, and examining (?<item>.+?) inside (?<inside>.+?) risks causing damage\\. Are you sure you want to do so\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
            || TryTranslateBroken(source, stripped, spans, route, family, out translated)
            || TryTranslateOwnedExamine(source, stripped, spans, route, family, out translated)
            || TryTranslateContainerOwnedExamine(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
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
            TranslateSubject(match, spans, "owner")
            + "はあなたのものではない。調べると"
            + TranslateObject(match, spans, "riskTarget")
            + "を傷つけるおそれがある。それでもそうするか？",
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
            TranslateSubject(match, spans, "container")
            + "はあなたのものではない。"
            + TranslateObject(match, spans, "inside")
            + "の中にある"
            + TranslateObject(match, spans, "item")
            + "を調べると損傷を引き起こすおそれがある。それでもそうするか？",
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

    private static string TranslateSubject(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return TranslatePronounOrObject(RestoreCapture(match, spans, groupName));
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
