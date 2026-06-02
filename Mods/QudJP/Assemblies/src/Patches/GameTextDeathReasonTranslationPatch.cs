using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameTextDeathReasonTranslationPatch
{
    private const string Context = nameof(GameTextDeathReasonTranslationPatch);

    private static readonly Regex ThirdPersonWasPattern = new(
        "^(?<subject>.+?) (?:was|were) (?<bare>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameTextType = GameTypeResolver.FindType("XRL.GameText", "GameText");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (gameTextType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve GameText or GameObject.", Context);
            return null;
        }

        var method = AccessTools.Method(
            gameTextType,
            "RoughConvertSecondPersonToThirdPerson",
            [typeof(string), gameObjectType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve GameText.RoughConvertSecondPersonToThirdPerson.", Context);
        }

        return method;
    }

    public static void Postfix(ref string? __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            var source = __result!;
            var translated = TranslateThirdPersonDeathReason(source);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(
                    Context,
                    "DeathReason.ThirdPersonConverted",
                    source,
                    translated);
                __result = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateThirdPersonDeathReasonForTests(string source) =>
        TranslateThirdPersonDeathReason(source);

    internal static string TranslateThirdPersonDeathReason(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ThirdPersonWasPattern.Match(stripped);
        if (!match.Success)
        {
            return source;
        }

        var playerKey = "You were " + match.Groups["bare"].Value + ".";
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(playerKey, out var translatedBare))
        {
            return source;
        }

        var subject = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["subject"].Value,
            spans,
            match.Groups["subject"]);
        var translatedSubject = DisplayNameCaptureTranslator.TranslatePreservingColors(subject, Context);
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(translatedSubject, out var strippedSubject))
        {
            if (strippedSubject is null)
            {
                Trace.TraceWarning("QudJP: {0} stripped direct-marked death subject to null.", Context);
            }
            else
            {
                translatedSubject = strippedSubject;
            }
        }

        var translated = translatedSubject + "は" + TrimJapaneseSentenceEnd(translatedBare) + "。";
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static string TrimJapaneseSentenceEnd(string text) =>
        text.EndsWith("。", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
}
