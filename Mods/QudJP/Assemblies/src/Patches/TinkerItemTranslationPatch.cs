using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkerItemTranslationPatch
{
    private const string Context = nameof(TinkerItemTranslationPatch);

    private static readonly Regex CannotAffectPattern = new(
        "^You cannot seem to affect (?<target>.+?) in any way\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerOwnedDisassemblyPattern = new(
        "^(?<container>.+?)(?: ?(?:is|are)) not owned by you\\. Are you sure you want to disassemble (?<target>.+?) inside (?<inside>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OwnedItemDisassemblyPattern = new(
        "^(?<owner>.+?)(?: ?(?:is|are)) not owned by you\\. Are you sure you want to disassemble (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DisassemblyConfirmationPattern = new(
        "^Are you sure you want to disassemble (?<target>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var tinkerItemType = AccessTools.TypeByName("XRL.World.Parts.TinkerItem");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (tinkerItemType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var handleEvent = AccessTools.Method(tinkerItemType, "HandleEvent", [inventoryActionEventType]);
        if (handleEvent is not null)
        {
            yield return handleEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) target not found.", Context);
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

        if (PopupShowTranslationPatch.TryConsumeDirectMarkerPassThrough(source, ref directMarkerPassThroughText))
        {
            translated = source;
            return true;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            directMarkerPassThroughText = markedText;
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslateCore(stripped, spans, out translated, out var detail))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
        return true;
    }

    private static bool TryTranslateCore(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CannotAffectPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreObject(match, spans, "target") + "にはどうやっても影響を与えられそうにない。";
            detail = "CannotAffect";
            return true;
        }

        match = ContainerOwnedDisassemblyPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreObject(match, spans, "container")
                + "はあなたのものではない。"
                + TranslateInside(match, spans)
                + "の"
                + TranslateDisassemblyTarget(match, spans, "target")
                + "を分解してよいか？";
            detail = "ContainerOwnedDisassembly";
            return true;
        }

        match = OwnedItemDisassemblyPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreObject(match, spans, "owner")
                + "はあなたのものではない。"
                + TranslatePronounOrObject(match, spans, "target")
                + "を分解してよいか？";
            detail = "OwnedItemDisassembly";
            return true;
        }

        match = DisassemblyConfirmationPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateDisassemblyConfirmationTarget(match, spans) + "分解してよいか？";
            detail = "DisassemblyConfirmation";
            return true;
        }

        translated = stripped;
        detail = string.Empty;
        return false;
    }

    private static string TranslateInside(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var inside = match.Groups["inside"].Value.Trim();
        if (inside is "it" or "them" or "him" or "her")
        {
            return "その中";
        }

        return RestoreObject(match, spans, "inside") + "の中";
    }

    private static string TranslatePronounOrObject(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var value = match.Groups[groupName].Value.Trim();
        return value switch
        {
            "it" => "それ",
            "them" => "それら",
            "him" or "her" => "その人",
            _ => RestoreObject(match, spans, groupName),
        };
    }

    private static string TranslateDisassemblyTarget(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var target = match.Groups[groupName];
        var value = target.Value.Trim();
        if (value.StartsWith("all the ", StringComparison.Ordinal))
        {
            return RestoreCapture(
                StringHelpers.StripLeadingEnglishArticle(value.Substring("all ".Length)),
                spans,
                target) + "をすべて";
        }

        if (value.StartsWith("all ", StringComparison.Ordinal))
        {
            return RestoreCapture(
                StringHelpers.StripLeadingEnglishArticle(value.Substring("all ".Length)),
                spans,
                target) + "をすべて";
        }

        if (value == "items")
        {
            return "アイテム";
        }

        return TranslatePronounOrObject(match, spans, groupName);
    }

    private static string TranslateDisassemblyConfirmationTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var target = match.Groups["target"];
        var value = target.Value.Trim();
        if (value.StartsWith("all the ", StringComparison.Ordinal))
        {
            return RestoreCapture(
                StringHelpers.StripLeadingEnglishArticle(value.Substring("all ".Length)),
                spans,
                target) + "をすべて";
        }

        if (value.StartsWith("all ", StringComparison.Ordinal))
        {
            return RestoreCapture(
                StringHelpers.StripLeadingEnglishArticle(value.Substring("all ".Length)),
                spans,
                target) + "をすべて";
        }

        return TranslatePronounOrObject(match, spans, "target") + "を";
    }

    private static string RestoreObject(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var cleaned = StringHelpers.StripLeadingEnglishArticle(
            group.Value.Trim(),
            includeCapitalizedDefiniteArticle: true);
        return RestoreCapture(cleaned, spans, group);
    }

    private static string RestoreCapture(string value, IReadOnlyList<ColorSpan> spans, Group group)
    {
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(value, spans, group).Trim();
    }
}
