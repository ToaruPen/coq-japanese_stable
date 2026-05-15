using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ConversationScriptPopupTranslationPatch
{
    private const string Context = nameof(ConversationScriptPopupTranslationPatch);

    private static readonly Regex MakeOutSpeechPattern = new(
        "^You can't seem to make out what (?<subject>.+?) (?:is|are) saying\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RefuseToSpeakPattern = new(
        "^(?<subject>.+?) refuses? to speak to you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex UtterlyUnresponsivePattern = new(
        "^(?<subject>.+?) (?:is|are) utterly unresponsive\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EngageConversationPattern = new(
        "^You cannot seem to engage (?<subject>.+?) in conversation\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SenseNothingPattern = new(
        "^You can sense nothing from (?<subject>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SenseHostilityPattern = new(
        "^You sense only hostility from (?<subject>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MakeContactPattern = new(
        "^You cannot seem to make contact with (?<subject>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var conversationScriptType = AccessTools.TypeByName("XRL.World.Parts.ConversationScript");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (conversationScriptType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var physical = AccessTools.Method(
            conversationScriptType,
            "IsPhysicalConversationPossible",
            [gameObjectType, gameObjectType, typeof(bool), typeof(bool), typeof(bool), typeof(int)]);
        if (physical is not null)
        {
            yield return physical;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.IsPhysicalConversationPossible(...) not found.", Context);
        }

        var mental = AccessTools.Method(
            conversationScriptType,
            "IsMentalConversationPossible",
            [gameObjectType, gameObjectType, typeof(bool), typeof(bool), typeof(int)]);
        if (mental is not null)
        {
            yield return mental;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.IsMentalConversationPossible(...) not found.", Context);
        }
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
            if (!OwnerTranslationScope.IsActive(activeDepth))
            {
                directMarkerPassThroughText = null;
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
        _ = family;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(source, ref directMarkerPassThroughText, out translated))
        {
            return true;
        }

        if (TryTranslateCore(source, out translated, out var detail))
        {
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + "." + detail,
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        if (TryTranslateSubjectTemplate(
            stripped,
            spans,
            MakeOutSpeechPattern,
            subject => subject + "が何と言っているのか聞き取れない。",
            "MakeOutSpeech",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateDoesVerb(source, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateSubjectTemplate(
            stripped,
            spans,
            UtterlyUnresponsivePattern,
            subject => subject + "はまったく反応しない",
            "UtterlyUnresponsive",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateSubjectTemplate(
            stripped,
            spans,
            RefuseToSpeakPattern,
            subject => subject + "はあなたと話そうとしない。",
            "RefuseToSpeak",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateSubjectTemplate(
            stripped,
            spans,
            EngageConversationPattern,
            subject => subject + "と会話を始められない。",
            "EngageConversation",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateSubjectTemplate(
            stripped,
            spans,
            SenseNothingPattern,
            subject => subject + "からは何も感じ取れない。",
            "SenseNothing",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateSubjectTemplate(
            stripped,
            spans,
            SenseHostilityPattern,
            subject => subject + "からは敵意しか感じない。",
            "SenseHostility",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateSubjectTemplate(
            stripped,
            spans,
            MakeContactPattern,
            subject => subject + "とうまく交信できない。",
            "MakeContact",
            out translated,
            out detail))
        {
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateSubjectTemplate(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Regex pattern,
        Func<string, string> compose,
        string candidateDetail,
        out string translated,
        out string detail)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = stripped;
            detail = string.Empty;
            return false;
        }

        var subject = NormalizeSubjectCapture(match, spans, "subject");
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            compose(subject),
            spans,
            stripped.Length);
        detail = candidateDetail;
        return true;
    }

    private static bool TryTranslateDoesVerb(string source, out string translated, out string detail)
    {
        if (source.Contains("engaged in hand-to-hand combat and"))
        {
            detail = "TooBusyCombat";
        }
        else if (source.Contains("on fire and")
                 && source.Contains("too busy to have a conversation with you"))
        {
            detail = "TooBusyOnFire";
        }
        else
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        return DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out translated)
               || DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated);
    }

    private static string NormalizeSubjectCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var subject = StringHelpers.StripLeadingEnglishArticle(
            group.Value,
            includeCapitalizedDefiniteArticle: true);
        var captureSpans = ColorCodePreserver.SliceSpans(spans, group.Index, group.Length);
        if (group.Index == 0
            && group.Length < match.Value.Length
            && !HasClosingTokenAt(spans, group.Index + group.Length))
        {
            captureSpans.RemoveAll(static span => !IsClosingToken(span.Token));
        }

        return ColorAwareTranslationComposer.Restore(subject, captureSpans);
    }

    private static bool HasClosingTokenAt(IReadOnlyList<ColorSpan> spans, int index)
    {
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            if (span.Index == index && IsClosingToken(span.Token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsClosingToken(string token)
    {
        return string.Equals(token, "}}", StringComparison.Ordinal)
               || token.StartsWith("</", StringComparison.Ordinal);
    }
}
