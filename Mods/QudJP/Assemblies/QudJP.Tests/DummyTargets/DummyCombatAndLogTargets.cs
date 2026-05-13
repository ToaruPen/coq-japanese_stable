using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCell
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class DummyZone
{
    public string ZoneId { get; set; } = string.Empty;
}

internal sealed class DummyFaction
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class DummyScreenBuffer
{
    public int Width { get; set; }
}

internal sealed class DummyMissilePath
{
    public int Length { get; set; }
}

internal sealed class DummyGameObject
{
    public string DisplayName { get; set; } = string.Empty;
}

internal sealed class DummyObjectEnteringCellEvent
{
}

internal sealed class DummyGameEvent
{
    public string ID { get; set; } = string.Empty;
}

internal sealed class DummyPhysicsApplyDischargeTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public int ApplyDischarge(
        DummyCell c,
        DummyCell targetCell,
        int voltage,
        int damage = 0,
        string? damageRange = null,
        object? damageRoll = null,
        DummyGameObject? target = null,
        List<DummyCell>? usedCells = null,
        DummyGameObject? owner = null,
        DummyGameObject? source = null,
        DummyGameObject? describeAsFrom = null,
        DummyGameObject? skip = null,
        List<DummyGameObject>? skipList = null,
        bool? sourceVisible = null,
        string? sourceDesc = null,
        string? sourceDirectionTowardTarget = null,
        int phase = 0,
        bool accidental = false,
        bool environmental = false,
        DummyGameObject? alternate = null,
        DummyGameObject? alternateAvoidedBecauseObject = null,
        string? alternateAvoidedBecauseReason = null,
        bool usePopups = false)
    {
        _ = c;
        _ = targetCell;
        _ = voltage;
        _ = damage;
        _ = damageRange;
        _ = damageRoll;
        _ = target;
        _ = usedCells;
        _ = owner;
        _ = source;
        _ = describeAsFrom;
        _ = skip;
        _ = skipList;
        _ = sourceVisible;
        _ = sourceDesc;
        _ = sourceDirectionTowardTarget;
        _ = phase;
        _ = accidental;
        _ = environmental;
        _ = alternate;
        _ = alternateAvoidedBecauseObject;
        _ = alternateAvoidedBecauseReason;
        _ = usePopups;

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return 1;
    }
}

internal sealed class DummyPhysicsObjectEnteringCellTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool HandleEvent(DummyObjectEnteringCellEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyCrippleApplyTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool Apply(DummyGameObject obj)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyAwardXPEvent
{
}

internal sealed class DummyExperienceTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool HandleEvent(DummyAwardXPEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyGameObjectHealTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public int Heal(int amount, bool message = false, bool floatText = false, bool randomMinimum = false)
    {
        _ = amount;
        _ = message;
        _ = floatText;
        _ = randomMinimum;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return 1;
    }
}

internal sealed class DummyGameObjectMoveTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? PopupMessageToSend { get; set; }

    public string? ColorToSend { get; set; }

    public bool Move(
        string direction,
        out DummyGameObject? blocking,
        bool forced = false,
        bool system = false,
        bool ignoreGravity = false,
        bool noStack = false,
        bool allowDashing = true,
        bool doConfirmations = true,
        DummyGameObject? dragging = null,
        DummyGameObject? actor = null,
        bool nearestAvailable = false,
        int? energyCost = null,
        string? type = null,
        int? moveSpeed = null,
        bool peaceful = false,
        bool ignoreMobility = false,
        DummyGameObject? forceSwap = null,
        DummyGameObject? ignore = null,
        int callDepth = 0)
    {
        _ = direction;
        _ = forced;
        _ = system;
        _ = ignoreGravity;
        _ = noStack;
        _ = allowDashing;
        _ = doConfirmations;
        _ = dragging;
        _ = actor;
        _ = nearestAvailable;
        _ = energyCost;
        _ = type;
        _ = moveSpeed;
        _ = peaceful;
        _ = ignoreMobility;
        _ = forceSwap;
        _ = ignore;
        _ = callDepth;

        blocking = null;
        if (PopupMessageToSend is not null)
        {
            DummyPopupShow.ShowYesNo(PopupMessageToSend);
        }

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return false;
    }
}

internal sealed class DummyDoorAttemptOpenTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool AttemptOpen(
        DummyGameObject? actor = null,
        bool usePopups = false,
        bool usePopupsForFailures = false,
        bool ignoreMobility = false,
        bool ignoreSpecialConditions = false,
        bool fromMove = false,
        bool silent = false,
        object? fromEvent = null)
    {
        _ = actor;
        _ = usePopups;
        _ = usePopupsForFailures;
        _ = ignoreMobility;
        _ = ignoreSpecialConditions;
        _ = fromMove;
        _ = silent;
        _ = fromEvent;

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return false;
    }
}

internal sealed class DummyGameObjectPerformThrowTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? PopupMessageToSend { get; set; }

    public string? ColorToSend { get; set; }

    public bool PerformThrow(
        DummyGameObject weapon,
        DummyCell targetCell,
        DummyGameObject? apparentTarget = null,
        DummyMissilePath? mPath = null,
        int phase = 0,
        int? rangeVariance = null,
        int? distanceVariance = null,
        int? energyCost = null)
    {
        _ = weapon;
        _ = targetCell;
        _ = apparentTarget;
        _ = mPath;
        _ = phase;
        _ = rangeVariance;
        _ = distanceVariance;
        _ = energyCost;
        if (PopupMessageToSend is not null)
        {
            DummyPopupShow.ShowYesNoCancel(PopupMessageToSend);
        }

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyXrlCoreRenderTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void RenderBaseToBuffer(DummyScreenBuffer buffer)
    {
        _ = buffer;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyGameObjectToggleActivatedAbilityTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool ToggleActivatedAbility(Guid id, bool silent = false, bool? setState = null)
    {
        _ = id;
        _ = silent;
        _ = setState;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyGameObjectPopupTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool UseShowFail { get; set; }

    public Task<bool> ConfirmUseImportantAsync()
    {
        _ = DummyPopupShow.ShowYesNoAsync(PopupMessageToSend).GetAwaiter().GetResult();
        return Task.FromResult(true);
    }

    public bool ConfirmUseImportant()
    {
        _ = DummyPopupShow.ShowYesNo(PopupMessageToSend);
        return true;
    }

    public void HandleRename()
    {
        if (UseShowFail)
        {
            DummyPopupShow.ShowFail(PopupMessageToSend);
            return;
        }

        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ChangeCompanionAbilityUse()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public bool CheckCompanionDirection()
    {
        DummyPopupShow.ShowFail(PopupMessageToSend);
        return true;
    }
}

internal sealed class DummyRealityStabilizedInterdictTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public void ShowGenericInterdictMessage()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ShowDistantInterdictMessage()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ShowDualInterdictMessage()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }
}

internal sealed class DummyHackingSifrahResultTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public void HackingResultSuccess()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void HackingResultExceptionalSuccess()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void HackingResultPartialSuccess()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void HackingResultFailure()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void HackingResultCriticalFailure()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }
}

internal sealed class DummyQuestLifecyclePopupTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public void ShowStartPopup()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ShowFailPopup()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ShowFailStepPopup()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ShowFinishPopup()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }
}

internal sealed class DummyFlightTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool StartFlying()
    {
        return EmitFlightMessage(nameof(StartFlying));
    }

    public bool StopFlying()
    {
        return EmitFlightMessage(nameof(StopFlying));
    }

    public void Land()
    {
        _ = nameof(Land);
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public bool FailFlying()
    {
        return EmitFlightMessage(nameof(FailFlying));
    }

    private bool EmitFlightMessage(string route)
    {
        _ = route;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyBodyTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public void CheckUnsupportedPartLoss()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void CheckPartRecovery()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void Dismember()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public bool RegenerateLimb()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyItemModdingSifrahTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public void ResultFailure()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ResultPartialSuccess()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ResultSuccess()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void ResultCriticalSuccess()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }
}

