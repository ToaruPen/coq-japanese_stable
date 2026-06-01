using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectPossessiveDisplayNameTranslationPatch
{
    internal const string Context = nameof(GameObjectPossessiveDisplayNameTranslationPatch);
    internal const string Family = Context + ".Poss";

    private static readonly Regex PlayerPossessivePattern =
        new("^(?:Your|your)\\s+(?<item>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OwnerPossessivePattern =
        new("^(?<owner>.+?)(?:'s|')\\s+(?<item>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} GameObject target type not found.", Context);
            yield break;
        }

        foreach (var methodName in new[] { "Poss", "poss" })
        {
            var method = AccessTools.Method(
                gameObjectType,
                methodName,
                new[] { gameObjectType, typeof(bool), typeof(bool?) });
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}(GameObject,bool,bool?) target not found.", Context, methodName);
            }
        }
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            var source = __result;
            if (!TryTranslatePossessiveDisplayName(source, out var translated)
                && string.Equals(source, translated, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(source, translated, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Family, source, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslatePossessiveDisplayName(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = string.Empty;
            return false;
        }

        var nonNullSource = source!;
        var cleanedSource = MessageFrameTranslator.TryStripDirectTranslationMarker(nonNullSource, out var unmarked)
            ? unmarked ?? string.Empty
            : nonNullSource;
        translated = cleanedSource;

        var playerMatch = PlayerPossessivePattern.Match(cleanedSource);
        if (playerMatch.Success)
        {
            translated = "あなたの" + TranslateCapture(playerMatch.Groups["item"].Value);
            return true;
        }

        var ownerMatch = OwnerPossessivePattern.Match(cleanedSource);
        if (ownerMatch.Success)
        {
            translated = TranslateCapture(ownerMatch.Groups["owner"].Value)
                + "の"
                + TranslateCapture(ownerMatch.Groups["item"].Value);
            return true;
        }

        return false;
    }

    private static string TranslateCapture(string source)
    {
        var translated = DisplayNameCaptureTranslator.TranslatePreservingColors(source, Context);
        return MessageFrameTranslator.TryStripDirectTranslationMarker(translated, out var stripped)
            ? stripped
            : translated;
    }
}
