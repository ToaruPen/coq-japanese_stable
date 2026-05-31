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
        "^(.+?) are important\\. Are you sure you want to (.+?) them(.*?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ImportantSingularPattern = new Regex(
        "^(.+?) (?:is|are) important\\. Are you sure you want to (.+?) (?:it|them)(.*?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DoesNotWantNamePattern = new Regex(
        "^(.+?) (?:don't|doesn't) want a new name\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StartCallingPattern = new Regex(
        "^You start calling (.+?) by the name '(.+?)'\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AbilityPossessivePattern = new Regex(
        "^(.+?)'s (.+?) ability (.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotHearPattern = new Regex(
        "^(.+?) can't hear you!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CompanionFollowDistancePromptPattern = new Regex(
        "^Instruct (.+?) to follow at what distance\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PullDownDestinationOptionPattern = new Regex(
        "^(Current location|Arrival location|Center)(, .+?)?( \\([A-Z]+\\))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!IsTargetMessage(source))
        {
            translated = source;
            return false;
        }

        if (!TryTranslateCore(source, out var coreTranslated))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, coreTranslated);
        translated = coreTranslated;
        return true;
    }

    internal static bool TryTranslatePopupProducerText(string source, string route, string family, out string translated)
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

        if (!TryTranslatePickOptionCore(source, out var coreTranslated))
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
            || value.Contains(" ability is now toggled ")
            || value.EndsWith(" ability cannot be toggled at this time.", StringComparison.Ordinal)
            || value.Contains(" ability is now forbidden.")
            || value.Contains(" ability is now allowed.")
            || value.EndsWith(" can't hear you!", StringComparison.Ordinal);
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        var importantPlural = ImportantPluralPattern.Match(source);
        if (importantPlural.Success)
        {
            translated = $"{importantPlural.Groups[1].Value}は重要だ。本当にそれらを{importantPlural.Groups[2].Value}{importantPlural.Groups[3].Value}しますか？";
            return true;
        }

        var importantSingular = ImportantSingularPattern.Match(source);
        if (importantSingular.Success)
        {
            translated = $"{importantSingular.Groups[1].Value}は重要だ。本当に{importantSingular.Groups[2].Value}{importantSingular.Groups[3].Value}しますか？";
            return true;
        }

        var doesNotWantName = DoesNotWantNamePattern.Match(source);
        if (doesNotWantName.Success)
        {
            translated = $"{doesNotWantName.Groups[1].Value}は新しい名前を望んでいない。";
            return true;
        }

        var startCalling = StartCallingPattern.Match(source);
        if (startCalling.Success)
        {
            translated = $"{startCalling.Groups[1].Value}を「{startCalling.Groups[2].Value}」と呼び始めた。";
            return true;
        }

        var abilityPossessive = AbilityPossessivePattern.Match(source);
        if (abilityPossessive.Success
            && TryTranslateAbilityState(
                abilityPossessive.Groups[1].Value,
                abilityPossessive.Groups[2].Value,
                abilityPossessive.Groups[3].Value,
                out translated))
        {
            return true;
        }

        var cannotHear = CannotHearPattern.Match(source);
        if (cannotHear.Success)
        {
            translated = $"{cannotHear.Groups[1].Value}にはあなたの声が聞こえない！";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePickOptionCore(string source, out string translated)
    {
        var followDistancePrompt = CompanionFollowDistancePromptPattern.Match(source);
        if (followDistancePrompt.Success)
        {
            translated = $"{followDistancePrompt.Groups[1].Value}にどの距離で追従するよう指示しますか？";
            return true;
        }

        if (TryTranslatePullDownDestinationOption(source, out translated))
        {
            return true;
        }

        switch (source)
        {
            case "Select a destination":
                translated = "目的地を選択";
                return true;
            case "close":
                translated = "近く";
                return true;
            case "medium":
                translated = "中距離";
                return true;
            case "far":
                translated = "遠く";
                return true;
            default:
                translated = source;
                return false;
        }
    }

    private static bool TryTranslatePullDownDestinationOption(string source, out string translated)
    {
        var match = PullDownDestinationOptionPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var prefix = match.Groups[1].Value switch
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

        translated = prefix + match.Groups[2].Value + match.Groups[3].Value;
        return true;
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
