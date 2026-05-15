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
        MutationSelfTargetPopupTranslationPatch.TryTranslatePopupMessage,
        MutationGeneratedTextTranslationPatch.TryTranslatePopupMessage,
        PickTargetShowPickerTranslationPatch.TryTranslatePopupMessage,
        GameObjectPopupTranslationPatch.TryTranslatePopupMessage,
        OldSaveContinueMenuPopupTranslationPatch.TryTranslatePopupMessage,
        GolemQuestSelectionPopupTranslationPatch.TryTranslatePopupMessage,
        RealityStabilizedInterdictTranslationPatch.TryTranslatePopupMessage,
        MutationActionFailureTranslationPatch.TryTranslatePopupMessage,
        TelekinesisTranslationPatch.TryTranslatePopupMessage,
        DisassemblyStartTranslationPatch.TryTranslatePopupMessage,
        DanceRitualOpponentTranslationPatch.TryTranslatePopupMessage,
        IExamineEventProcessIdentifyTranslationPatch.TryTranslatePopupMessage,
        EnergyLoaderCannotTakeTranslationPatch.TryTranslatePopupMessage,
        EnergyCellSocketAccessPopupTranslationPatch.TryTranslatePopupMessage,
        EquipmentApiTwiddleObjectTranslationPatch.TryTranslatePopupMessage,
        CampfireRemainsAttemptLightTranslationPatch.TryTranslatePopupMessage,
        ClonelingVehicleTranslationPatch.TryTranslatePopupMessage,
        PointOfInterestNavigationPopupTranslationPatch.TryTranslatePopupMessage,
        RunStartRunningPopupTranslationPatch.TryTranslatePopupMessage,
        DecoyHologramEmitterActivateTranslationPatch.TryTranslatePopupMessage,
        LevelerTranslationPatch.TryTranslatePopupMessage,
        AnimateObjectTranslationPatch.TryTranslatePopupMessage,
        RandomAltarBaetylTranslationPatch.TryTranslatePopupMessage,
        VehicleSeatTranslationPatch.TryTranslatePopupMessage,
        HistoricEventRegionRevealPopupTranslationPatch.TryTranslatePopupMessage,
        JournalScreenPopupTranslationPatch.TryTranslatePopupMessage,
        ConversationScriptPopupTranslationPatch.TryTranslatePopupMessage,
        TerrainTravelTranslationPatch.TryTranslatePopupMessage,
        RequiresPowerToEquipCheckEquipPopupTranslationPatch.TryTranslatePopupMessage,
        SurvivalCampAttemptCampPopupTranslationPatch.TryTranslatePopupMessage,
        HackingSifrahResultTranslationPatch.TryTranslatePopupMessage,
        QuestLifecyclePopupTranslationPatch.TryTranslatePopupMessage,
        ConversationTakeItemPopupTranslationPatch.TryTranslatePopupMessage,
        ConversationCheckLostPopupTranslationPatch.TryTranslatePopupMessage,
        ConversationRewardPopupTranslationPatch.TryTranslatePopupMessage,
        EelSpawnTranslationPatch.TryTranslatePopupMessage,
        TeleporterPairTranslationPatch.TryTranslatePopupMessage,
        ITeleporterTranslationPatch.TryTranslatePopupMessage,
        LongBladesCoreTranslationPatch.TryTranslatePopupMessage,
        ShortBladesHobbleTranslationPatch.TryTranslatePopupMessage,
        ShortBladesShankTranslationPatch.TryTranslatePopupMessage,
        FirefightingTranslationPatch.TryTranslatePopupMessage,
        TinkerItemTranslationPatch.TryTranslatePopupMessage,
        KeyMappingUiTranslationPatch.TryTranslatePopupMessage,
        PsychicGlimmerTranslationPatch.TryTranslatePopupMessage,
        BodyTranslationPatch.TryTranslatePopupMessage,
        SifrahTokenItemPopupTranslationPatch.TryTranslatePopupMessage,
        SifrahPureOwnerPopupTranslationPatch.TryTranslatePopupMessage,
        ItemModdingSifrahTranslationPatch.TryTranslatePopupMessage,
        CudgelConkPopupTranslationPatch.TryTranslatePopupMessage,
        SunderMindTranslationPatch.TryTranslatePopupMessage,
        KeybindsScreenConflictTranslationPatch.TryTranslatePopupMessage,
        AbilityManagerPopupTranslationPatch.TryTranslatePopupMessage,
        SkillsAndPowersSelectNodePopupTranslationPatch.TryTranslatePopupMessage,
        RealityStabilizedEventTranslationPatch.TryTranslatePopupMessage,
        GeomagneticDiscTranslationPatch.TryTranslatePopupMessage,
        CampfireNostrumsTranslationPatch.TryTranslatePopupMessage,
        CampfireCookAvailabilityTranslationPatch.TryTranslatePopupMessage,
        CampfirePreserveTranslationPatch.TryTranslatePopupMessage,
        CampfireCookFromIngredientsTranslationPatch.TryTranslatePopupMessage,
        InventoryFireEventTranslationPatch.TryTranslatePopupMessage,
        PhysicsInventoryActionPopupTranslationPatch.TryTranslatePopupMessage,
        ActionManagerRunSegmentTranslationPatch.TryTranslatePopupMessage,
        WaterRitualPopupTranslationPatch.TryTranslatePopupMessage,
        PsychometryTranslationPatch.TryTranslatePopupMessage,
        SpindleNegotiationTranslationPatch.TryTranslatePopupMessage,
        CookingRuntimeTranslationPatch.TryTranslatePopupMessage,
        GameSummaryTombstonePopupTranslationPatch.TryTranslatePopupMessage,
        LocationFinderPopupTranslationPatch.TryTranslatePopupMessage,
        MapRevealPopupTranslationPatch.TryTranslatePopupMessage,
        MechanicalWingsPopupTranslationPatch.TryTranslatePopupMessage,
        SupplyableIntegratedHostPopupTranslationPatch.TryTranslatePopupMessage,
        DataDiskLearnPopupTranslationPatch.TryTranslatePopupMessage,
        HighScoresDeletePopupTranslationPatch.TryTranslatePopupMessage,
        CodeRedemptionPopupTranslationPatch.TryTranslatePopupMessage,
        GritGateTerminalKnowledgePopupTranslationPatch.TryTranslatePopupMessage,
        PickItemTakeAllPopupTranslationPatch.TryTranslatePopupMessage,
        CyberneticsWishImplantPopupTranslationPatch.TryTranslatePopupMessage,
        TinkeringBuildPopupTranslationPatch.TryTranslatePopupMessage,
        TinkeringModPopupTranslationPatch.TryTranslatePopupMessage,
        PopupPickSeveralTranslationPatch.TryTranslatePopupMessage,
        ZoneManagerGenerateZoneTranslationPatch.TryTranslatePopupMessage,
        SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage,
        BrainWriteFeelingSamplesPopupTranslationPatch.TryTranslatePopupMessage,
        BrainBrineCurseTranslationPatch.TryTranslatePopupMessage,
        StatusScreenPopupTranslationPatch.TryTranslatePopupMessage,
        TeleprojectorTranslationPatch.TryTranslatePopupMessage,
        CyberneticsMedassistModuleTranslationPatch.TryTranslatePopupMessage,
        OldSaveContinueMenuTranslationPatch.TryTranslatePopupMessage,
        DeployableInfrastructureTranslationPatch.TryTranslatePopupMessage,
        LiquidLoaderTranslationPatch.TryTranslatePopupMessage,
        LiquidVolumeTranslationPatch.TryTranslatePopupMessage,
        MutatingTranslationPatch.TryTranslatePopupMessage,
        TenfoldPathInitiatoryTranslationPatch.TryTranslatePopupMessage,
        PowerEntryRequirementPopupTranslationPatch.TryTranslatePopupMessage,
        MagneticPulseTranslationPatch.TryTranslatePopupMessage,
        PetGloamingTranslationPatch.TryTranslatePopupMessage,
        WindupTranslationPatch.TryTranslatePopupMessage,
        BasePronounProviderCustomizePopupTranslationPatch.TryTranslatePopupMessage,
        CloningStartBuddedCloneTranslationPatch.TryTranslatePopupMessage,
        TattooGunTranslationPatch.TryTranslatePopupMessage,
        EngraverTranslationPatch.TryTranslatePopupMessage,
        LightManipulationTranslationPatch.TryTranslatePopupMessage,
        AsleepOwnerTranslationPatch.TryTranslatePopupMessage,
        EnclosingTranslationPatch.TryTranslatePopupMessage,
        ModMagnetizedTranslationPatch.TryTranslatePopupMessage,
        MutationInfectionTranslationPatch.TryTranslatePopupMessage,
        StairsDownTranslationPatch.TryTranslatePopupMessage,
        StairsUpTranslationPatch.TryTranslatePopupMessage,
        AbsorbablePsychePopupTranslationPatch.TryTranslatePopupMessage,
        BeguilingTranslationPatch.TryTranslatePopupMessage,
        AscensionCableTranslationPatch.TryTranslatePopupMessage,
        CarapaceTranslationPatch.TryTranslatePopupMessage,
        NephalPropertiesTranslationPatch.TryTranslatePopupMessage,
        IntegratedWeaponHostsTranslationPatch.TryTranslatePopupMessage,
        FloatingEquipmentPopupTranslationPatch.TryTranslatePopupMessage,
        PoweredFloatingTranslationPatch.TryTranslatePopupMessage,
        FungalSporeInfectionTranslationPatch.TryTranslatePopupMessage,
        EffectMobilityBlockTranslationPatch.TryTranslatePopupMessage,
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
