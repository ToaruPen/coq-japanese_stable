#if HAS_GAME_DLL
using System;
using System.Diagnostics;
using HarmonyLib;
using HistoryKit;
using Qud.API;

namespace QudJP.Patches;

[HarmonyPatch(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), typeof(HistoricEntity))]
public static class AddVillageGospelsTranslationPatch
{
    private const string Context = nameof(AddVillageGospelsTranslationPatch);

    [HarmonyPriority(Priority.Low)]
    public static void Prefix(HistoricEntity Village)
    {
        try
        {
            HistoricNarrativeDictionaryWalker.TranslateEntity(Village, Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: AddVillageGospelsTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
#endif
