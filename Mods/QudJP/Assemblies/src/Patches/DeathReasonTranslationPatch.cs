using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Observes Reason and ThirdPersonReason parameters passed to GameObject.Die().
/// Producer-owned translations may arrive pre-marked; sink-side handling only strips markers and logs unclaimed text.
/// </summary>
[HarmonyPatch]
public static class DeathReasonTranslationPatch
{
    private const string Context = nameof(DeathReasonTranslationPatch);

    private static readonly Regex ExplodeThirdPersonReasonPattern = new(
        "^(?<subject>.+?) @@(?<cause>exploded|crushed under the weight of a thousand suns)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var type = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (type is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve GameObject type.", Context);
            return null;
        }

        var method = AccessTools.Method(type, "Die");
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Die method.", Context);
        }

        return method;
    }

    public static void Prefix(ref string Reason, ref string ThirdPersonReason)
    {
        try
        {
            if (!string.IsNullOrEmpty(Reason))
            {
                var translated = TranslateDeathReason(Reason);
                if (!string.Equals(translated, Reason, StringComparison.Ordinal))
                {
                    DynamicTextObservability.RecordTransform(
                        Context, "DeathReason.Reason", Reason, translated);
                    Reason = translated;
                }
            }

            if (!string.IsNullOrEmpty(ThirdPersonReason))
            {
                var translated = TranslateDeathReason(ThirdPersonReason);
                if (!string.Equals(translated, ThirdPersonReason, StringComparison.Ordinal))
                {
                    DynamicTextObservability.RecordTransform(
                        Context, "DeathReason.ThirdPerson", ThirdPersonReason, translated);
                    ThirdPersonReason = translated;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateDeathReason(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return reason ?? string.Empty;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(reason, out var markedText))
        {
            return markedText;
        }

        if (TryTranslateExplodeThirdPersonReason(reason, out var generatedTranslated))
        {
            return generatedTranslated;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            reason,
            static visible => StringHelpers.TryGetTranslationExactOrLowerAscii(visible, out var translated)
                ? translated
                : visible);
    }

    private static bool TryTranslateExplodeThirdPersonReason(string reason, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(reason);
        var match = ExplodeThirdPersonReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = reason;
            return false;
        }

        var bodyKey = match.Groups["cause"].Value switch
        {
            "exploded" => "QudJP.DeathWrapper.Exploded.Bare",
            "crushed under the weight of a thousand suns" => "QudJP.DeathWrapper.CrushedUnderSuns.Bare",
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(bodyKey)
            || !StringHelpers.TryGetTranslationExactOrLowerAscii(bodyKey, out var bodyTranslation))
        {
            translated = reason;
            return false;
        }

        var subject = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["subject"].Value,
            spans,
            match.Groups["subject"]);
        var translatedSubject = GetDisplayNameRouteTranslator.TranslatePreservingColors(subject, Context);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedSubject + "は" + bodyTranslation,
            spans,
            stripped.Length,
            reason);
        return true;
    }
}
