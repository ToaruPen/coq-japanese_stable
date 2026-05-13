using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MutationActionFailureTranslationPatch
{
    private const string Context = nameof(MutationActionFailureTranslationPatch);
    private static readonly Regex ElectricalGenerationDrinkFailurePattern = new(
        "^You can't seem to drink any of the juice from (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TeleportOtherSelfTargetPattern = new(
        "^You may not teleport (?<target>.+?) with Teleport Other!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (eventType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} event target types not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            "XRL.World.Parts.Mutation.ElectricalGeneration",
            "HandleEvent",
            new[] { inventoryActionEventType });
        AddTarget(
            targets,
            "XRL.World.Parts.Mutation.TeleportOther",
            "FireEvent",
            new[] { eventType });
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
        if (TryTranslateElectricalGenerationDrinkFailure(stripped, spans, out translated)
            || TryTranslateTeleportOtherSelfTarget(stripped, spans, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateElectricalGenerationDrinkFailure(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = ElectricalGenerationDrinkFailurePattern.Match(stripped);
        if (!match.Success)
        {
            translated = stripped;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "item")}から電荷を吸い取れないようだ。";
        return true;
    }

    private static bool TryTranslateTeleportOtherSelfTarget(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = TeleportOtherSelfTargetPattern.Match(stripped);
        if (!match.Success)
        {
            translated = stripped;
            return false;
        }

        translated = $"他者転送で{TranslateTeleportTarget(match, spans)}を転送することはできない！";
        return true;
    }

    private static string TranslateTeleportTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var target = RestoreCapture(match, spans, "target");
        return string.Equals(target, "yourself", StringComparison.Ordinal)
            ? "自分自身"
            : target;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
