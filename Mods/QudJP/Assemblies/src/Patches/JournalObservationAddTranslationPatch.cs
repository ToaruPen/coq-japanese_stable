using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Prefix patch for JournalAPI.AddObservation.
/// Translates the observation text and additionalRevealText at storage time.
/// </summary>
[HarmonyPatch]
public static class JournalObservationAddTranslationPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.API.JournalAPI", "JournalAPI");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: JournalObservationAddTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "AddObservation");
        if (method is null)
        {
            Trace.TraceError("QudJP: JournalObservationAddTranslationPatch.AddObservation not found.");
        }

        return method;
    }

    public static void Prefix(
        ref string text,
        string id,
        string category,
        ref string? additionalRevealText)
    {
        try
        {
            _ = id;
            const string route = nameof(JournalObservationAddTranslationPatch);

            if (JournalTextTranslator.TryTranslateAccomplishmentTextForStorage(
                    text, category, route, out var translatedText))
            {
                text = translatedText;
            }

            if (additionalRevealText is not null
                && JournalTextTranslator.TryTranslateObservationRevealTextForStorage(
                    additionalRevealText, route, out var translatedReveal))
            {
                additionalRevealText = translatedReveal;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalObservationAddTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
