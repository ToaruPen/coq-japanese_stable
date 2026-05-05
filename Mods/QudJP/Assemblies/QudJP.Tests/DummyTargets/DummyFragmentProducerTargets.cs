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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool HandleEvent(DummyInventoryActionEvent e)
    {
        _ = e;
        DummyPopupTarget.ShowBlock(PopupMessageToShow);
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
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool PerformFill(DummyGameObject actor, ref bool requestInterfaceExit, bool ownershipHandled = false)
    {
        _ = actor;
        _ = ownershipHandled;
        requestInterfaceExit = false;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
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
        DummyPopupShow.Show(PopupMessageToShow);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ExitEnclosure(DummyGameObject who, DummyGameEvent? e = null, DummyEnclosedEffect? enc = null)
    {
        _ = who;
        _ = e;
        _ = enc;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
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
    public string GetDetails()
    {
        return ReturnValue;
    }
}
