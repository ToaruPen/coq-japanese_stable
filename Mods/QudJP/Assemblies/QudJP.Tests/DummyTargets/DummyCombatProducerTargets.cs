namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCombatGetDefenderHitDiceEvent
{
}

internal sealed class DummyCombatBodyPart
{
    public string Name { get; set; } = string.Empty;
}

internal struct DummyMeleeAttackResult
{
}

internal sealed class DummyCombatGetDefenderHitDiceTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public bool HandleEvent(DummyCombatGetDefenderHitDiceEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}

internal sealed class DummyCombatMeleeAttackTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public DummyMeleeAttackResult MeleeAttackWithWeaponInternal(
        DummyGameObject attacker,
        DummyGameObject defender,
        DummyGameObject weapon,
        DummyCombatBodyPart bodyPart,
        string? properties = null,
        int hitModifier = 0,
        int penModifier = 0,
        int penCapModifier = 0,
        int adjustDamageResult = 0,
        int adjustDamageDieSize = 0,
        bool primary = false,
        bool intrinsic = true)
    {
        _ = attacker;
        _ = defender;
        _ = weapon;
        _ = bodyPart;
        _ = properties;
        _ = hitModifier;
        _ = penModifier;
        _ = penCapModifier;
        _ = adjustDamageResult;
        _ = adjustDamageDieSize;
        _ = primary;
        _ = intrinsic;

        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return default;
    }
}
