using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyInventoryActionEvent
{
    public string Id { get; set; } = nameof(DummyInventoryActionEvent);
}

internal sealed class DummyEnclosedEffect
{
    public string Id { get; set; } = nameof(DummyEnclosedEffect);
}

internal sealed class DummyGetShortDescriptionEvent
{
    public StringBuilder Postfix { get; } = new();
}

internal sealed class DummyLiquidVolumeProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string QueuedMessageToSend { get; set; } = string.Empty;

    public string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        ShowConfiguredPopup();
        AddConfiguredQueuedMessage();
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool Pour(
        ref bool requestInterfaceExit,
        DummyGameObject? actor = null,
        DummyCell? targetCell = null,
        bool forced = false,
        bool douse = false,
        int pourAmount = -1,
        bool ownershipHandled = false)
    {
        _ = actor;
        _ = targetCell;
        _ = forced;
        _ = douse;
        _ = pourAmount;
        _ = ownershipHandled;
        requestInterfaceExit = false;
        ShowConfiguredPopup();
        AddConfiguredQueuedMessage();
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PerformFill(DummyGameObject actor, ref bool requestInterfaceExit, bool ownershipHandled = false)
    {
        _ = actor;
        _ = ownershipHandled;
        requestInterfaceExit = false;
        ShowConfiguredPopup();
        return true;
    }

    private void ShowConfiguredPopup()
    {
        if (string.IsNullOrEmpty(PopupMessageToShow))
        {
            return;
        }

        if (string.Equals(PopupMethod, nameof(DummyPopupTarget.ShowBlock), StringComparison.Ordinal))
        {
            DummyPopupTarget.ShowBlock(PopupMessageToShow);
            return;
        }

        if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowYesNoCancel), StringComparison.Ordinal))
        {
            DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
            return;
        }

        DummyPopupShow.Show(PopupMessageToShow);
    }

    private void AddConfiguredQueuedMessage()
    {
        if (!string.IsNullOrEmpty(QueuedMessageToSend))
        {
            DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        }
    }
}

internal sealed class DummyGameObjectStatPopupProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GainSP(int amount, bool message = true)
    {
        ShowStatPopup(nameof(GainSP), amount, message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GainEgo(int amount, bool message = true)
    {
        ShowStatPopup(nameof(GainEgo), amount, message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LoseEgo(int amount, bool message = true)
    {
        ShowStatPopup(nameof(LoseEgo), amount, message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GainIntelligence(int amount, bool message = true)
    {
        ShowStatPopup(nameof(GainIntelligence), amount, message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void GainWillpower(int amount, bool message = true)
    {
        ShowStatPopup(nameof(GainWillpower), amount, message);
    }

    private void ShowStatPopup(string methodName, int amount, bool message)
    {
        _ = methodName;
        _ = amount;
        _ = message;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyDesalinationPelletProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}

internal sealed class DummyClonelingProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string QueuedMessageToSend { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        DummyPopupShow.ShowFail(PopupMessageToShow);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool AttemptCloning()
    {
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        return true;
    }
}

internal sealed class DummyVehicleRepairProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}

internal sealed class DummyRepairProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string HandleEventPopupMethod { get; set; } = "ShowYesNoCancel";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        if (HandleEventPopupMethod == "ShowFail")
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
        else if (HandleEventPopupMethod == "Show")
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
        else
        {
            DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RepairResultSuccess(DummyGameObject who, DummyGameObject obj)
    {
        _ = who;
        _ = obj;
        DummyPopupTarget.ShowBlock(PopupMessageToShow);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RepairResultExceptionalSuccess(DummyGameObject who, DummyGameObject obj)
    {
        ShowRepairPopup(nameof(RepairResultExceptionalSuccess), who, obj);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RepairResultPartialSuccess(DummyGameObject who, DummyGameObject obj)
    {
        ShowRepairPopup(nameof(RepairResultPartialSuccess), who, obj);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RepairResultFailure(DummyGameObject who, DummyGameObject obj)
    {
        ShowRepairPopup(nameof(RepairResultFailure), who, obj);
    }

    private void ShowRepairPopup(string methodName, DummyGameObject who, DummyGameObject obj)
    {
        _ = methodName;
        _ = who;
        _ = obj;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyVehicleRecallProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}

internal sealed class DummyEnclosingProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string QueuedMessageToSend { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool EnterEnclosure(DummyGameObject who, DummyGameEvent? e = null)
    {
        _ = who;
        _ = e;
        DummyPopupShow.Show(PopupMessageToShow);
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ExitEnclosure(DummyGameObject who, DummyGameEvent? e = null, DummyEnclosedEffect? enc = null)
    {
        _ = who;
        _ = e;
        _ = enc;
        DummyPopupShow.Show(PopupMessageToShow);
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool EnclosureExitImpeded(DummyGameObject who, bool showMessage = false, DummyEnclosedEffect? effect = null)
    {
        _ = who;
        _ = showMessage;
        _ = effect;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}

internal sealed class DummyStairsDownProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}

internal sealed class DummyStairsUpProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}

internal sealed class DummyPoweredFloatingProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CheckFloating()
    {
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyModMagnetizedProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CheckFloating()
    {
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyGivesRepProducerTarget
{
    public string PostfixTextToAppend { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyGetShortDescriptionEvent E)
    {
        E.Postfix.Append(PostfixTextToAppend);
        return true;
    }
}

internal sealed class DummyPetEitherOrProducerTarget
{
    public string QueuedMessageToSend { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void explode()
    {
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void trigger()
    {
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
    }
}

internal sealed class DummyHologramInvulnerabilityProducerTarget
{
    public string QueuedMessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent()
    {
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, ColorToSend, Capitalize: false);
        return false;
    }
}

internal sealed class DummyDecarbonizerProducerTarget
{
    public string QueuedMessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ShutDownTargeting()
    {
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyModPaddedProducerTarget
{
    public string QueuedMessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool FireEvent()
    {
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyMotePropertiesProducerTarget
{
    public string QueuedMessageToSend { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent()
    {
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
        return true;
    }
}

internal sealed class DummyZoneWindChangeProducerTarget
{
    public string QueuedMessageToSend { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void WindChange(long turnNumber)
    {
        _ = turnNumber;
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
    }
}

internal sealed class DummyPlayerDanceRitualProducerTarget
{
    public string QueuedMessageToSend { get; set; } = string.Empty;

    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ExecuteMove(string actor, string direction)
    {
        _ = actor;
        _ = direction;
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void PassStep(string reason = "")
    {
        _ = nameof(PassStep);
        _ = reason;
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void FailStep(string reason = "")
    {
        _ = nameof(FailStep);
        _ = reason;
        DummyMessageQueue.AddPlayerMessage(QueuedMessageToSend, null, Capitalize: false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void FailDance(string reason = "")
    {
        _ = nameof(FailDance);
        _ = reason;
        DummyPopupShow.Show(PopupMessageToShow);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SuccessDance(string reason = "")
    {
        _ = nameof(SuccessDance);
        _ = reason;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyBeguilingSifrahProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultCriticalFailure(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultCriticalFailure), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultFailure(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultFailure), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultPartialSuccess(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultPartialSuccess), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultSuccess(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultSuccess), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultExceptionalSuccess(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultExceptionalSuccess), contextObject);
    }

    private void ShowResultPopup(string methodName, DummyGameObject contextObject)
    {
        _ = methodName;
        _ = contextObject;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummySifrahPureOwnerPopupProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

    public bool InvokeMakeMoveBeforeHagglingPopup { get; set; }

    public string NestedPopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BaetylOfferingSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(BaetylOfferingSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void FormalWaterRitualSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(FormalWaterRitualSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HagglingSifrah(DummyGameObject contextObject)
    {
        if (InvokeMakeMoveBeforeHagglingPopup)
        {
            var outerMessage = PopupMessageToShow;
            PopupMessageToShow = NestedPopupMessageToShow;
            SifrahGameMakeMoveForSlot(contextObject);
            PopupMessageToShow = outerMessage;
        }

        ShowPopup(nameof(HagglingSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DisarmingSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(DisarmingSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ExamineSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(ExamineSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HackingSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(HackingSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ProselytizationSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(ProselytizationSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RebukingSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(RebukingSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ItemModdingSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(ItemModdingSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ItemNamingSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(ItemNamingSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ReverseEngineeringSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(ReverseEngineeringSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RepairSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(RepairSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void PsychicCombatSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(PsychicCombatSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RealityDistortionSifrah(DummyGameObject contextObject)
    {
        ShowPopup(nameof(RealityDistortionSifrah), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ReverseEngineeringCheckEarlyExit(DummyGameObject contextObject)
    {
        ShowPopup(nameof(ReverseEngineeringCheckEarlyExit), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RitualAttributeSacrificeCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(RitualAttributeSacrificeCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RitualInvokeHigherBeingCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(RitualInvokeHigherBeingCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SocialSecretCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(SocialSecretCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void TinkeringBitCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(TinkeringBitCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void TinkeringChargeCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(TinkeringChargeCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void TinkeringComputePowerCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(TinkeringComputePowerCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void TinkeringLiquidCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(TinkeringLiquidCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SifrahGameMakeMoveForSlot(DummyGameObject contextObject)
    {
        ShowPopup(nameof(SifrahGameMakeMoveForSlot), contextObject);
    }

    private void ShowPopup(string methodName, DummyGameObject contextObject)
    {
        _ = methodName;
        _ = contextObject;
        if (PopupMethod == nameof(DummyPopupShow.ShowYesNoCancel))
        {
            _ = DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
            return;
        }

        if (PopupMethod == nameof(DummyPopupShow.ShowFail))
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return;
        }

        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummySifrahTokenItemPopupProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public Action? BeforePopup { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SocialSifrahTokenGiftCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(SocialSifrahTokenGiftCheckTokenUse), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SocialSifrahTokenItemCheckTokenUse(DummyGameObject contextObject)
    {
        ShowPopup(nameof(SocialSifrahTokenItemCheckTokenUse), contextObject);
    }

    private void ShowPopup(string methodName, DummyGameObject contextObject)
    {
        _ = methodName;
        _ = contextObject;
        BeforePopup?.Invoke();
        DummyPopupShow.ShowFail(PopupMessageToShow);
    }
}

internal sealed class DummyProselytizationSifrahProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultCriticalFailure(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultCriticalFailure), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultFailure(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultFailure), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultPartialSuccess(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultPartialSuccess), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultSuccess(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultSuccess), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultExceptionalSuccess(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultExceptionalSuccess), contextObject);
    }

    private void ShowResultPopup(string methodName, DummyGameObject contextObject)
    {
        _ = methodName;
        _ = contextObject;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyRebukingSifrahProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultCriticalFailure(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultCriticalFailure), contextObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultPartialSuccess(DummyGameObject contextObject)
    {
        ShowResultPopup(nameof(ResultPartialSuccess), contextObject);
    }

    private void ShowResultPopup(string methodName, DummyGameObject contextObject)
    {
        _ = methodName;
        _ = contextObject;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyExaminerProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultSuccess(DummyGameObject actor)
    {
        ShowResultPopup(nameof(ResultSuccess), actor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultExceptionalSuccess(DummyGameObject actor)
    {
        ShowResultPopup(nameof(ResultExceptionalSuccess), actor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultFailure(DummyGameObject actor)
    {
        ShowResultPopup(nameof(ResultFailure), actor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ResultFakeConfusionFailure(DummyGameObject actor)
    {
        ShowResultPopup(nameof(ResultFakeConfusionFailure), actor);
    }

    private void ShowResultPopup(string methodName, DummyGameObject actor)
    {
        _ = methodName;
        _ = actor;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal sealed class DummyItemNamingProducerTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool Opportunity(
        DummyGameObject owner,
        DummyGameObject? kill = null,
        DummyGameObject? influencedBy = null,
        string? zoneId = null,
        string opportunityType = "General",
        int suppressedByAnyTypeForLevels = 0,
        int suppressedBySameTypeForLevels = 0,
        int suppressedBySameTypeOnlyIfAtLeast = 0,
        int chanceToBypassSuppression = 0,
        bool force = false)
    {
        _ = owner;
        _ = kill;
        _ = influencedBy;
        _ = zoneId;
        _ = opportunityType;
        _ = suppressedByAnyTypeForLevels;
        _ = suppressedBySameTypeForLevels;
        _ = suppressedBySameTypeOnlyIfAtLeast;
        _ = chanceToBypassSuppression;
        _ = force;
        DummyPopupShow.ShowYesNo(PopupMessageToShow);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CheckBestowals(
        DummyGameObject owner,
        DummyGameObject obj,
        string? type,
        string? element,
        DummyGameObject? kill,
        DummyGameObject? influencedBy,
        string opportunityType,
        out bool bestowalsPossible,
        out int didBasicBestowals,
        out bool didElementBestowal)
    {
        _ = owner;
        _ = obj;
        _ = type;
        _ = element;
        _ = kill;
        _ = influencedBy;
        _ = opportunityType;
        bestowalsPossible = true;
        didBasicBestowals = 1;
        didElementBestowal = false;
        DummyPopupShow.Show(PopupMessageToShow);
    }
}

internal static class DummyMutationsApiTarget
{
    public static string? FailureMessageToShow;

    public static string? ConfirmMessageToShow;

    public static void Reset()
    {
        FailureMessageToShow = null;
        ConfirmMessageToShow = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool BuyRandomMutation(DummyGameObject obj, int Cost = 4, bool Confirm = true, string? MutationTerm = null)
    {
        _ = obj;
        _ = Cost;
        _ = MutationTerm;

        if (!string.IsNullOrEmpty(FailureMessageToShow))
        {
            DummyPopupShow.Show(FailureMessageToShow);
            return false;
        }

        if (Confirm && !string.IsNullOrEmpty(ConfirmMessageToShow))
        {
            DummyPopupShow.ShowYesNo(ConfirmMessageToShow);
        }

        return true;
    }
}

internal sealed class DummyCookingEffectTextTarget
{
    public string ReturnValue { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetDescription()
    {
        return ReturnValue;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetTemplatedDescription()
    {
        return ReturnValue;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetTriggerDescription()
    {
        return ReturnValue;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetTemplatedTriggerDescription()
    {
        return ReturnValue;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetProceduralEffectDescription()
    {
        return ReturnValue;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetTemplatedProceduralEffectDescription()
    {
        return ReturnValue;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetDetails()
    {
        return ReturnValue;
    }
}
