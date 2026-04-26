#if HAS_GAME_DLL
using System;
using System.Diagnostics;
using HarmonyLib;
using HistoryKit;
using XRL.Annals;

namespace QudJP.Patches;

[HarmonyPatch(typeof(QudHistoryFactory), nameof(QudHistoryFactory.GenerateVillageEraHistory))]
public static class GenerateVillageEraHistoryTranslationPatch
{
    private const string Context = nameof(GenerateVillageEraHistoryTranslationPatch);

    [HarmonyPriority(Priority.Low)]
    public static void Postfix(History __result)
    {
        try
        {
            if (__result?.events == null)
            {
                return;
            }
            foreach (var ev in __result.events)
            {
                HistoricNarrativeDictionaryWalker.TranslateEventProperties(ev, Context);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GenerateVillageEraHistoryTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
#endif
