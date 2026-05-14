using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class KeyMappingUiTranslationPatch
{
    private const string Context = nameof(KeyMappingUiTranslationPatch);

    private static readonly Regex LastBindingPattern = new(
        "^Can not remove the last binding for (?<command>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ClearBindingPattern = new(
        "^Are you sure you want to clear this binding for (?<command>.+?)\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var keyMappingUiType = AccessTools.TypeByName("XRL.UI.KeyMappingUI");
        if (keyMappingUiType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var show = AccessTools.Method(keyMappingUiType, "Show", Type.EmptyTypes);
        if (show is not null)
        {
            yield return show;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.Show target not found.", Context);
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

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(source, ref directMarkerPassThroughText, out translated))
        {
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
        var match = LastBindingPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreCommand(match, spans) + "の最後の割り当ては削除できない。";
            detail = "LastBinding";
            return true;
        }

        match = ClearBindingPattern.Match(stripped);
        if (match.Success)
        {
            translated = RestoreCommand(match, spans) + "のこの割り当てを消去してよいか？";
            detail = "ClearBinding";
            return true;
        }

        translated = stripped;
        detail = string.Empty;
        return false;
    }

    private static string RestoreCommand(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var command = match.Groups["command"];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            command.Value.Trim(),
            spans,
            command).Trim();
    }
}
