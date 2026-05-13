using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EnergyCellSocketAccessPopupTranslationPatch
{
    private const string Context = nameof(EnergyCellSocketAccessPopupTranslationPatch);
    private const string Detail = "AccessEnergyCellOwnershipWarning";
    private static readonly Regex AccessEnergyCellPattern = new(
        "^(?<owner>.+?) (?:is|are) not owned by you\\. Are you sure you want to access (?<possessive>.+?) energy cell\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.EnergyCellSocket");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (targetType is null || gameObjectType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(
            targetType,
            "AttemptReplaceCell",
            new[] { gameObjectType, inventoryActionEventType, typeof(int), gameObjectType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.AttemptReplaceCell target not found.", Context);
            yield break;
        }

        yield return method;
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = AccessEnergyCellPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{TranslateOwner(match, spans)}あなたの所有物ではない。本当に{TranslatePossessive(match, spans)}エネルギーセルにアクセスしますか？";
        _ = family;
        DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + "." + Detail, source, translated);
        return true;
    }

    private static string TranslateOwner(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var owner = RestoreCapture(match, spans, "owner");
        return owner switch
        {
            "This" => "これは",
            "These" => "これらは",
            "That" => "それは",
            "Those" => "それらは",
            _ => owner + "は",
        };
    }

    private static string TranslatePossessive(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var possessive = RestoreCapture(match, spans, "possessive");
        return possessive switch
        {
            "its" or "their" or "his" or "her" => "その",
            "your" => "あなたの",
            _ => possessive + "の",
        };
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
