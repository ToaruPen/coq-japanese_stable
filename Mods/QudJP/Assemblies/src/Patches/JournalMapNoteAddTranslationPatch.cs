using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Prefix patch for JournalAPI.AddMapNote.
/// Translates the note text at storage time, skipping categories
/// that should remain untranslated (Miscellaneous, Named Locations).
/// </summary>
[HarmonyPatch]
public static class JournalMapNoteAddTranslationPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.API.JournalAPI", "JournalAPI");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: JournalMapNoteAddTranslationPatch target type not found.");
            return null;
        }

        // AddMapNote has two overloads: AddMapNote(JournalMapNote) and
        // AddMapNote(string, string, string, string[], string, bool, bool, long, bool).
        // We target the string-based overload.
        var method = AccessTools.Method(
            targetType,
            "AddMapNote",
            new[]
            {
                typeof(string),   // ZoneID
                typeof(string),   // text
                typeof(string),   // category
                typeof(string[]), // attributes
                typeof(string),   // secretId
                typeof(bool),     // revealed
                typeof(bool),     // sold
                typeof(long),     // time
                typeof(bool),     // silent
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: JournalMapNoteAddTranslationPatch.AddMapNote not found.");
        }

        return method;
    }

    public static void Prefix(
        string ZoneID,
        ref string text,
        string category = "general")
    {
        try
        {
            _ = ZoneID;
            const string route = nameof(JournalMapNoteAddTranslationPatch);

            if (JournalTextTranslator.TryTranslateMapNoteTextForStorage(
                    text, category, route, out var translated))
            {
                text = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalMapNoteAddTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
