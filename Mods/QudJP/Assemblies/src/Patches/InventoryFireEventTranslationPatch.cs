using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryFireEventTranslationPatch
{
    private const string Context = nameof(InventoryFireEventTranslationPatch);

    private static readonly Regex GraveyardZoneQueuePattern = new(
        "^(?<owner>.+?)] Error dropping object, removing to graveyard zone! \\(Inventory\\.cs:CommandEquipObject\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerOwnershipPromptPattern = new(
        "^You don't own (?<container>.+?)\\. Are you sure you want to take (?<item>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotEquipPattern = new(
        "^You cannot equip (?:your )?(?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotEquipOnSlotPattern = new(
        "^You cannot equip (?:your )?(?<item>.+?) on your (?<slot>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotBudgePattern = new(
        "^You cannot budge (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var inventoryType = AccessTools.TypeByName("XRL.World.Parts.Inventory");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (inventoryType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(inventoryType, "FireEvent", [eventType]);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.FireEvent(Event) target not found.", Context);
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

        if (!TryTranslateGraveyardZoneMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".GraveyardZoneRecovery",
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

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(source, ref directMarkerPassThroughText, out translated))
        {
            return true;
        }

        if (!TryTranslateContainerOwnershipPrompt(source, out translated)
            && !TryTranslateInventoryFailurePopup(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + GetPopupFamilyDetail(source),
            source,
            translated);
        return true;
    }

    private static bool TryTranslateGraveyardZoneMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = GraveyardZoneQueuePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = Restore(match, spans, "owner")
            + "] オブジェクトを落とせません。墓地ゾーンに移動します！ (Inventory.cs:CommandEquipObject)";
        return true;
    }

    private static bool TryTranslateContainerOwnershipPrompt(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ContainerOwnershipPromptPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = Restore(match, spans, "container") + "はあなたのものではない。"
            + "本当に" + Restore(match, spans, "item") + "を取りますか？";
        return true;
    }

    private static bool TryTranslateInventoryFailurePopup(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        switch (stripped)
        {
            case "You cannot equip items while stuck!":
                translated = RestoreWholeSource("動けない間はアイテムを装備できない！", spans, stripped, source);
                return true;
            case "You cannot remove items while stuck!":
                translated = RestoreWholeSource("動けない間はアイテムを外せない！", spans, stripped, source);
                return true;
        }

        var equipOnSlotMatch = CannotEquipOnSlotPattern.Match(stripped);
        if (equipOnSlotMatch.Success)
        {
            translated = TranslateItemCapture(Restore(equipOnSlotMatch, spans, "item"))
                + "を"
                + TranslateInventorySlot(Restore(equipOnSlotMatch, spans, "slot"))
                + "に装備できない。";
            translated = RestoreWholeSource(translated, spans, stripped, source);
            return true;
        }

        var equipMatch = CannotEquipPattern.Match(stripped);
        if (equipMatch.Success)
        {
            translated = TranslateItemCapture(Restore(equipMatch, spans, "item")) + "を装備できない。";
            translated = RestoreWholeSource(translated, spans, stripped, source);
            return true;
        }

        var budgeMatch = CannotBudgePattern.Match(stripped);
        if (budgeMatch.Success)
        {
            translated = TranslateItemCapture(Restore(budgeMatch, spans, "item")) + "を動かせない。";
            translated = RestoreWholeSource(translated, spans, stripped, source);
            return true;
        }

        translated = source;
        return false;
    }

    private static string GetPopupFamilyDetail(string source)
    {
        if (ContainerOwnershipPromptPattern.IsMatch(ColorAwareTranslationComposer.Strip(source).stripped))
        {
            return "ContainerOwnershipPrompt";
        }

        return "InventoryFailurePopup";
    }

    private static string TranslateItemCapture(string source)
    {
        try
        {
            return GetDisplayNameRouteTranslator.TranslatePreservingColors(
                source,
                Context + ".InventoryFailurePopup");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateItemCapture failed: {1}", Context, ex);
            return source;
        }
    }

    private static string TranslateInventorySlot(string source)
    {
        try
        {
            var visible = ColorAwareTranslationComposer.GetVisibleText(source).Trim();
            if (TryTranslateInventorySlotName(visible, out var slotName))
            {
                return ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => slotName);
            }

            var translated = Translator.Translate(source);
            return string.Equals(translated, source, StringComparison.Ordinal)
                ? source
                : translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateInventorySlot failed: {1}", Context, ex);
            return source;
        }
    }

    private static bool TryTranslateInventorySlotName(string source, out string translated)
    {
        translated = source.ToUpperInvariant() switch
        {
            "RIGHT HAND" => "右手",
            "LEFT HAND" => "左手",
            "RIGHT FOOT" => "右足",
            "LEFT FOOT" => "左足",
            "RIGHT ARM" => "右腕",
            "LEFT ARM" => "左腕",
            "HAND" => "手",
            "FOOT" => "足",
            "HEAD" => "頭",
            "FACE" => "顔",
            "ARM" => "腕",
            "LEG" => "脚",
            "TAIL" => "尾",
            "WING" => "翼",
            "HORN" => "角",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWholeSource(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        string stripped,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }
}
