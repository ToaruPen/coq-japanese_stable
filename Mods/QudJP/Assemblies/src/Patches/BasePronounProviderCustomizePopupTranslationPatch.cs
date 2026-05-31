using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BasePronounProviderCustomizePopupTranslationPatch
{
    private const string Context = nameof(BasePronounProviderCustomizePopupTranslationPatch);
    private static readonly Regex FullyPluralPattern = new(
        "^Should your (?<what>.+?) be treated as fully plural, with you being addressed as a multiple subject in all circumstances\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ConditionallyPluralPattern = new(
        "^Should your (?<what>.+?) be treated as conditionally plural, with you being addressed as a multiple subject only following a pronoun, as with with singular \"they\"\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PersonPattern = new(
        "^Is an entity with this (?<what>.+?) treated grammatically as a person, such that it would be improper to say \"look at (?<indicative>.+?)\" in reference to (?<objective>.+?) -- one would say \"look at (?<indicativePerson>.+?) (?<personTerm>.+?)\" or \"look at (?<objectiveAgain>.+?)\" instead\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.BasePronounProvider");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddStateMachineTarget(targets, targetType, "CustomizeProcess", new[] { typeof(string) });

        var genderType = AccessTools.TypeByName("XRL.World.Gender");
        if (genderType is null)
        {
            Trace.TraceError("QudJP: {0} Gender target type not found.", Context);
        }
        else
        {
            AddStateMachineTarget(targets, genderType, "CustomizeProcess", new[] { typeof(string) });
        }

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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (TryTranslateCustomizePopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCustomizePopup(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = FullyPluralPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"あなたの{TranslateWhat(match, spans)}を完全な複数形として扱い、あらゆる状況で複数主語として呼びかけますか？";
            return true;
        }

        match = ConditionallyPluralPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"あなたの{TranslateWhat(match, spans)}を条件付きの複数形として扱い、単数の \"they\" のように、代名詞の後でのみ複数主語として呼びかけますか？";
            return true;
        }

        match = PersonPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"この{TranslateWhat(match, spans)}のエンティティを文法上の人物として扱いますか？ つまり、{RestoreCapture(match, spans, "objective")}を指して「look at {RestoreCapture(match, spans, "indicative")}」と言うのは不適切で、「look at {RestoreCapture(match, spans, "indicativePerson")} {RestoreCapture(match, spans, "personTerm")}」または「look at {RestoreCapture(match, spans, "objectiveAgain")}」と言うべきですか？";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateWhat(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["what"];
        var translated = group.Value switch
        {
            "gender" => "ジェンダー",
            "pronoun set" => "代名詞セット",
            _ => group.Value,
        };

        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static void AddStateMachineTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var sourceMethod = AccessTools.Method(targetType, methodName, parameters);
        if (sourceMethod is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} async source target not found.", Context, targetType.FullName, methodName);
            return;
        }

        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        var moveNext = asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
        if (moveNext is not null)
        {
            targets.Add(moveNext);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} async state machine MoveNext not found.", Context, targetType.FullName, methodName);
    }
}