internal sealed class DummySunderMindTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public void CancelSunder()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void BeginSunder(bool usePopup)
    {
        if (usePopup)
        {
            DummyPopupShow.Show(PopupMessageToSend);
            return;
        }

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void PenetrationFailure()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyKeybindsScreenConflictTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public Task<bool> ConfirmConflictBind()
    {
        return ShowYesNoAsync(nameof(ConfirmConflictBind));
    }

    public Task<bool> ConfirmDynamicConflictBind()
    {
        return ShowYesNoAsync(nameof(ConfirmDynamicConflictBind));
    }

    public Task RequiredConflictBind()
    {
        _ = nameof(RequiredConflictBind);
        return DummyPopupShow.ShowAsync(PopupMessageToSend);
    }

    private Task<bool> ShowYesNoAsync(string route)
    {
        _ = route;
        _ = DummyPopupShow.ShowYesNoAsync(PopupMessageToSend).GetAwaiter().GetResult();
        return Task.FromResult(true);
    }
}

internal sealed class DummyOldSaveContinueMenuTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public Task<object?> MainMenuContinueMenu()
    {
        return ShowOldSavePopup(nameof(MainMenuContinueMenu));
    }

    public Task<object?> SaveManagementContinueMenu()
    {
        return ShowOldSavePopup(nameof(SaveManagementContinueMenu));
    }

    private async Task<object?> ShowOldSavePopup(string route)
    {
        _ = route;
        await DummyPopupShow.ShowAsync(PopupMessageToSend).ConfigureAwait(false);
        return null;
    }
}

internal sealed class DummyRealityStabilizedEventTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool TryContest()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public void ShortCircuitDevice(bool usePopup)
    {
        if (usePopup)
        {
            DummyPopupShow.Show(PopupMessageToSend);
            return;
        }

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyCyberneticRejectionSyndromeTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool Apply(DummyGameObject? gameObject = null)
    {
        _ = gameObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public void Remove(DummyGameObject? gameObject = null)
    {
        _ = gameObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void Reduce(int by = 1)
    {
        _ = by;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyGeomagneticDiscTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public void SignalFailure(DummyGameObject? actor = null)
    {
        _ = actor;
        _ = nameof(SignalFailure);
        DummyPopupShow.ShowFail(PopupMessageToSend);
    }

    public void SignalLowPower(DummyGameObject? actor = null)
    {
        _ = actor;
        _ = nameof(SignalLowPower);
        DummyPopupShow.ShowFail(PopupMessageToSend);
    }

    public bool ExamineFailure(DummyExamineEvent? examineEvent = null, int chance = 100)
    {
        _ = examineEvent;
        _ = chance;
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }
}

internal sealed class DummyExamineEvent
{
}

internal sealed class DummyCampfireCookTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool Cook()
    {
        DummyPopupShow.Show(PopupMessageToSend);
        return false;
    }
}

internal sealed class DummyTeleprojectorTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool HandleEvent(DummyBootSequenceDoneEvent? bootSequenceDoneEvent = null)
    {
        _ = bootSequenceDoneEvent;
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }

    public bool ActivateTeleprojector()
    {
        DummyPopupShow.ShowFail(PopupMessageToSend);
        return false;
    }

    public bool RoboDom(DummyMentalAttackEvent? mentalAttackEvent = null)
    {
        _ = mentalAttackEvent;
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }
}

internal sealed class DummyBootSequenceDoneEvent
{
}

internal sealed class DummyMentalAttackEvent
{
}

internal sealed class DummyTombAnchorSystemTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void OnEndTurn()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void Recall(DummyZone? zone = null)
    {
        _ = zone;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void AnchorCall()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyCyberneticsMedassistModuleTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool HandleEvent(DummyInventoryActionEvent? inventoryActionEvent = null)
    {
        _ = inventoryActionEvent;
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }

    public void AttemptMedicalAssistance(DummyDamage? damage = null)
    {
        _ = damage;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyDamage
{
    public string Id { get; set; } = nameof(DummyDamage);
}

internal sealed class DummyLiquidLoaderTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool HandleEvent(DummyCommandReloadEvent? commandReloadEvent = null)
    {
        _ = commandReloadEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool FireEvent(DummyEvent? eventObject = null)
    {
        _ = eventObject;
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }
}

internal sealed class DummyCommandReloadEvent
{
    public string Id { get; set; } = nameof(DummyCommandReloadEvent);
}

internal sealed class DummyEvent
{
    public string Id { get; set; } = nameof(DummyEvent);
}

internal sealed class DummyTrollKingTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void CheckSpawn(int turns = 1)
    {
        _ = turns;
        _ = nameof(CheckSpawn);
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void StopBudding(int turns = 1)
    {
        _ = turns;
        _ = nameof(StopBudding);
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyMutatingTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool Apply(DummyGameObject? gameObject = null)
    {
        _ = gameObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool HandleEvent(DummyEndTurnEvent? endTurnEvent = null, bool usePopup = false)
    {
        _ = endTurnEvent;
        if (usePopup)
        {
            DummyPopupShow.Show(PopupMessageToSend);
            return true;
        }

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyEndTurnEvent
{
    public string Id { get; set; } = nameof(DummyEndTurnEvent);
}

internal sealed class DummyQuillsTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool HandleEvent(DummyTookDamageEvent? tookDamageEvent = null)
    {
        _ = tookDamageEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool FireEvent(DummyEvent? eventObject = null)
    {
        _ = eventObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyTookDamageEvent
{
    public string Id { get; set; } = nameof(DummyTookDamageEvent);
}

internal sealed class DummyLightManipulationTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool HandleEvent(DummyCommandEvent? commandEvent = null, bool usePopup = false)
    {
        _ = commandEvent;
        if (usePopup)
        {
            DummyPopupShow.ShowFail(PopupMessageToSend);
            return true;
        }

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool Lase(DummyCell? cell = null, int pathLength = 0)
    {
        _ = cell;
        _ = pathLength;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyCommandEvent
{
    public string Id { get; set; } = nameof(DummyCommandEvent);
}

internal sealed class DummyLatchesOnTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool HandleEvent(DummyUnequippedEvent? unequippedEvent = null)
    {
        _ = unequippedEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool FireEvent(DummyEvent? eventObject = null)
    {
        _ = eventObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyUnequippedEvent
{
    public string Id { get; set; } = nameof(DummyUnequippedEvent);
}

internal sealed class DummyAsleepOwnerTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool Apply(DummyGameObject? obj = null)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool HandleEvent(DummyBeginTakeActionEvent? beginTakeActionEvent = null)
    {
        _ = beginTakeActionEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool HandleEvent(DummyInventoryActionEvent? inventoryActionEvent = null, bool usePopup = false)
    {
        _ = inventoryActionEvent;
        if (usePopup)
        {
            DummyPopupShow.Show(PopupMessageToSend);
            return true;
        }

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyBeginTakeActionEvent
{
    public string Id { get; set; } = nameof(DummyBeginTakeActionEvent);
}

internal sealed class DummyEffectAppliedEvent
{
}

internal sealed class DummyBuddingTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool Apply(DummyGameObject? obj = null)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public void Remove(DummyGameObject? obj = null)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyBeguilingTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public string PopupMessageToSend { get; set; } = string.Empty;

    public static bool Cast(
        DummyGameObject? who = null,
        DummyBeguilingTarget? mutation = null,
        DummyEvent? eventObject = null,
        int genericLevel = 1)
    {
        _ = who;
        _ = mutation;
        _ = eventObject;
        _ = genericLevel;
        DummyMessageQueue.AddPlayerMessage(StaticMessageToSend, StaticColorToSend, Capitalize: false);
        if (StaticPopupMessageToSend is not null)
        {
            _ = DummyPopupShow.ShowYesNo(StaticPopupMessageToSend);
        }

        return true;
    }

    public bool Beguile(DummyMentalAttackEvent? mentalAttackEvent = null)
    {
        _ = mentalAttackEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public static string StaticMessageToSend { get; set; } = string.Empty;

    public static string? StaticColorToSend { get; set; }

    public static string? StaticPopupMessageToSend { get; set; }
}

internal sealed class DummyAscensionCableTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool TryAscend(DummyGameObject? actor = null, bool fromDialog = false)
    {
        return ShowPopup(actor, fromDialog, nameof(TryAscend));
    }

    public bool TryDescend(DummyGameObject? actor = null, bool fromDialog = false)
    {
        return ShowPopup(actor, fromDialog, nameof(TryDescend));
    }

    private bool ShowPopup(DummyGameObject? actor, bool fromDialog, string route)
    {
        _ = actor;
        _ = fromDialog;
        _ = route;
        DummyPopupShow.Show(PopupMessageToSend);
        return false;
    }
}

internal sealed class DummyCarapaceTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public void Tighten(bool message = false)
    {
        _ = message;
        DummyPopupShow.Show(PopupMessageToSend);
    }
}

internal sealed class DummySvardymSystemTarget
{
    public string FirstMessageToSend { get; set; } = string.Empty;

    public string SecondMessageToSend { get; set; } = string.Empty;

    public void BeginStorm()
    {
        DummyMessageQueue.AddPlayerMessage(FirstMessageToSend);
        if (!string.IsNullOrEmpty(SecondMessageToSend))
        {
            DummyMessageQueue.AddPlayerMessage(SecondMessageToSend);
        }
    }

    public void Tick()
    {
        var message = FirstMessageToSend;
        DummyMessageQueue.AddPlayerMessage(message);
        if (!string.IsNullOrEmpty(SecondMessageToSend))
        {
            DummyMessageQueue.AddPlayerMessage(SecondMessageToSend);
        }
    }
}

internal sealed class DummyPhasedTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public bool HandleEvent(DummyEffectAppliedEvent? effectAppliedEvent = null)
    {
        _ = effectAppliedEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend);
        return true;
    }

    public bool HandleEvent(DummyBeginTakeActionEvent? beginTakeActionEvent = null)
    {
        _ = beginTakeActionEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend);
        return true;
    }

    public void Remove(DummyGameObject? obj = null)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend);
    }
}

internal sealed class DummyPersuasionRebukeRobotTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public bool Rebuke(DummyMentalAttackEvent? mentalAttackEvent = null)
    {
        _ = mentalAttackEvent;
        DummyMessageQueue.AddPlayerMessage(MessageToSend);
        return false;
    }
}

internal sealed class DummyNephalPropertiesTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool TryPacify()
    {
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }
}

internal sealed class DummyTonicTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public bool FireEvent(DummyEvent? eventObject = null)
    {
        _ = eventObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend);
        return true;
    }
}

internal sealed class DummySimpleOwnerQueueTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool FireEvent(DummyEvent? eventObject = null)
    {
        _ = eventObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool HandleEvent(DummyBeginTakeActionEvent? eventObject = null)
    {
        _ = eventObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool HandleEvent(DummyEndTurnEvent? eventObject = null)
    {
        _ = eventObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool HandleEvent(DummyBeforeApplyDamageEvent? eventObject = null)
    {
        _ = eventObject;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool Apply(DummyGameObject? obj = null)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public static bool ApplyFear(DummyMentalAttackEvent? mentalAttackEvent = null)
    {
        _ = mentalAttackEvent;
        DummyMessageQueue.AddPlayerMessage(StaticMessageToSend, StaticColorToSend, Capitalize: false);
        return false;
    }

    public void Remove(DummyGameObject? obj = null)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public static bool CheckpointOn()
    {
        DummyMessageQueue.AddPlayerMessage(StaticMessageToSend, StaticColorToSend, Capitalize: false);
        return true;
    }

    public void SetHolyZone(DummyZone? zone = null, DummyFaction? faction = null)
    {
        _ = zone;
        _ = faction;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void Quake()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public void TeleportToClamWorld(DummyGameObject? obj = null)
    {
        SendOwnerQueueMessage(nameof(TeleportToClamWorld), obj);
    }

    public void TeleportFromClamWorld(DummyGameObject? obj = null)
    {
        SendOwnerQueueMessage(nameof(TeleportFromClamWorld), obj);
    }

    public void TeleportJoppaWorld(DummyGameObject? obj = null)
    {
        SendOwnerQueueMessage(nameof(TeleportJoppaWorld), obj);
    }

    public void Expired()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    private void SendOwnerQueueMessage(string ownerMethodName, DummyGameObject? obj)
    {
        _ = ownerMethodName;
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public static string StaticMessageToSend { get; set; } = string.Empty;

    public static string? StaticColorToSend { get; set; }
}

internal sealed class DummyBeforeApplyDamageEvent;

internal sealed class DummyQuest
{
    public string ID { get; set; } = string.Empty;
}

internal sealed class DummyXrlGameTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public bool FinishQuestStep(
        DummyQuest? quest = null,
        string? questStepList = null,
        int xp = -1,
        bool canFinishQuest = true,
        string? zoneId = null)
    {
        _ = quest;
        _ = questStepList;
        _ = xp;
        _ = canFinishQuest;
        _ = zoneId;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, "R");
        return false;
    }
}

internal sealed class DummyIntegratedWeaponHostsTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public DummyGameObject GenerateTurret(DummyGameObject weapon, DummyGameObject? owner = null, bool overrideSupply = false)
    {
        _ = weapon;
        _ = owner;
        _ = overrideSupply;
        DummyPopupShow.Show(PopupMessageToSend);
        return new DummyGameObject();
    }

    public bool HandleTurretWish(Match match)
    {
        _ = match;
        DummyPopupShow.ShowFail(PopupMessageToSend);
        return true;
    }
}

internal sealed class DummyBoostStatisticTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool Apply(DummyGameObject obj)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public void Remove(DummyGameObject obj)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyEmboldenedTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool Apply(DummyGameObject obj)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public void Remove(DummyGameObject obj)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyFungalSporeInfectionTarget
{
    public static string PopupMessageToSend { get; set; } = string.Empty;

    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public static bool ApplyFungalInfection(DummyGameObject obj, string infectionBlueprint, DummyBodyPart? selectedPart = null)
    {
        _ = obj;
        _ = infectionBlueprint;
        _ = selectedPart;
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }

    public bool FireEvent(DummyGameEvent e)
    {
        _ = e;
        return EmitQueuedMessage("fungal");
    }

    public bool PaxFireEvent(DummyGameEvent e)
    {
        _ = e;
        return EmitQueuedMessage("pax");
    }

    public bool PuffFireEvent(DummyGameEvent e)
    {
        _ = e;
        return EmitQueuedMessage("puff");
    }

    private bool EmitQueuedMessage(string producer)
    {
        _ = producer;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyUseEnergyEvent
{
    public bool Passive { get; set; }

    public string? Type { get; set; }
}

internal sealed class DummyHealingTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool HandleEvent(DummyUseEnergyEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool FireEvent(DummyGameEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyStressedTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool Apply(DummyGameObject obj)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public void Remove(DummyGameObject obj)
    {
        _ = obj;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummySecretVisibilityChangedEvent
{
    public string Id { get; set; } = nameof(DummySecretVisibilityChangedEvent);
}

internal sealed class DummyEnteredCellEvent
{
    public string Id { get; set; } = nameof(DummyEnteredCellEvent);
}

internal sealed class DummyAmnesiaTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool HandleEvent(DummySecretVisibilityChangedEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool HandleEvent(DummyEnteredCellEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyGameObjectDieTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool Die(
        DummyGameObject? killer = null,
        string? killerText = null,
        string? reason = null,
        string? thirdPersonReason = null,
        bool accidental = false,
        DummyGameObject? weapon = null,
        DummyGameObject? projectile = null,
        bool force = false,
        bool alwaysUsePopups = false,
        string? message = null,
        string? deathVerb = null,
        string? deathCategory = null)
    {
        _ = killer;
        _ = killerText;
        _ = reason;
        _ = thirdPersonReason;
        _ = accidental;
        _ = weapon;
        _ = projectile;
        _ = force;
        _ = alwaysUsePopups;
        _ = message;
        _ = deathVerb;
        _ = deathCategory;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyGameObjectFireEventTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool FireEvent(DummyGameEvent E)
    {
        _ = E;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyGameObjectSpotTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool ArePerceptibleHostilesNearby(
        bool logSpot = false,
        bool popSpot = false,
        string? description = null,
        object? action = null,
        string? setting = null,
        int ignoreEasierThan = int.MinValue,
        int ignoreFartherThan = 40,
        bool ignorePlayerTarget = false,
        bool checkingPrior = false)
    {
        _ = logSpot;
        _ = popSpot;
        _ = description;
        _ = action;
        _ = setting;
        _ = ignoreEasierThan;
        _ = ignoreFartherThan;
        _ = ignorePlayerTarget;
        _ = checkingPrior;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyGameObjectEmitMessageTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void EmitMessage(string message, DummyGameObject? obj = null, string? color = null, bool usePopup = false)
    {
        _ = message;
        _ = obj;
        _ = color;
        _ = usePopup;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyDeployableInfrastructureTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool UseShowFail { get; set; }

    public string? ColorToSend { get; set; }

    public void DeployOne(DummyGameObject actor, DummyCell cell, bool active = true, bool message = false)
    {
        _ = actor;
        _ = cell;
        _ = active;
        _ = message;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }

    public bool AttemptDeploy(DummyGameObject actor)
    {
        _ = actor;
        if (UseShowFail)
        {
            DummyPopupShow.ShowFail(PopupMessageToSend);
        }
        else
        {
            DummyPopupShow.Show(PopupMessageToSend);
        }

        return true;
    }
}

internal static class DummyMessagingEmitMessageTarget
{
    public static string MessageToSend { get; set; } = string.Empty;

    public static string? ColorToSend { get; set; }

    public static void EmitMessage(
        DummyGameObject who,
        string message,
        char ifPlayer,
        bool inScreenBuffer,
        bool log,
        bool single,
        DummyGameObject? fromDialog = null,
        DummyGameObject? fromCurrentCell = null)
    {
        _ = who;
        _ = message;
        _ = ifPlayer;
        _ = inScreenBuffer;
        _ = log;
        _ = single;
        _ = fromDialog;
        _ = fromCurrentCell;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyZoneManagerTryThawZoneTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool TryThawZone(string zoneId, out DummyZone? zone)
    {
        _ = zoneId;
        zone = null;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return false;
    }
}

internal sealed class DummyZoneManagerTickTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void Tick(bool allowFreeze)
    {
        _ = allowFreeze;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyZoneManagerMapNotesTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void SetActiveZone(DummyZone zone)
    {
        _ = zone;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyZoneManagerGenerateZoneTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void GenerateZone(string zoneId)
    {
        _ = zoneId;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}
