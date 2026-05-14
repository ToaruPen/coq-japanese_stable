using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SingleCallsiteOwnerPopupTranslationPatch
{
    private const string Context = nameof(SingleCallsiteOwnerPopupTranslationPatch);
    private const string DecoyHologramOwner = "XRL.World.Parts.DecoyHologramEmitter|CreateHolograms";
    private const string BaetylRewardWishOwner = "XRL.World.Parts.RandomAltarBaetyl|HandleBaetylRewardWish";
    private const string AxeDismemberOwner = "XRL.World.Parts.Skill.Axe_Dismember|CastForceSuccess";
    private const string CudgelSlamOwner = "XRL.World.Parts.Skill.Cudgel_Slam|Cast";
    private const string ProselytizeOwner = "XRL.World.Parts.Skill.Persuasion_Proselytize|AttemptProselytization";
    private const string TinkeringOwner = "XRL.World.Parts.Skill.Tinkering|LearnNewRecipe";
    private const string GameUniqueOwner = "XRL.World.Parts.GameUnique|OnCreated";
    private const string GenocideCurioOwner = "XRL.World.Parts.GenocideCurio|HandleEvent";
    private const string GritGateMainframeOwner = "XRL.World.Parts.GritGateMainframeTerminal|HandleEvent";
    private const string LiquidFueledPowerPlantOwner = "XRL.World.Parts.LiquidFueledPowerPlant|HandleEvent";
    private const string RecoilOnDeathOwner = "XRL.World.Parts.RecoilOnDeath|HandleEvent";
    private const string SpraybottleOwner = "XRL.World.Parts.Spraybottle|HandleEvent";
    private const string TrainingBookOwner = "XRL.World.Parts.TrainingBook|HandleEvent";

    private const string DummySingleCallsiteOwner = "QudJP.Tests.DummyTargets.DummySingleCallsiteOwnerPopupTarget|";

    private static readonly Regex DecoyOutOfRangePattern = new(
        "^That is out of range \\((?<range>.+?) (?<unit>squares?)\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BaetylRewardWishPattern = new(
        "^Generated (?<item>.+?) as reward for (?<demand>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AxeDismemberSelfPattern = new(
        "^Are you sure you want to dismember (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CudgelSlamSelfPattern = new(
        "^Are you sure you want to slam (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkeringLearnRecipePattern = new(
        "^You have a flash of insight and scribe (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GameUniqueWishConfirmationPattern = new(
        "^(?<object>.+?) \\((?<blueprint>.+?)\\) is considered unique, are you sure you want to create another\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GenocideCurioActivationPattern = new(
        "^You activate (?<item>.+?) and toss it into the air\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GritGateMainframeUnresponsivePattern = new(
        "^(?<object>.+?) is unresponsive\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LiquidFueledPowerPlantEmptyPattern = new(
        "^Your (?<object>.+?) (?<verb>has|have) consumed all of (?<fuel>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RecoilOnDeathTransportPattern = new(
        "^Just before your demise, you are transported to safety! (?<object>.+?) (?<verb>disintegrates|disintegrate)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SpraybottleCoveredPattern = new(
        "^(?<object>.+?) (?<verb>is|are) covered in (?<liquid>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrainingBookAttributeIncreasePattern = new(
        "^Your (?<attribute>.+?) is increased by (?<amount>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<string>? ownerStack;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        var endTurnEventType = AccessTools.TypeByName("XRL.World.EndTurnEvent");
        var beforeDieEventType = AccessTools.TypeByName("XRL.World.BeforeDieEvent");
        var axeDismemberType = AccessTools.TypeByName("XRL.World.Parts.Skill.Axe_Dismember");
        var cudgelSlamType = AccessTools.TypeByName("XRL.World.Parts.Skill.Cudgel_Slam");
        if (gameObjectType is null
            || inventoryActionEventType is null
            || endTurnEventType is null
            || beforeDieEventType is null
            || axeDismemberType is null
            || cudgelSlamType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            "XRL.World.Parts.DecoyHologramEmitter",
            "CreateHolograms",
            [gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.Parts.RandomAltarBaetyl",
            "HandleBaetylRewardWish",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Axe_Dismember",
            "CastForceSuccess",
            [gameObjectType, axeDismemberType, gameObjectType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Cudgel_Slam",
            "Cast",
            [gameObjectType, cudgelSlamType, typeof(string), gameObjectType, typeof(bool), typeof(int), typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Persuasion_Proselytize",
            "AttemptProselytization",
            Type.EmptyTypes);
        AddTarget(
            targets,
            "XRL.World.Parts.Skill.Tinkering",
            "LearnNewRecipe",
            [gameObjectType, typeof(int), typeof(int)]);
        AddTarget(
            targets,
            "XRL.World.Parts.GameUnique",
            "OnCreated",
            [typeof(string)]);
        AddTarget(
            targets,
            "XRL.World.Parts.GenocideCurio",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.GritGateMainframeTerminal",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.LiquidFueledPowerPlant",
            "HandleEvent",
            [endTurnEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.RecoilOnDeath",
            "HandleEvent",
            [beforeDieEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.Spraybottle",
            "HandleEvent",
            [inventoryActionEventType]);
        AddTarget(
            targets,
            "XRL.World.Parts.TrainingBook",
            "HandleEvent",
            [inventoryActionEventType]);
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

        if (TryTranslateCore(source, CurrentOwnerKey(), out translated, out var detail))
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

    private static bool TryTranslateCore(string source, string? ownerKey, out string translated, out string detail)
    {
        var match = DecoyOutOfRangePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, DecoyHologramOwner, DummySingleCallsiteOwner + "CreateHolograms"))
        {
            translated = $"範囲外だ（{NormalizeRange(match.Groups["range"].Value)}マス）。";
            detail = "DecoyHologramOutOfRange";
            return true;
        }

        match = BaetylRewardWishPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, BaetylRewardWishOwner, DummySingleCallsiteOwner + "HandleBaetylRewardWish"))
        {
            translated = $"{match.Groups["demand"].Value}の報酬として{match.Groups["item"].Value}を生成した。";
            detail = "BaetylRewardWish";
            return true;
        }

        match = AxeDismemberSelfPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, AxeDismemberOwner, DummySingleCallsiteOwner + "CastForceSuccess"))
        {
            translated = $"{match.Groups["target"].Value}を切断してもよいか？";
            detail = "AxeDismemberSelfConfirmation";
            return true;
        }

        match = CudgelSlamSelfPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, CudgelSlamOwner, DummySingleCallsiteOwner + "Cast"))
        {
            translated = $"{match.Groups["target"].Value}を叩きつけてもよいか？";
            detail = "CudgelSlamSelfConfirmation";
            return true;
        }

        if (OwnerMatches(ownerKey, ProselytizeOwner, DummySingleCallsiteOwner + "AttemptProselytization")
            && source.Contains(" already your follower. Do you want to proselytize ")
            && DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated))
        {
            detail = "ProselytizeFollowerConfirmation";
            return true;
        }

        match = TinkeringLearnRecipePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, TinkeringOwner, DummySingleCallsiteOwner + "LearnNewRecipe"))
        {
            translated = $"ひらめきを得て{StringHelpers.StripLeadingEnglishArticle(match.Groups["item"].Value)}を記した。";
            detail = "TinkeringLearnRecipe";
            return true;
        }

        match = GameUniqueWishConfirmationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, GameUniqueOwner, DummySingleCallsiteOwner + "OnCreated"))
        {
            translated = $"{match.Groups["object"].Value}（{match.Groups["blueprint"].Value}）は一意とみなされています。もう1つ作成しますか？";
            detail = "GameUniqueWishConfirmation";
            return true;
        }

        match = GenocideCurioActivationPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, GenocideCurioOwner, DummySingleCallsiteOwner + "HandleGenocideCurio"))
        {
            translated = $"{match.Groups["item"].Value}を起動して空中に放り投げた。";
            detail = "GenocideCurioActivation";
            return true;
        }

        match = GritGateMainframeUnresponsivePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, GritGateMainframeOwner, DummySingleCallsiteOwner + "HandleGritGateMainframeTerminal"))
        {
            translated = $"{match.Groups["object"].Value}は反応しない。";
            detail = "GritGateMainframeUnresponsive";
            return true;
        }

        match = LiquidFueledPowerPlantEmptyPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, LiquidFueledPowerPlantOwner, DummySingleCallsiteOwner + "HandleLiquidFueledPowerPlant"))
        {
            translated = $"あなたの{match.Groups["object"].Value}は{match.Groups["fuel"].Value}をすべて消費した。";
            detail = "LiquidFueledPowerPlantEmpty";
            return true;
        }

        match = RecoilOnDeathTransportPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, RecoilOnDeathOwner, DummySingleCallsiteOwner + "HandleRecoilOnDeath"))
        {
            translated = $"死の直前、あなたは安全な場所へ転送された！ {match.Groups["object"].Value}は崩壊した。";
            detail = "RecoilOnDeathTransport";
            return true;
        }

        match = SpraybottleCoveredPattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, SpraybottleOwner, DummySingleCallsiteOwner + "HandleSpraybottle"))
        {
            translated = $"{match.Groups["object"].Value}は{match.Groups["liquid"].Value}に覆われた！";
            detail = "SpraybottleCovered";
            return true;
        }

        match = TrainingBookAttributeIncreasePattern.Match(source);
        if (match.Success && OwnerMatches(ownerKey, TrainingBookOwner, DummySingleCallsiteOwner + "HandleTrainingBook"))
        {
            translated = $"あなたの{match.Groups["attribute"].Value}が{match.Groups["amount"].Value}上昇した！";
            detail = "TrainingBookAttributeIncrease";
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

    private static string NormalizeRange(string source)
    {
        var trimmed = source.Trim();
        return trimmed switch
        {
            "zero" => "0",
            "one" => "1",
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "six" => "6",
            "seven" => "7",
            "eight" => "8",
            "nine" => "9",
            "ten" => "10",
            _ => trimmed,
        };
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type {1} not found.", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }
}
