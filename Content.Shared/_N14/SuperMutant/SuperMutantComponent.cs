using Content.Shared.Damage;

namespace Content.Shared._N14.SuperMutant;

/// <summary>
/// Marks a wielder as a super mutant (or otherwise large/strong creature) that can
/// wield two-handed firearms with a single hand. Replaces the old "Oni" ability.
/// Melee bonus (the old Oni damage modifiers) is preserved here, while the firearm
/// behaviour is handled by <see cref="SharedSuperMutantSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class SuperMutantComponent : Component
{
    /// <summary>
    /// Flat damage modifiers applied to all outgoing melee attacks (unarmed + wielded melee).
    /// </summary>
    [DataField("modifiers", required: true)]
    public DamageModifierSet MeleeModifiers = default!;

    /// <summary>
    /// Multiplier applied to stamina damage taken by the super mutant (defensive bonus).
    /// </summary>
    [DataField("stamDamageBonus")]
    public float StamMultiplier = 1.25f;

    /// <summary>
    /// How much the firing spread is multiplied by when the super mutant uses a
    /// two-handed firearm that is NOT flagged <see cref="SuperMutantFittingComponent"/>
    /// (e.g. a submachine gun) with a single hand. Mirrors the old Oni 15x penalty.
    /// </summary>
    [DataField("unfittingSpreadMultiplier")]
    public float UnfittingSpreadMultiplier = 15f;
}
