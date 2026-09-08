using Content.Shared.Damage;

namespace Content.Server._Misfits.TreeOfLife;

/// <summary>
///     Grants nearby living players a restorative aura and welcome message.
/// </summary>
[RegisterComponent]
public sealed partial class TreeOfLifeHealingComponent : Component
{
    [DataField]
    public float Range = 16f;

    [DataField]
    public float HealingCooldown = 3f;

    [DataField]
    public DamageSpecifier Healing = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public float HealingAccumulator;

    [DataField]
    public float HearthHealingCooldown = 1.5f;

    [DataField]
    public float HearthStaminaRecovery = 3f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float HearthHealingAccumulator;

    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> PlayersInRange = new();
}
