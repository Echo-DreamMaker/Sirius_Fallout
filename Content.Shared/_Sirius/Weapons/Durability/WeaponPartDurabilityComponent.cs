// #Misfits Add - Per-part wear for the modular weapon durability system.
//
// Durability lives on the loose part entity rather than on the assembled
// weapon, because construction moves the very same part entity between
// containers when a gun is assembled or stripped: whatever a part was worn
// down to travels with it.
//
// "Current" is the part's remaining health (it never goes below zero).
// "Deferred" is firing wear that has dulled the part but is still recoverable;
// only the barrel fills it (a cleaning rod restores it later). Deferred does
// not count towards the weapon's total, it is a repair debt.
namespace Content.Shared._Sirius.Weapons.Durability;

/// <summary>
///     What a fitted part does, which decides how much it wears per shot and
///     which drawback it inflicts once it is damaged.
/// </summary>
public enum WeaponPartRole : byte
{
    Barrel,
    Bolt,
    Grip,
    Stock,
    Drive,
    Optics,
    Other,
}

[RegisterComponent]
public sealed partial class WeaponPartDurabilityComponent : Component
{
    [DataField]
    public WeaponPartRole Role = WeaponPartRole.Other;

    [DataField]
    public float Max = 250f;

    /// <summary>
    ///     Remaining health. NaN means "fresh" and is set to <see cref="Max"/> on
    ///     init; an explicitly zeroed part stays at zero.
    /// </summary>
    [DataField]
    public float Current = float.NaN;

    /// <summary>
    ///     Recoverable firing wear, restored by a cleaning rod. Only the barrel
    ///     accumulates this.
    /// </summary>
    [DataField]
    public float Deferred;
}
