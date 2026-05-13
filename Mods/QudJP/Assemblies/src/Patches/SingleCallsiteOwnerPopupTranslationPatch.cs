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

    private static readonly Regex DecoyOutOfRangePattern = new(
        "^That is out of range \\((?<range>.+?) (?<unit>squares?)\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BaetylRewardWishPattern = new(
        "^Generated (?<item>.+?) as reward for (?<demand>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AxeDismemberSelfPattern = new(
        "^Are you sure you want to dismember (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var axeDismemberType = AccessTools.TypeByName("XRL.World.Parts.Skill.Axe_Dismember");
        if (gameObjectType is null || axeDismemberType is null)
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
        var match = DecoyOutOfRangePattern.Match(source);
        if (match.Success)
        {
            translated = $"範囲外だ（{NormalizeRange(match.Groups["range"].Value)}マス）。";
            detail = "DecoyHologramOutOfRange";
            return true;
        }

        match = BaetylRewardWishPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups["demand"].Value}の報酬として{match.Groups["item"].Value}を生成した。";
            detail = "BaetylRewardWish";
            return true;
        }

        match = AxeDismemberSelfPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups["target"].Value}を切断してもよいか？";
            detail = "AxeDismemberSelfConfirmation";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
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
