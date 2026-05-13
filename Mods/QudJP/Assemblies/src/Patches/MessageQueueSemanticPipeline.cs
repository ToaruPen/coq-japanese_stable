using System;
using System.Diagnostics;

namespace QudJP.Patches;

internal static class MessageQueueSemanticPipeline
{
    private static readonly QueuedMessageTranslator[] Translators =
    [
        PhysicsApplyDischargeTranslationPatch.TryTranslateQueuedMessage,
        AutoActTranslationPatch.TryTranslateQueuedMessage,
        PhysicsObjectEnteringCellTranslationPatch.TryTranslateQueuedMessage,
        CrippleApplyTranslationPatch.TryTranslateQueuedMessage,
        GameObjectHealTranslationPatch.TryTranslateQueuedMessage,
        ExperienceAwardXpTranslationPatch.TryTranslateQueuedMessage,
        GameObjectMoveTranslationPatch.TryTranslateQueuedMessage,
        GameObjectPerformThrowTranslationPatch.TryTranslateQueuedMessage,
        GameObjectToggleActivatedAbilityTranslationPatch.TryTranslateQueuedMessage,
        FlightTranslationPatch.TryTranslateQueuedMessage,
        BodyTranslationPatch.TryTranslateQueuedMessage,
        SunderMindTranslationPatch.TryTranslateQueuedMessage,
        RealityStabilizedEventTranslationPatch.TryTranslateQueuedMessage,
        CyberneticRejectionSyndromeTranslationPatch.TryTranslateQueuedMessage,
        TombAnchorSystemTranslationPatch.TryTranslateQueuedMessage,
        CyberneticsMedassistModuleTranslationPatch.TryTranslateQueuedMessage,
        LiquidLoaderTranslationPatch.TryTranslateQueuedMessage,
        LiquidVolumeTranslationPatch.TryTranslateQueuedMessage,
        FireSuppressionDischargeTranslationPatch.TryTranslateQueuedMessage,
        TrollKingTranslationPatch.TryTranslateQueuedMessage,
        MutatingTranslationPatch.TryTranslateQueuedMessage,
        QuillsTranslationPatch.TryTranslateQueuedMessage,
        LightManipulationTranslationPatch.TryTranslateQueuedMessage,
        CombatSkillMessageTranslationPatch.TryTranslateQueuedMessage,
        StasisTranslationPatch.TryTranslateQueuedMessage,
        EffectGeneratedMessageTranslationPatch.TryTranslateQueuedMessage,
        BlazeTonicRemoveTranslationPatch.TryTranslateQueuedMessage,
        LatchesOnTranslationPatch.TryTranslateQueuedMessage,
        AsleepOwnerTranslationPatch.TryTranslateQueuedMessage,
        EnclosingTranslationPatch.TryTranslateQueuedMessage,
        BuddingTranslationPatch.TryTranslateQueuedMessage,
        BeguilingTranslationPatch.TryTranslateQueuedMessage,
        SvardymSystemTranslationPatch.TryTranslateQueuedMessage,
        PhasedTranslationPatch.TryTranslateQueuedMessage,
        PersuasionRebukeRobotTranslationPatch.TryTranslateQueuedMessage,
        TonicTranslationPatch.TryTranslateQueuedMessage,
        XrlGameTranslationPatch.TryTranslateQueuedMessage,
        BoostStatisticTranslationPatch.TryTranslateQueuedMessage,
        EmboldenedTranslationPatch.TryTranslateQueuedMessage,
        FungalSporeInfectionTranslationPatch.TryTranslateQueuedMessage,
        HealingTranslationPatch.TryTranslateQueuedMessage,
        StressedTranslationPatch.TryTranslateQueuedMessage,
        MonochromeOnsetTranslationPatch.TryTranslateQueuedMessage,
        IronshankOnsetTranslationPatch.TryTranslateQueuedMessage,
        AdrenalControlTranslationPatch.TryTranslateQueuedMessage,
        AmnesiaTranslationPatch.TryTranslateQueuedMessage,
        BlinkingTicTranslationPatch.TryTranslateQueuedMessage,
        BrittleBonesTranslationPatch.TryTranslateQueuedMessage,
        ElectromagneticImpulseTranslationPatch.TryTranslateQueuedMessage,
        FearAuraTranslationPatch.TryTranslateQueuedMessage,
        CookingRuntimeTranslationPatch.TryTranslateQueuedMessage,
        MeditatingTranslationPatch.TryTranslateQueuedMessage,
        RegenerationTranslationPatch.TryTranslateQueuedMessage,
        EffectStaticMessageTranslationPatch.TryTranslateQueuedMessage,
        SystemStaticMessageTranslationPatch.TryTranslateQueuedMessage,
        CombatTextSurfaceTranslationPatch.TryTranslateQueuedMessage,
        GritGateTerminalScreenMessageTranslationPatch.TryTranslateQueuedMessage,
        DoorAttemptOpenTranslationPatch.TryTranslateQueuedMessage,
        GameObjectDieTranslationPatch.TryTranslateQueuedMessage,
        GameObjectRegeneraTranslationPatch.TryTranslateQueuedMessage,
        ClonelingVehicleTranslationPatch.TryTranslateQueuedMessage,
        PetEitherOrExplodeTranslationPatch.TryTranslateQueuedMessage,
        ZoneWindChangeTranslationPatch.TryTranslateQueuedMessage,
        GameObjectSpotTranslationPatch.TryTranslateQueuedMessage,
        XrlCoreLostSightTranslationPatch.TryTranslateQueuedMessage,
        DeployableInfrastructureTranslationPatch.TryTranslateQueuedMessage,
        PlayerDanceRitualTranslationPatch.TryTranslateQueuedMessage,
        GameObjectEmitMessageTranslationPatch.TryTranslateQueuedMessage,
        ZoneManagerTryThawZoneTranslationPatch.TryTranslateQueuedMessage,
        ZoneManagerTickTranslationPatch.TryTranslateQueuedMessage,
        ZoneManagerSetActiveZoneMapNotesTranslationPatch.TryTranslateQueuedMessage,
        ZoneManagerGenerateZoneTranslationPatch.TryTranslateQueuedMessage,
    ];

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        for (var index = 0; index < Translators.Length; index++)
        {
            if (TryTranslateQueuedMessageWithFallback(Translators[index], ref message, color))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateQueuedMessageWithFallback(
        QueuedMessageTranslator translator,
        ref string message,
        string? color)
    {
        var source = message;

        try
        {
            return translator(ref message, color);
        }
        catch (Exception ex)
        {
            message = source;
            Trace.TraceError(
                "QudJP: MessageQueueSemanticPipeline translator {0} failed: {1}",
                FormatTranslatorName(translator),
                ex);
            return false;
        }
    }

    private static string FormatTranslatorName(Delegate translator)
    {
        return translator.Method.DeclaringType?.FullName ?? translator.Method.Name;
    }

    private delegate bool QueuedMessageTranslator(ref string message, string? color);
}
