using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EnergyLoaderCannotTakeTranslationPatch
{
    private const string Context = nameof(EnergyLoaderCannotTakeTranslationPatch);
    private static readonly Regex CannotTakePattern = new(
        "^(?:The |the |A |a |An |an )?(?<loader>.+?) cannot take (?:the |The |a |A |an |An )?(?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} event type not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Parts.ElectricalDischargeLoader", eventType);
        AddTarget(targets, "XRL.World.Parts.EnergyAmmoLoader", eventType);
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
        _ = route;
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = CannotTakePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "loader")}に{RestoreCapture(match, spans, "item")}を装填できない。";
        DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, Type eventType)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, "FireEvent", new[] { eventType });
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.FireEvent target not found.", Context, targetType.FullName);
    }
}
