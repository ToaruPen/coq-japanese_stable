using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TerrainTravelTranslationPatch
{
    private const string Context = nameof(TerrainTravelTranslationPatch);

    private static readonly Regex EncounterChancePattern = new(
        "^(?<label>Base encounter chance|Modified encounter chance|Triggered encounter chance): (?<value>\\d+)%$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GetLostChancePattern = new(
        "^Get lost chance: (?<value>\\d+)%$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TravelSpeedPattern = new(
        "^Travel speed: (?<value>\\d+) segments/parasang$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HpWarningStopTravelPattern = new(
        "^\\{\\{R\\|Your health has dropped below \\{\\{C\\|(?<value>\\d+)%\\}\\}!\\}\\} Do you want to stop travelling\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var terrainTravelType = AccessTools.TypeByName("XRL.World.Parts.TerrainTravel");
        var objectEnteredCellEventType = AccessTools.TypeByName("XRL.World.ObjectEnteredCellEvent");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (terrainTravelType is null || objectEnteredCellEventType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var handleEvent = AccessTools.Method(terrainTravelType, "HandleEvent", [objectEnteredCellEventType]);
        if (handleEvent is not null)
        {
            yield return handleEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(ObjectEnteredCellEvent) not found.", Context);
        }

        var handleLeavingCell = AccessTools.Method(
            terrainTravelType,
            "HandleLeavingCell",
            [gameObjectType, typeof(int).MakeByRefType()]);
        if (handleLeavingCell is not null)
        {
            yield return handleLeavingCell;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleLeavingCell(GameObject, ref int) not found.", Context);
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

        if (!TryTranslateTravelQueuedMessage(message, out var translated, out var detail))
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

        if (!TryTranslateHpWarningStopTravel(source, out translated))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".HpWarningStopTravel",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateTravelQueuedMessage(string source, out string translated, out string detail)
    {
        var match = EncounterChancePattern.Match(source);
        if (match.Success)
        {
            detail = match.Groups["label"].Value switch
            {
                "Base encounter chance" => "BaseEncounterChance",
                "Modified encounter chance" => "ModifiedEncounterChance",
                "Triggered encounter chance" => "TriggeredEncounterChance",
                _ => string.Empty,
            };
            var label = match.Groups["label"].Value switch
            {
                "Base encounter chance" => "基本遭遇率",
                "Modified encounter chance" => "修正後遭遇率",
                "Triggered encounter chance" => "発生した遭遇率",
                _ => string.Empty,
            };
            translated = label + ": " + match.Groups["value"].Value + "%";
            return detail.Length > 0;
        }

        match = GetLostChancePattern.Match(source);
        if (match.Success)
        {
            detail = "GetLostChance";
            translated = "迷子になる確率: " + match.Groups["value"].Value + "%";
            return true;
        }

        match = TravelSpeedPattern.Match(source);
        if (match.Success)
        {
            detail = "TravelSpeed";
            translated = "移動速度: " + match.Groups["value"].Value + " セグメント/パラサング";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateHpWarningStopTravel(string source, out string translated)
    {
        var match = HpWarningStopTravelPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = "{{R|HPが{{C|" + match.Groups["value"].Value + "%}}を下回った！}} 移動をやめるか？";
        return true;
    }
}
