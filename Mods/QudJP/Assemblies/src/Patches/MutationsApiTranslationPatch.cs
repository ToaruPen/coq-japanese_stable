using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MutationsApiTranslationPatch
{
    private const string Context = nameof(MutationsApiTranslationPatch);

    private static readonly Regex InsufficientPointsPattern =
        new Regex(
            "^You don't have (?<cost>\\d+) mutation points!$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuyPromptPattern =
        new Regex(
            "^Are you sure you want to spend (?<cost>\\d+) mutation points to buy a new (?<term>.+?)\\?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("Qud.API.MutationsAPI");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: MutationsApiTranslationPatch failed to resolve MutationsAPI or GameObject.");
            return null;
        }

        var method = AccessTools.Method(targetType, "BuyRandomMutation", [gameObjectType, typeof(int), typeof(bool), typeof(string)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: MutationsApiTranslationPatch.BuyRandomMutation(GameObject, int, bool, string) not found.");
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MutationsApiTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: MutationsApiTranslationPatch.Finalizer failed: {0}", ex);
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

        var insufficientMatch = InsufficientPointsPattern.Match(stripped);
        if (insufficientMatch.Success)
        {
            translated = string.Concat(
                "突然変異ポイントが",
                insufficientMatch.Groups["cost"].Value,
                "ポイント足りない！");
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".InsufficientPoints", source, translated);
            return true;
        }

        var promptMatch = BuyPromptPattern.Match(stripped);
        if (promptMatch.Success)
        {
            translated = string.Concat(
                "本当に",
                promptMatch.Groups["cost"].Value,
                "ポイントを消費して新しい",
                TranslateMutationTerm(promptMatch.Groups["term"], spans),
                "を購入しますか？");
            DynamicTextObservability.RecordTransform(route, family + "." + Context + ".BuyPrompt", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateMutationTerm(Group termGroup, IReadOnlyList<ColorSpan> spans)
    {
        var restored = ColorAwareTranslationComposer.RestoreCapture(termGroup.Value, spans, termGroup).Trim();
        var translated = ChargenStructuredTextTranslator.Translate(restored);
        if (!string.Equals(translated, restored, StringComparison.Ordinal))
        {
            return translated;
        }

        return StringHelpers.TryGetTranslationExactOrLowerAscii(termGroup.Value.Trim(), out translated)
            ? ColorAwareTranslationComposer.RestoreCapture(translated, spans, termGroup).Trim()
            : restored;
    }
}
