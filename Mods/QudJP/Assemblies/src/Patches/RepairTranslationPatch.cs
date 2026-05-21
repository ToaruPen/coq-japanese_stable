using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RepairTranslationPatch
{
    private const string Context = nameof(RepairTranslationPatch);

    private static readonly Regex OwnershipRiskPattern =
        new Regex(
            "^(?<owner>.+?) (?:is|are) not owned by you, and trying to repair (?<target>.+?) risks damaging (?<risk>.+?)\\. Are you sure you want to do so\\?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ContainerOwnershipRiskPattern =
        new Regex(
            "^(?<owner>.+?) (?:is|are) not owned by you, and trying to repair (?<target>.+?) inside (?<container>.+?) risks causing damage\\. Are you sure you want to do so\\?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RepairSuccessPattern =
        new Regex(
            "^You repair (?<target>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotRepairUntilUnderstandPattern =
        new Regex(
            "^You cannot repair (?<target>.+?) until you understand (?<them>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotRepairPattern =
        new Regex(
            "^You cannot repair (?<target>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotReachPattern =
        new Regex(
            "^You cannot reach (?<target>.+?) to repair (?<pronoun>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutOfPhasePattern =
        new Regex(
            "^You are out of phase with (?<target>.+?) and cannot repair (?<pronoun>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MissingBitsPattern =
        new Regex(
            "^You don't have <(?<bits>.+?)> to repair (?<target>.+?)\\. You have:\\n\\n(?<owned>.*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SpendBitsPattern =
        new Regex(
            "^Do you want to spend <(?<bits>.+?)> to repair (?<target>.+?)\\? You have:\\n\\n(?<owned>.*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TinkeringBitsPattern =
        new Regex(
            "^You receive tinkering bits <(?<bits>.+?)>$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PartialSuccessPattern =
        new Regex(
            "^You make some progress repairing (?<target>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FailurePattern =
        new Regex(
            "^You can't figure out how to fix (?<target>.+?)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CriticalFailurePattern =
        new Regex(
            "^You think you broke (?<target>.+?)\\.\\.\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (inventoryActionEventType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve InventoryActionEvent or GameObject.", Context);
            yield break;
        }

        foreach (var typeName in new[] { "XRL.World.Parts.Repair", "XRL.World.Parts.Skill.Tinkering_Repair" })
        {
            var repairType = AccessTools.TypeByName(typeName);
            if (repairType is null)
            {
                Trace.TraceError("QudJP: {0} failed to resolve {1}.", Context, typeName);
                continue;
            }

            var handleEvent = AccessTools.Method(repairType, "HandleEvent", [inventoryActionEventType]);
            if (handleEvent is not null)
            {
                yield return handleEvent;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}.HandleEvent(InventoryActionEvent) not found.", Context, typeName);
            }

            foreach (var methodName in new[]
                     {
                         "RepairResultSuccess",
                         "RepairResultExceptionalSuccess",
                         "RepairResultPartialSuccess",
                         "RepairResultFailure",
                         "RepairResultCriticalFailure",
                     })
            {
                var method = AccessTools.Method(repairType, methodName, [gameObjectType, gameObjectType]);
                if (method is not null)
                {
                    yield return method;
                }
                else
                {
                    Trace.TraceError("QudJP: {0}.{1}.{2}(GameObject, GameObject) not found.", Context, typeName, methodName);
                }
            }
        }
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
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
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        return TryTranslateContainerOwnershipRisk(source, stripped, spans, route, family, out translated)
            || TryTranslateOwnershipRisk(source, stripped, spans, route, family, out translated)
            || TryTranslateCannotReach(source, stripped, spans, route, family, out translated)
            || TryTranslateOutOfPhase(source, stripped, spans, route, family, out translated)
            || TryTranslateCannotRepairUntilUnderstand(source, stripped, spans, route, family, out translated)
            || TryTranslateCannotRepair(source, stripped, spans, route, family, out translated)
            || TryTranslateMissingBits(source, stripped, spans, route, family, out translated)
            || TryTranslateSpendBits(source, stripped, spans, route, family, out translated)
            || TryTranslateSuccess(source, stripped, spans, route, family, out translated)
            || TryTranslateTinkeringBits(source, stripped, spans, route, family, out translated)
            || TryTranslatePartialSuccess(source, stripped, spans, route, family, out translated)
            || TryTranslateFailure(source, stripped, spans, route, family, out translated)
            || TryTranslateCriticalFailure(source, stripped, spans, route, family, out translated);
    }

    private static bool TryTranslateOwnershipRisk(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = OwnershipRiskPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(
                RestoreDisplayNameCapture(match, spans, "owner"),
                "はあなたのものではなく、",
                RestoreDisplayNameCapture(match, spans, "target"),
                "を修理しようとすると",
                RestoreDisplayNameCapture(match, spans, "risk"),
                "を損傷させる危険がある。本当に行いますか？"),
            stripped,
            spans);
        Record(route, family, "OwnershipRisk", source, translated);
        return true;
    }

    private static bool TryTranslateContainerOwnershipRisk(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ContainerOwnershipRiskPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(
                RestoreDisplayNameCapture(match, spans, "owner"),
                "はあなたのものではなく、",
                RestoreDisplayNameCapture(match, spans, "container"),
                "の中にある",
                RestoreDisplayNameCapture(match, spans, "target"),
                "を修理しようとすると損傷を引き起こす危険がある。本当に行いますか？"),
            stripped,
            spans);
        Record(route, family, "ContainerOwnershipRisk", source, translated);
        return true;
    }

    private static bool TryTranslateCannotRepairUntilUnderstand(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = CannotRepairUntilUnderstandPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "について理解するまで、修理できない。"),
            stripped,
            spans);
        Record(route, family, "CannotRepairUntilUnderstand", source, translated);
        return true;
    }

    private static bool TryTranslateCannotReach(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = CannotReachPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "に手が届かず、修理できない。"),
            stripped,
            spans);
        Record(route, family, "CannotReach", source, translated);
        return true;
    }

    private static bool TryTranslateOutOfPhase(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = OutOfPhasePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "とは位相がずれているため、修理できない。"),
            stripped,
            spans);
        Record(route, family, "OutOfPhase", source, translated);
        return true;
    }

    private static bool TryTranslateCannotRepair(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = CannotRepairPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "は修理できない。"),
            stripped,
            spans);
        Record(route, family, "CannotRepair", source, translated);
        return true;
    }

    private static bool TryTranslateMissingBits(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = MissingBitsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(
                RestoreDisplayNameCapture(match, spans, "target"),
                "を修理するための<",
                RestoreCapture(match, spans, "bits"),
                ">がない。所持ビット:\n\n",
                RestoreCapture(match, spans, "owned")),
            stripped,
            spans);
        Record(route, family, "MissingBits", source, translated);
        return true;
    }

    private static bool TryTranslateSpendBits(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = SpendBitsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(
                RestoreDisplayNameCapture(match, spans, "target"),
                "を修理するために<",
                RestoreCapture(match, spans, "bits"),
                ">を消費しますか？所持ビット:\n\n",
                RestoreCapture(match, spans, "owned")),
            stripped,
            spans);
        Record(route, family, "SpendBits", source, translated);
        return true;
    }

    private static bool TryTranslateSuccess(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = RepairSuccessPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "を修理した。"),
            stripped,
            spans);
        Record(route, family, "Success", source, translated);
        return true;
    }

    private static bool TryTranslateTinkeringBits(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = TinkeringBitsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat("修理ビット<", RestoreCapture(match, spans, "bits"), ">を受け取った。"),
            stripped,
            spans);
        Record(route, family, "TinkeringBits", source, translated);
        return true;
    }

    private static bool TryTranslatePartialSuccess(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = PartialSuccessPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "の修理が少し進んだ。"),
            stripped,
            spans);
        Record(route, family, "PartialSuccess", source, translated);
        return true;
    }

    private static bool TryTranslateFailure(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = FailurePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "の修理方法がわからない。"),
            stripped,
            spans);
        Record(route, family, "Failure", source, translated);
        return true;
    }

    private static bool TryTranslateCriticalFailure(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = CriticalFailurePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(RestoreDisplayNameCapture(match, spans, "target"), "を壊してしまったようだ..."),
            stripped,
            spans);
        Record(route, family, "CriticalFailure", source, translated);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreDisplayNameCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return DisplayNameCaptureTranslator.TranslatePreservingColors(RestoreCapture(match, spans, groupName), Context);
    }

    private static string RestoreWholeSourceBoundary(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
