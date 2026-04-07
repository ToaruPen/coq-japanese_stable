using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AbilityBarAfterRenderTranslationPatch
{
    private const string Context = nameof(AbilityBarAfterRenderTranslationPatch);

    private static readonly Regex ActiveEffectsPattern =
        new Regex("^(?<label>ACTIVE EFFECTS:)(?: (?<tail>.+))?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TargetTextPattern =
        new Regex("^(?<label>TARGET:)\\s+(?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.AbilityBar", "AbilityBar");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: AbilityBarAfterRenderTranslationPatch target type not found.");
            return null;
        }

        var xrlCoreType = AccessTools.TypeByName("XRL.Core.XRLCore");
        var screenBufferType = AccessTools.TypeByName("ConsoleLib.Console.ScreenBuffer");
        if (xrlCoreType is null || screenBufferType is null)
        {
            Trace.TraceError("QudJP: AbilityBarAfterRenderTranslationPatch parameter types not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "AfterRender", new[] { xrlCoreType, screenBufferType });
        if (method is null)
        {
            Trace.TraceError("QudJP: AbilityBarAfterRenderTranslationPatch.AfterRender not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            TranslateField(__instance, "effectText", TryTranslateEffectText);
            TranslateField(__instance, "targetText", TryTranslateTargetText);
            TranslateField(__instance, "targetHealthText", TryTranslateTargetHealthText);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AbilityBarAfterRenderTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static void TranslateField(object instance, string fieldName, TryTranslateText translate)
    {
        var field = AccessTools.Field(instance.GetType(), fieldName);
        if (field is null || field.FieldType != typeof(string))
        {
            return;
        }

        var current = field.GetValue(instance) as string;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "field=" + fieldName);
        if (!translate(current!, route, out var translated)
            || string.Equals(current, translated, StringComparison.Ordinal))
        {
            return;
        }

        field.SetValue(instance, translated);
    }

    // Keep color-aware restoration here even if shared active-effects parsing is
    // later extracted alongside StatusLineTranslationHelpers.TryTranslateActiveEffectsLine.
    private static bool TryTranslateEffectText(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ActiveEffectsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedLabel = Translator.Translate("ACTIVE EFFECTS:");
        if (string.Equals(translatedLabel, "ACTIVE EFFECTS:", StringComparison.Ordinal))
        {
            translatedLabel = match.Groups["label"].Value;
        }

        translated = ColorAwareTranslationComposer.RestoreCapture(translatedLabel, spans, match.Groups["label"]);
        var tailGroup = match.Groups["tail"];
        if (tailGroup.Success)
        {
            translated += " " + TranslateActiveEffectTailParts(tailGroup.Value, spans, tailGroup.Index);
        }

        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "AbilityBar.ActiveEffects", source, translated);
        return true;
    }

    private static string TranslateActiveEffectTailParts(string tail, IReadOnlyList<ColorSpan>? spans, int startIndex)
    {
        var translatedParts = new List<string>();
        var partStart = 0;

        while (partStart < tail.Length)
        {
            var separatorIndex = tail.IndexOf(", ", partStart, StringComparison.Ordinal);
            var partLength = separatorIndex >= 0 ? separatorIndex - partStart : tail.Length - partStart;
            if (partLength > 0)
            {
                var part = tail.Substring(partStart, partLength);
                var translatedPart = StringHelpers.TranslateExactOrLowerAscii(part);
                translatedParts.Add(ColorAwareTranslationComposer.RestoreSlice(translatedPart ?? part, spans, startIndex + partStart, partLength));
            }

            if (separatorIndex < 0)
            {
                break;
            }

            partStart = separatorIndex + 2;
        }

        return string.Join("、", translatedParts);
    }

    private static bool TryTranslateTargetText(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = TargetTextPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedLabel = StringHelpers.TranslateExactOrLowerAscii(match.Groups["label"].Value);
        if (translatedLabel is null)
        {
            translated = source;
            return false;
        }

        var nameSource = match.Groups["name"].Value;
        var translatedName = StringHelpers.TranslateExactOrLowerAscii(nameSource);
        if (translatedName is null)
        {
            translatedName = GetDisplayNameRouteTranslator.TranslatePreservingColors(
                nameSource,
                ObservabilityHelpers.ComposeContext(Context, "field=targetText", "segment=name"));
        }

        translated =
            ColorAwareTranslationComposer.RestoreCapture(translatedLabel, spans, match.Groups["label"])
            + " "
            + ColorAwareTranslationComposer.RestoreCapture(translatedName, spans, match.Groups["name"]);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "AbilityBar.TargetText", source, translated);
        return true;
    }

    private static bool TryTranslateTargetHealthText(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (StatusLineTranslationHelpers.TryTranslateCompareStatusSequence(
                stripped,
                route,
                "AbilityBar.TargetHealth",
                out var translatedVisible))
        {
            translated = ColorAwareTranslationComposer.Restore(translatedVisible, spans);
            return true;
        }

        var direct = StringHelpers.TranslateExactOrLowerAscii(stripped);
        if (direct is null)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.Restore(direct, spans);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "AbilityBar.TargetHealth.Exact", source, translated);
        return true;
    }

    private delegate bool TryTranslateText(string source, string route, out string translated);
}
