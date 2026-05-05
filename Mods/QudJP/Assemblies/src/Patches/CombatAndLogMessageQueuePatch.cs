using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CombatAndLogMessageQueuePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.Messages.MessageQueue:AddPlayerMessage",
            new[] { typeof(string), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve MessageQueue.AddPlayerMessage(string, string, bool) for CombatAndLogMessageQueuePatch.");
        }

        return method;
    }

    [HarmonyPriority(Priority.First - 1)]
    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Capitalize;

            _ = PhysicsApplyDischargeTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || AutoActTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || PhysicsObjectEnteringCellTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectHealTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectMoveTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectPerformThrowTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectToggleActivatedAbilityTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || CombatGetDefenderHitDiceTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || DoorAttemptOpenTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || CombatMeleeAttackTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectDieTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectRegeneraTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || ClonelingVehicleTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectSpotTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || XrlCoreLostSightTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || DeployableInfrastructureTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || GameObjectEmitMessageTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || ZoneManagerTryThawZoneTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || ZoneManagerTickTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || ZoneManagerSetActiveZoneMapNotesTranslationPatch.TryTranslateQueuedMessage(ref Message, Color)
                || ZoneManagerGenerateZoneTranslationPatch.TryTranslateQueuedMessage(ref Message, Color);

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CombatAndLogMessageQueuePatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}
