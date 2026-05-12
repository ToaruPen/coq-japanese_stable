using System;
using System.Diagnostics;

namespace QudJP.Patches;

internal static class PopupShowSemanticPipeline
{
    private const string PopupShowFamily = "Popup.Show";

    private static readonly PopupMessageTranslator[] Translators =
    [
        TryTranslatePerformOfferTradeWaterMessage,
        TryTranslateHasNothingToTradeMessage,
        GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage,
        GameObjectMoveTranslationPatch.TryTranslatePopupMessage,
        GameObjectPerformThrowTranslationPatch.TryTranslatePopupMessage,
        GameObjectPopupTranslationPatch.TryTranslatePopupMessage,
        RealityStabilizedInterdictTranslationPatch.TryTranslatePopupMessage,
        HackingSifrahResultTranslationPatch.TryTranslatePopupMessage,
        QuestLifecyclePopupTranslationPatch.TryTranslatePopupMessage,
        BodyTranslationPatch.TryTranslatePopupMessage,
        SifrahTokenItemPopupTranslationPatch.TryTranslatePopupMessage,
        SifrahPureOwnerPopupTranslationPatch.TryTranslatePopupMessage,
        ItemModdingSifrahTranslationPatch.TryTranslatePopupMessage,
        SunderMindTranslationPatch.TryTranslatePopupMessage,
        KeybindsScreenConflictTranslationPatch.TryTranslatePopupMessage,
        AbilityManagerPopupTranslationPatch.TryTranslatePopupMessage,
        RealityStabilizedEventTranslationPatch.TryTranslatePopupMessage,
        GeomagneticDiscTranslationPatch.TryTranslatePopupMessage,
        CampfireCookAvailabilityTranslationPatch.TryTranslatePopupMessage,
        CampfirePreserveTranslationPatch.TryTranslatePopupMessage,
        CookingRuntimeTranslationPatch.TryTranslatePopupMessage,
        StatusScreenPopupTranslationPatch.TryTranslatePopupMessage,
        TeleprojectorTranslationPatch.TryTranslatePopupMessage,
        CyberneticsMedassistModuleTranslationPatch.TryTranslatePopupMessage,
        LiquidLoaderTranslationPatch.TryTranslatePopupMessage,
        LiquidVolumeTranslationPatch.TryTranslatePopupMessage,
        MutatingTranslationPatch.TryTranslatePopupMessage,
        LightManipulationTranslationPatch.TryTranslatePopupMessage,
        AsleepOwnerTranslationPatch.TryTranslatePopupMessage,
        EnclosingTranslationPatch.TryTranslatePopupMessage,
        StairsDownTranslationPatch.TryTranslatePopupMessage,
        StairsUpTranslationPatch.TryTranslatePopupMessage,
        BeguilingTranslationPatch.TryTranslatePopupMessage,
        AscensionCableTranslationPatch.TryTranslatePopupMessage,
        CarapaceTranslationPatch.TryTranslatePopupMessage,
        NephalPropertiesTranslationPatch.TryTranslatePopupMessage,
        IntegratedWeaponHostsTranslationPatch.TryTranslatePopupMessage,
        FungalSporeInfectionTranslationPatch.TryTranslatePopupMessage,
    ];

    internal static string TranslateMessage(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                return string.Empty;
            }

            return source;
        }

        for (var index = 0; index < Translators.Length; index++)
        {
            if (TryTranslatePopupMessageWithFallback(
                Translators[index],
                source,
                route,
                out var translated))
            {
                return translated;
            }
        }

        if (SifrahPureOwnerPopupTranslationPatch.TryGetPureOwnerBatchPopupCandidateText(source, out var candidateText))
        {
            return candidateText;
        }

        return PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, route);
    }

    private static bool TryTranslatePerformOfferTradeWaterMessage(
        string source,
        string route,
        string family,
        out string translated)
    {
        _ = route;
        _ = family;
        return TradeUiPopupTranslationPatch.TryTranslatePerformOfferTradeWaterMessage(source, out translated);
    }

    private static bool TryTranslateHasNothingToTradeMessage(
        string source,
        string route,
        string family,
        out string translated)
    {
        _ = route;
        _ = family;
        return TradeUiPopupTranslationPatch.TryTranslateHasNothingToTradeMessage(source, out translated);
    }

    private static bool TryTranslatePopupMessageWithFallback(
        PopupMessageTranslator translator,
        string source,
        string route,
        out string translated)
    {
        try
        {
            return translator(source, route, PopupShowFamily, out translated);
        }
        catch (Exception ex)
        {
            translated = source;
            Trace.TraceError(
                "QudJP: PopupShowSemanticPipeline translator {0} failed: {1}",
                FormatTranslatorName(translator),
                ex);
            return false;
        }
    }

    private static string FormatTranslatorName(Delegate translator)
    {
        return translator.Method.DeclaringType?.FullName ?? translator.Method.Name;
    }

    private delegate bool PopupMessageTranslator(string source, string route, string family, out string translated);
}
