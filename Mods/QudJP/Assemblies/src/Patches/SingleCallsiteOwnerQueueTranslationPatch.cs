using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SingleCallsiteOwnerQueueTranslationPatch
{
    private const string Context = nameof(SingleCallsiteOwnerQueueTranslationPatch);
    private const string ActivatedAbilityEntryOwner = "XRL.World.Parts.ActivatedAbilityEntry|TrySendCommandEventOnPlayer";
    private const string ElevatorSwitchOwner = "XRL.World.Parts.ElevatorSwitch|FireEvent";
    private const string ModMorphogeneticOwner = "XRL.World.Parts.ModMorphogenetic|ApplyMorphicShock";
    private const string MonochromeOwner = "XRL.World.Effects.Monochrome|FireEvent";
    private const string PersuasionRebukeRobotAttemptOwner = "XRL.World.Parts.Skill.Persuasion_RebukeRobot|AttemptRebuke";
    private const string SnapjawHowlOwner = "XRL.World.Parts.Skill.Snapjaw_Howl|FireEvent";
    private const string SphynxSaltTonicOwner = "XRL.World.Effects.SphynxSalt_Tonic|Apply";
    private const string StairsDownOwner = "XRL.World.Parts.StairsDown|CheckPullDown";
    private const string ThiefBotOwner = "XRL.World.Parts.ThiefBot|FireEvent";
    private const string WeirdwireConduitOwner = "XRL.World.Quests.WeirdwireConduitSystem|HandleEvent";

    private static readonly Regex MorphogeneticShockPattern = new(
        "^A weird(?<painful>, painful)? shock reverberates through you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThiefBotAvoidPincersPattern = new(
        "^You avoid (?<target>.+?)(?:'s|') pincers\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThiefBotPincersPassThroughPattern = new(
        "^(?<target>.+?)(?:'s|') pincers pass through you harmlessly\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WeirdwireCopperWirePattern = new(
        "^You now have (?<length>\\d+) feet of copper wire\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<string>? ownerStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var activatedAbilityEntryType = FindAssemblyCSharpType("XRL.World.Parts.ActivatedAbilityEntry");
        var elevatorSwitchType = FindAssemblyCSharpType("XRL.World.Parts.ElevatorSwitch");
        var eventType = FindAssemblyCSharpType("XRL.World.Event");
        var gameObjectType = FindAssemblyCSharpType("XRL.World.GameObject");
        var modMorphogeneticType = FindAssemblyCSharpType("XRL.World.Parts.ModMorphogenetic");
        var monochromeType = FindAssemblyCSharpType("XRL.World.Effects.Monochrome");
        var persuasionRebukeRobotType = FindAssemblyCSharpType("XRL.World.Parts.Skill.Persuasion_RebukeRobot");
        var snapjawHowlType = FindAssemblyCSharpType("XRL.World.Parts.Skill.Snapjaw_Howl");
        var sphynxSaltTonicType = FindAssemblyCSharpType("XRL.World.Effects.SphynxSalt_Tonic");
        var stairsDownType = FindAssemblyCSharpType("XRL.World.Parts.StairsDown");
        var thiefBotType = FindAssemblyCSharpType("XRL.World.Parts.ThiefBot");
        var tookEventType = FindAssemblyCSharpType("XRL.World.TookEvent");
        var weirdwireConduitType = FindAssemblyCSharpType("XRL.World.Quests.WeirdwireConduitSystem");
        if (activatedAbilityEntryType is null
            || elevatorSwitchType is null
            || eventType is null
            || gameObjectType is null
            || modMorphogeneticType is null
            || monochromeType is null
            || persuasionRebukeRobotType is null
            || snapjawHowlType is null
            || sphynxSaltTonicType is null
            || stairsDownType is null
            || thiefBotType is null
            || tookEventType is null
            || weirdwireConduitType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            activatedAbilityEntryType,
            "TrySendCommandEventOnPlayer",
            Type.EmptyTypes);
        AddTarget(
            targets,
            elevatorSwitchType,
            "FireEvent",
            [eventType]);
        AddTarget(
            targets,
            modMorphogeneticType,
            "ApplyMorphicShock",
            [gameObjectType, typeof(int), gameObjectType, typeof(int)]);
        AddTarget(
            targets,
            monochromeType,
            "FireEvent",
            [eventType]);
        AddTarget(
            targets,
            persuasionRebukeRobotType,
            "AttemptRebuke",
            Type.EmptyTypes);
        AddTarget(
            targets,
            snapjawHowlType,
            "FireEvent",
            [eventType]);
        AddTarget(
            targets,
            sphynxSaltTonicType,
            "Apply",
            [gameObjectType]);
        AddTarget(
            targets,
            stairsDownType,
            "CheckPullDown",
            [gameObjectType]);
        AddTarget(
            targets,
            thiefBotType,
            "FireEvent",
            [eventType]);
        AddTarget(
            targets,
            weirdwireConduitType,
            "HandleEvent",
            [tookEventType]);
        return targets;
    }

    public static void Prefix(MethodBase __originalMethod)
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
            ownerStack ??= new Stack<string>();
            ownerStack.Push(FormatOwnerKey(__originalMethod));
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
            if (ownerStack is { Count: > 0 })
            {
                _ = ownerStack.Pop();
            }

            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        return TryTranslateQueuedMessageForOwnerKey(ref message, color, CurrentOwnerKey());
    }

    internal static bool TryTranslateQueuedMessageForOwnerKey(ref string message, string? color, string? ownerKey)
    {
        _ = color;

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateCore(message, ownerKey, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateCore(string source, string? ownerKey, out string translated, out string detail)
    {
        if (string.Equals(source, "You cannot do that on the world map.", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, ActivatedAbilityEntryOwner))
        {
            translated = "ワールドマップではそれはできない。";
            detail = "ActivatedAbilityEntryWorldMapBlock";
            return true;
        }

        if (string.Equals(source, "Nothing seems to happen when you hit the switch.", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, ElevatorSwitchOwner))
        {
            translated = "スイッチを押しても何も起こらない。";
            detail = "ElevatorSwitchNothingHappens";
            return true;
        }

        var match = MorphogeneticShockPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ModMorphogeneticOwner))
        {
            if (match.Groups["painful"].Success)
            {
                translated = "「奇妙で痛い電撃」が全身を駆け抜けた。";
                detail = "ModMorphogeneticPainfulShock";
            }
            else
            {
                translated = "「奇妙な電撃」が全身を駆け抜けた。";
                detail = "ModMorphogeneticPainlessShock";
            }

            return true;
        }

        if (string.Equals(source, "Color starts to seep into the world.", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, MonochromeOwner))
        {
            translated = "世界に色が染み込んでいく。";
            detail = "MonochromeColorReturns";
            return true;
        }

        if (string.Equals(source, "You cannot rebuke without a tongue.", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, PersuasionRebukeRobotAttemptOwner))
        {
            translated = "舌がないと叱責できない。";
            detail = "PersuasionRebukeRobotMissingTongue";
            return true;
        }

        if (string.Equals(source, "You are frenzied by the howl!", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, SnapjawHowlOwner))
        {
            translated = "遠吠えに興奮させられた！";
            detail = "SnapjawHowlFrenzy";
            return true;
        }

        if (string.Equals(source, "You sense a subtle psychic disturbance.", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, SphynxSaltTonicOwner))
        {
            translated = "かすかな精神的乱れを感じる。";
            detail = "SphynxSaltPsychicDisturbance";
            return true;
        }

        if (string.Equals(source, "You fall downward!", StringComparison.Ordinal)
            && OwnerMatches(ownerKey, StairsDownOwner))
        {
            translated = "下に落ちた！";
            detail = "StairsDownFallDownward";
            return true;
        }

        match = ThiefBotPincersPassThroughPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ThiefBotOwner))
        {
            translated = match.Groups["target"].Value + "のハサミはあなたを傷つけることなくすり抜けた。";
            detail = "ThiefBotPincersPassThrough";
            return true;
        }

        match = ThiefBotAvoidPincersPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, ThiefBotOwner))
        {
            translated = match.Groups["target"].Value + "のハサミを避けた。";
            detail = "ThiefBotAvoidPincers";
            return true;
        }

        match = WeirdwireCopperWirePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, WeirdwireConduitOwner))
        {
            translated = "銅線を" + match.Groups["length"].Value + "フィート持っている。";
            detail = "WeirdwireCopperWireTotal";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string? CurrentOwnerKey()
    {
        return ownerStack is { Count: > 0 } ? ownerStack.Peek() : null;
    }

    private static bool OwnerMatches(string? actual, params string[] expected)
    {
        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        for (var index = 0; index < expected.Length; index++)
        {
            if (string.Equals(actual, expected[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatOwnerKey(MethodBase method)
    {
        return (method.DeclaringType?.FullName ?? string.Empty) + "|" + method.Name;
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

    private static Type? FindAssemblyCSharpType(string fullTypeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
            {
                continue;
            }

            var type = assembly.GetType(fullTypeName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        return Type.GetType(fullTypeName + ", Assembly-CSharp", throwOnError: false);
    }
}
