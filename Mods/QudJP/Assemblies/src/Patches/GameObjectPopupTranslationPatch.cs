using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectPopupTranslationPatch
{
    private const string Context = nameof(GameObjectPopupTranslationPatch);

    private static readonly Regex ImportantPluralPattern = new Regex(
        "^(?<item>.+?) are important\\. Are you sure you want to (?<verb>.+?) them(?<tail>.*?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ImportantSingularPattern = new Regex(
        "^(?<item>.+?) (?:is|are) important\\. Are you sure you want to (?<verb>.+?) (?:it|them)(?<tail>.*?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DoesNotWantNamePattern = new Regex(
        "^(?<owner>.+?) (?:don't|doesn't) want a new name\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StartCallingPattern = new Regex(
        "^You start calling (?<owner>.+?) by the name '(?<name>.+?)'\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AbilityPossessivePattern = new Regex(
        "^(?<owner>.+?)'s (?<ability>.+?) ability (?<state>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CompanionAbilityPickOptionIntroPattern = new Regex(
        "^Choose one of (?<owner>.+?)'s abilities to forbid or allow\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CompanionFollowDistancePickOptionIntroPattern = new Regex(
        "^Instruct (?<owner>.+?) to follow at what distance\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CompanionAbilityPickOptionRowPattern = new Regex(
        "^(?<ability>.+?) (?<state>\\[(?:allowed|forbidden|toggled on|toggled off)\\])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotHearPattern = new Regex(
        "^(?<owner>.+?) can't hear you!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PullDownDestinationOptionPattern = new Regex(
        "^(?<label>Current location|Arrival location|Center)(?<detail>, .+?)?(?<hotkey> \\([A-Z]+\\))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static int followDistanceOptionsRemaining;

    [ThreadStatic]
    private static List<int>? followDistanceOptionsStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        var activatedAbilitiesType = AccessTools.TypeByName("XRL.World.Parts.ActivatedAbilities");
        if (gameObjectType is null || inventoryActionEventType is null || activatedAbilitiesType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, gameObjectType, "ConfirmUseImportantAsync", gameObjectType, typeof(string), typeof(string), typeof(int));
        AddTarget(targets, gameObjectType, "ConfirmUseImportant", gameObjectType, typeof(string), typeof(string), typeof(int));
        AddTarget(targets, gameObjectType, "HandleInventoryActionEvent", inventoryActionEventType);
        AddTarget(targets, gameObjectType, "HandleRename", inventoryActionEventType);
        AddTarget(targets, gameObjectType, "ChangeCompanionAbilityUse", gameObjectType, activatedAbilitiesType);
        AddTarget(targets, gameObjectType, "CheckCompanionDirection", gameObjectType);
        AddTarget(targets, gameObjectType, "PullDown", typeof(bool));
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            followDistanceOptionsStack ??= new List<int>();
            followDistanceOptionsStack.Add(followDistanceOptionsRemaining);
            followDistanceOptionsRemaining = 0;
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

            var stack = followDistanceOptionsStack;
            if (stack is not null && stack.Count > 0)
            {
                var lastIndex = stack.Count - 1;
                followDistanceOptionsRemaining = stack[lastIndex];
                stack.RemoveAt(lastIndex);
            }

            if (activeDepth == 0)
            {
                followDistanceOptionsRemaining = 0;
                followDistanceOptionsStack = null;
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!IsTargetMessage(stripped))
        {
            translated = source;
            return false;
        }

        if (!TryTranslateCore(source, stripped, spans, out var coreTranslated))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, coreTranslated);
        translated = coreTranslated;
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string name, params Type[] parameters)
    {
        var method = AccessTools.Method(targetType, name, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1} target not found.", Context, name);
    }

    private static bool IsTargetMessage(string value)
    {
        return value.Contains(" important. Are you sure you want to ")
            || value.EndsWith(" want a new name.", StringComparison.Ordinal)
            || value.StartsWith("You start calling ", StringComparison.Ordinal)
            || value.StartsWith("Choose one of ", StringComparison.Ordinal)
            || value.StartsWith("Instruct ", StringComparison.Ordinal)
            || string.Equals(value, "Select a destination", StringComparison.Ordinal)
            || PullDownDestinationOptionPattern.IsMatch(value)
            || IsPendingCompanionFollowDistanceOption(value)
            || CompanionAbilityPickOptionRowPattern.IsMatch(value)
            || value.Contains(" ability is now toggled ")
            || value.EndsWith(" ability cannot be toggled at this time.", StringComparison.Ordinal)
            || value.Contains(" ability is now forbidden.")
            || value.Contains(" ability is now allowed.")
            || value.EndsWith(" can't hear you!", StringComparison.Ordinal);
    }

    private static bool TryTranslateCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var companionFollowDistanceIntro = CompanionFollowDistancePickOptionIntroPattern.Match(stripped);
        if (companionFollowDistanceIntro.Success)
        {
            followDistanceOptionsRemaining = 3;
            var owner = RestoreCapture(companionFollowDistanceIntro, spans, "owner");
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{owner}にどの距離で追従させますか？",
                spans,
                stripped.Length,
                source);
            return true;
        }

        if (TryTranslateCompanionFollowDistanceOption(stripped, spans, source, out translated))
        {
            return true;
        }

        if (TryTranslatePullDownDestinationOption(stripped, spans, source, out translated))
        {
            return true;
        }

        var companionAbilityIntro = CompanionAbilityPickOptionIntroPattern.Match(stripped);
        if (companionAbilityIntro.Success)
        {
            var owner = RestoreCapture(companionAbilityIntro, spans, "owner");
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{owner}の能力を1つ選んで禁止または許可してください。",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var companionAbilityRow = CompanionAbilityPickOptionRowPattern.Match(stripped);
        if (companionAbilityRow.Success)
        {
            var ability = TranslateAbilityName(RestoreCapture(companionAbilityRow, spans, "ability"));
            var state = TranslateCompanionAbilityRowState(RestoreCapture(companionAbilityRow, spans, "state"));
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{ability} {state}",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var importantPlural = ImportantPluralPattern.Match(stripped);
        if (importantPlural.Success)
        {
            var item = RestoreCapture(importantPlural, spans, "item");
            var verb = RestoreCapture(importantPlural, spans, "verb");
            var tail = RestoreCapture(importantPlural, spans, "tail", trim: false);
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{item}は重要だ。本当にそれらを{verb}{tail}しますか？",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var importantSingular = ImportantSingularPattern.Match(stripped);
        if (importantSingular.Success)
        {
            var item = RestoreCapture(importantSingular, spans, "item");
            var verb = RestoreCapture(importantSingular, spans, "verb");
            var tail = RestoreCapture(importantSingular, spans, "tail", trim: false);
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{item}は重要だ。本当に{verb}{tail}しますか？",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var doesNotWantName = DoesNotWantNamePattern.Match(stripped);
        if (doesNotWantName.Success)
        {
            var owner = RestoreCapture(doesNotWantName, spans, "owner");
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{owner}は新しい名前を望んでいない。",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var startCalling = StartCallingPattern.Match(stripped);
        if (startCalling.Success)
        {
            var owner = RestoreCapture(startCalling, spans, "owner");
            var name = RestoreCapture(startCalling, spans, "name");
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{owner}を「{name}」と呼び始めた。",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var abilityPossessive = AbilityPossessivePattern.Match(stripped);
        if (abilityPossessive.Success
            && TryTranslateAbilityState(
                RestoreCapture(abilityPossessive, spans, "owner"),
                TranslateAbilityName(RestoreCapture(abilityPossessive, spans, "ability")),
                abilityPossessive.Groups["state"].Value,
                out var abilityStateTranslation))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                abilityStateTranslation,
                spans,
                stripped.Length,
                source);
            return true;
        }

        var cannotHear = CannotHearPattern.Match(stripped);
        if (cannotHear.Success)
        {
            var owner = RestoreCapture(cannotHear, spans, "owner");
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{owner}にはあなたの声が聞こえない！",
                spans,
                stripped.Length,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool IsPendingCompanionFollowDistanceOption(string value)
    {
        return followDistanceOptionsRemaining > 0
            && (string.Equals(value, "close", StringComparison.Ordinal)
                || string.Equals(value, "medium", StringComparison.Ordinal)
                || string.Equals(value, "far", StringComparison.Ordinal));
    }

    private static bool TryTranslateCompanionFollowDistanceOption(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        if (!IsPendingCompanionFollowDistanceOption(stripped))
        {
            translated = source;
            return false;
        }

        followDistanceOptionsRemaining--;
        translated = stripped switch
        {
            "close" => "近く",
            "medium" => "中間",
            "far" => "遠く",
            _ => stripped,
        };
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslatePullDownDestinationOption(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        if (string.Equals(stripped, "Select a destination", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                "目的地を選択",
                spans,
                stripped.Length,
                source);
            return true;
        }

        var match = PullDownDestinationOptionPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var prefix = match.Groups["label"].Value switch
        {
            "Current location" => "現在地",
            "Arrival location" => "到着地点",
            "Center" => "中央",
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(prefix))
        {
            translated = source;
            return false;
        }

        var detail = match.Groups["detail"].Success ? RestoreCapture(match, spans, "detail", trim: false) : string.Empty;
        var hotkey = match.Groups["hotkey"].Success ? RestoreCapture(match, spans, "hotkey", trim: false) : string.Empty;
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            prefix + detail + hotkey,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName, bool trim = true)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group);
        return trim ? restored.Trim() : restored;
    }

    private static string TranslateAbilityName(string source)
    {
        return ActivatedAbilityNameTranslator.TranslatePreservingColors(
            source,
            Context,
            Context + ".CompanionAbilityName");
    }

    private static string TranslateCompanionAbilityRowState(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => visible switch
            {
                "[allowed]" => "[許可]",
                "[forbidden]" => "[禁止]",
                "[toggled on]" => "[オン]",
                "[toggled off]" => "[オフ]",
                _ => visible,
            });
    }

    private static bool TryTranslateAbilityState(string owner, string ability, string state, out string translated)
    {
        switch (state)
        {
            case "is now toggled on.":
                translated = $"{owner}の{ability}能力はオンに切り替わった。";
                return true;
            case "is now toggled off.":
                translated = $"{owner}の{ability}能力はオフに切り替わった。";
                return true;
            case "cannot be toggled at this time.":
                translated = $"{owner}の{ability}能力は今は切り替えられない。";
                return true;
            case "is now forbidden.":
                translated = $"{owner}の{ability}能力は禁止された。";
                return true;
            case "is now allowed.":
                translated = $"{owner}の{ability}能力は許可された。";
                return true;
            default:
                translated = string.Empty;
                return false;
        }
    }
}
