using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ConversationPronounExchangeTranslationPatch
{
    private const string Context = nameof(ConversationPronounExchangeTranslationPatch);

    private static readonly Regex ExchangePattern = new(
        "^(?:you|You) and (?<speaker>.+) exchange pronouns; (?<its>.+?) are (?<pronouns>.+?)\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GiveToPlayerPattern = new(
        "^(?<speaker>.+?) give(?:s)? you (?<its>.+?) pronouns, which are (?<pronouns>.+?)\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GiveNewPattern = new(
        "^(?:you|You) give (?<speaker>.+?) your new pronouns\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GiveCurrentPattern = new(
        "^(?:you|You) give (?<speaker>.+?) your pronouns\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.ConversationScript");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: ConversationPronounExchangeTranslationPatch target type not found.");
            return null;
        }

        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var method = gameObjectType is null
            ? null
            : AccessTools.Method(
                targetType,
                "PronounExchangeDescription",
                new[] { gameObjectType, gameObjectType, typeof(bool), typeof(bool), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: ConversationPronounExchangeTranslationPatch.PronounExchangeDescription not found.");
        }

        return method;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            if (!TryTranslate(__result, out var translated))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, "Conversation.PronounExchange", __result, translated);
            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ConversationPronounExchangeTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static bool TryTranslate(string source, out string translated)
    {
        translated = source;

        var match = ExchangePattern.Match(source);
        if (match.Success)
        {
            var speaker = match.Groups["speaker"].Value;
            translated = $"{speaker}と代名詞を交換した。{speaker}の代名詞は{match.Groups["pronouns"].Value}。";
            return true;
        }

        match = GiveToPlayerPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups["speaker"].Value}が代名詞を教えてくれた。{match.Groups["pronouns"].Value}。";
            return true;
        }

        match = GiveNewPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups["speaker"].Value}に新しい代名詞を伝えた。";
            return true;
        }

        match = GiveCurrentPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups["speaker"].Value}に代名詞を伝えた。";
            return true;
        }

        return false;
    }
}
