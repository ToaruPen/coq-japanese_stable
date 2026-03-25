using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Prefix patch for JournalAPI.AddAccomplishment.
/// Translates text, muralText, and gospelText at storage time so the journal
/// persists Japanese text directly.
/// </summary>
[HarmonyPatch]
public static class JournalAccomplishmentAddTranslationPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.API.JournalAPI", "JournalAPI");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: JournalAccomplishmentAddTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "AddAccomplishment");
        if (method is null)
        {
            Trace.TraceError("QudJP: JournalAccomplishmentAddTranslationPatch.AddAccomplishment not found.");
        }

        return method;
    }

    public static void Prefix(
        ref string text,
        ref string? muralText,
        ref string? gospelText,
        string category = "general")
    {
        try
        {
            const string route = nameof(JournalAccomplishmentAddTranslationPatch);

            if (JournalTextTranslator.TryTranslateAccomplishmentTextForStorage(
                    text, category, route, out var translatedText))
            {
                text = translatedText;
            }

            if (muralText is not null
                && JournalTextTranslator.TryTranslateAccomplishmentTextForStorage(
                    muralText, category, route, out var translatedMural))
            {
                muralText = translatedMural;
            }

            if (gospelText is not null
                && JournalTextTranslator.TryTranslateAccomplishmentTextForStorage(
                    gospelText, category, route, out var translatedGospel))
            {
                gospelText = translatedGospel;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalAccomplishmentAddTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    // Required by L2 test which patches both Prefix and Postfix.
    public static void Postfix()
    {
        try
        {
            // Intentionally empty — reserved for future post-storage hooks.
            _ = 0;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalAccomplishmentAddTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
