using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LoadingStatusTranslationPatch
{
    private const string Context = nameof(LoadingStatusTranslationPatch);
    private static readonly Regex RestingUntilHealedTurnPattern = new(
        "^Resting until (?<party>party )?healed\\.\\.\\. Turn: (?<turn>\\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("XRL.UI.Loading");
        if (type is null)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch target type 'XRL.UI.Loading' not found.");
            return null;
        }

        var method = AccessTools.Method(type, "SetLoadingStatus", new[] { typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch method 'SetLoadingStatus' not found on 'XRL.UI.Loading'.");
        }

        return method;
    }

    public static void Prefix(ref string description)
    {
        try
        {
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(description, out var stripped))
            {
                description = stripped;
                return;
            }

            var restingMatch = RestingUntilHealedTurnPattern.Match(description);
            if (restingMatch.Success)
            {
                var subject = restingMatch.Groups["party"].Success ? "パーティが" : string.Empty;
                var translatedResting = $"{subject}回復するまで休息中… ターン: {restingMatch.Groups["turn"].Value}";
                DynamicTextObservability.RecordTransform(Context, "Loading.RestingUntilHealedTurn", description, translatedResting);
                description = translatedResting;
                return;
            }

            var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
                description,
                static visible => StringHelpers.TranslateExactOrLowerAsciiFallback(visible));
            if (!string.Equals(translated, description, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(Context, "Loading.Exact", description, translated);
                description = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LoadingStatusTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
