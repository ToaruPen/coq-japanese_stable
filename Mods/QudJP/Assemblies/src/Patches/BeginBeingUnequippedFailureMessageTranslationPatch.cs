using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BeginBeingUnequippedFailureMessageTranslationPatch
{
    private const string Context = nameof(BeginBeingUnequippedFailureMessageTranslationPatch);
    private const string Detail = "CannotRemoveItem";

    private static readonly Regex CannotRemovePattern = new(
        "^You can't remove (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.BeginBeingUnequippedEvent");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "AddFailureMessage", [typeof(string)]);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.AddFailureMessage target not found.", Context);
    }

    public static void Prefix(ref string Message)
    {
        try
        {
            if (TryTranslateFailureMessage(Message, out var translated))
            {
                DynamicTextObservability.RecordTransform(Context, Detail, Message, translated);
                Message = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateFailureMessage(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var sourceValue = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(sourceValue);
        var match = CannotRemovePattern.Match(stripped);
        if (!match.Success)
        {
            translated = sourceValue;
            return false;
        }

        translated = TranslateItemCapture(match, spans) + "を外せない。";
        return true;
    }

    private static string TranslateItemCapture(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["item"];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();

        try
        {
            return GetDisplayNameRouteTranslator.TranslatePreservingColors(restored, Context + "." + Detail);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TranslateItemCapture failed: {1}", Context, ex);
            return restored;
        }
    }
}
