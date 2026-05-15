using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicsInventoryActionPopupTranslationPatch
{
    private const string Context = nameof(PhysicsInventoryActionPopupTranslationPatch);

    private static readonly Regex NoCleaningLiquidPattern = new(
        "^You don't have any (?<liquid>.+?) to clean (?<target>.+?) with\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Physics");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (targetType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [inventoryActionEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) target not found.", Context);
        }

        return method;
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
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(source, ref directMarkerPassThroughText, out translated))
        {
            return true;
        }

        var ownerFamily = family + "." + Context;
        if (LiquidVolumeFragmentTranslator.TryTranslatePopupMessage(source, route, ownerFamily, out translated))
        {
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateNoCleaningLiquid(source, stripped, spans, route, ownerFamily, out translated))
        {
            return true;
        }

        if (PopupTranslationPatch.TryTranslatePhysicsAttackConfirmText(stripped, spans, out translated))
        {
            DynamicTextObservability.RecordTransform(
                route,
                ownerFamily + ".PhysicsAttackConfirm",
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateNoCleaningLiquid(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = NoCleaningLiquidPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = string.Concat(
            TranslateTarget(match, spans),
            "を清掃するための",
            RestoreCapture(match, spans, "liquid"),
            "がない。");
        DynamicTextObservability.RecordTransform(
            route,
            family + ".NoCleaningLiquid",
            source,
            translated);
        return true;
    }

    private static string TranslateTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var target = match.Groups["target"];
        var cleaned = StringHelpers.StripLeadingEnglishArticle(
            target.Value.Trim(),
            includeCapitalizedDefiniteArticle: true);
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(cleaned, spans, target).Trim();
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
