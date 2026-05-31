using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WishCommandQueueTranslationPatch
{
    private const string Context = nameof(WishCommandQueueTranslationPatch);
    private const string DictionaryFile = "ui-messagelog-world.ja.json";
    private const string LandingPadsContext = "XRL.World.Quests.LandingPadsSystem";

    private static readonly Regex ReclamationWishTimerPattern = new(
        "^Turns until nephal arrives: (?<turns>\\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FindASiteDynamicQuestWherePattern = new(
        "^quest in (?<zone>.+?) secret id is (?<secret>.+?) for quest (?<quest>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var landingPadsType = AccessTools.TypeByName("XRL.World.Quests.LandingPadsSystem");
        var reclamationType = AccessTools.TypeByName("XRL.World.Quests.ReclamationSystem");
        var statWishType = AccessTools.TypeByName("XRL.World.StatWishHandler");
        var findASiteType = AccessTools.TypeByName("XRL.World.ZoneBuilders.FindASiteDynamicQuestManager");
        if (landingPadsType is null || reclamationType is null || statWishType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var slynthQuestWish = AccessTools.Method(landingPadsType, "SlynthQuestWish", [typeof(string)]);
        if (slynthQuestWish is not null)
        {
            yield return slynthQuestWish;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.LandingPadsSystem.SlynthQuestWish(string) not found.", Context);
        }

        var wishTimer = AccessTools.Method(reclamationType, "WishTimer", Type.EmptyTypes);
        if (wishTimer is not null)
        {
            yield return wishTimer;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ReclamationSystem.WishTimer() not found.", Context);
        }

        var clearStatShifts = AccessTools.Method(statWishType, "ClearStatShifts", Type.EmptyTypes);
        if (clearStatShifts is not null)
        {
            yield return clearStatShifts;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.StatWishHandler.ClearStatShifts() not found.", Context);
        }

        if (findASiteType is not null)
        {
            var dynamicQuestWhere = AccessTools.Method(findASiteType, "DynamicQuestWhere", Type.EmptyTypes);
            if (dynamicQuestWhere is not null)
            {
                yield return dynamicQuestWhere;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.FindASiteDynamicQuestManager.DynamicQuestWhere() not found.", Context);
            }
        }
        else
        {
            Trace.TraceError(
                "QudJP: {0} target type not found: XRL.World.ZoneBuilders.FindASiteDynamicQuestManager.",
                Context);
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
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateWishCommandMessage(message, out var translated, out var detail))
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

    private static bool TryTranslateWishCommandMessage(
        string source,
        out string translated,
        out string detail)
    {
        if (string.Equals(source, "No faction found by that name.", StringComparison.Ordinal))
        {
            var dictionaryTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                source,
                LandingPadsContext,
                DictionaryFile);
            if (string.IsNullOrEmpty(dictionaryTranslation)
                || string.Equals(dictionaryTranslation, source, StringComparison.Ordinal))
            {
                translated = source;
                detail = string.Empty;
                return false;
            }

            translated = dictionaryTranslation!;
            detail = "SlynthQuestNoFaction";
            return true;
        }

        var match = ReclamationWishTimerPattern.Match(source);
        if (match.Success)
        {
            translated = "ネファル到着までのターン数: " + match.Groups["turns"].Value;
            detail = "ReclamationWishTimer";
            return true;
        }

        if (string.Equals(source, "Clearing player body stat shifts...", StringComparison.Ordinal))
        {
            translated = "プレイヤー身体の能力値補正を消去中...";
            detail = "ClearStatShifts";
            return true;
        }

        match = FindASiteDynamicQuestWherePattern.Match(source);
        if (match.Success)
        {
            translated = $"クエスト {match.Groups["quest"].Value} の場所は {match.Groups["zone"].Value}、秘密IDは {match.Groups["secret"].Value}。";
            detail = "FindASiteDynamicQuestWhere";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }
}
