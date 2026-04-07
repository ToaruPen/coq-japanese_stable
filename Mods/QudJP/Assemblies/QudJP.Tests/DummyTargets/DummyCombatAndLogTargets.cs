using System;
using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCell
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class DummyZone
{
    public string ZoneId { get; set; } = string.Empty;
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
