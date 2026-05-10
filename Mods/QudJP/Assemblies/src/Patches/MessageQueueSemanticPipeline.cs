namespace QudJP.Patches;

internal static class MessageQueueSemanticPipeline
{
    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        return PhysicsApplyDischargeTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || AutoActTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || PhysicsObjectEnteringCellTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || CrippleApplyTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectHealTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ExperienceAwardXpTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectMoveTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectPerformThrowTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectToggleActivatedAbilityTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || CombatGetDefenderHitDiceTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || DoorAttemptOpenTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || CombatMeleeAttackTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectDieTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectRegeneraTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ClonelingVehicleTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || PetEitherOrExplodeTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ZoneWindChangeTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectSpotTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || XrlCoreLostSightTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || DeployableInfrastructureTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || PlayerDanceRitualTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || GameObjectEmitMessageTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ZoneManagerTryThawZoneTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ZoneManagerTickTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ZoneManagerSetActiveZoneMapNotesTranslationPatch.TryTranslateQueuedMessage(ref message, color)
            || ZoneManagerGenerateZoneTranslationPatch.TryTranslateQueuedMessage(ref message, color);
    }
}
