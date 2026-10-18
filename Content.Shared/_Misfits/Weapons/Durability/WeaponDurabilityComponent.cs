// #Misfits Add - Durability of a weapon body.
//
// Two shapes share this component:
//   * Modular: the receiver/frame is the weapon body itself, not a contained
//     part, so its health lives here and everything else is a fitted
//     WeaponPartDurabilityComponent. Total/TotalMax are cached sums refreshed
//     by SharedWeaponDurabilitySystem. The frame values are copied across the
//     entity swap that construction performs (see the server construction
//     system), so they survive being built up and stripped back down.
//   * Monolithic: weapons that are not assembled from parts (map-spawned guns,
//     melee weapons) carry all of their durability here. ShotWear/MeleeWear/
//     ThrowWear drive their per-action wear and Features decides which
//     drawbacks apply once the weapon is worn.
namespace Content.Shared._Misfits.Weapons.Durability;

/// <summary>
///     Which drawbacks a monolithic weapon inflicts as it wears down. Modular
///     weapons derive their features from the roles of their fitted parts.
/// </summary>
[Flags]
public enum WeaponFeatures : byte
{
    None = 0,

    /// <summary>Chance to slip out of the user's hands.</summary>
    Drop = 1 << 0,

    /// <summary>Chance to jam and need racking before it will fire.</summary>
    Jam = 1 << 1,

    /// <summary>Extra spread as the barrel wears.</summary>
    Spread = 1 << 2,

    /// <summary>Less melee damage as the weapon wears.</summary>
    DamageDown = 1 << 3,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeaponDurabilityComponent : Component
{
    /// <summary>
    ///     Whether this weapon is assembled from fitted parts. Modular weapons
    ///     ignore the monolithic wear/features fields below.
    /// </summary>
    [DataField]
    public bool Modular;

    /// <summary>
    ///     Whether the body itself accumulates recoverable "deferred" wear
    ///     (restored with a cleaning rod / whetstone). Only used by monolithic
    ///     weapons; modular weapons keep deferred wear on their barrel part.
    /// </summary>
    [DataField]
    public bool Recoverable;

    [DataField]
    public float FrameMax = 500f;

    /// <summary>
    ///     Remaining body health. NaN means "fresh" and is set to
    ///     <see cref="FrameMax"/> on init.
    /// </summary>
    [DataField]
    public float FrameCurrent = float.NaN;

    /// <summary>
    ///     Recoverable wear on the body itself (monolithic only).
    /// </summary>
    [DataField]
    public float Deferred;

    // Monolithic per-action wear.
    [DataField]
    public float ShotWear = 2f;

    [DataField]
    public float MeleeWear = 1f;

    [DataField]
    public float ThrowWear = 2f;

    [DataField]
    public WeaponFeatures Features = WeaponFeatures.None;

    // Networked so the client predicts spread and melee damage identically to
    // the server, without needing every part's wear.
    [DataField, AutoNetworkedField]
    public float SpreadMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 1f;

    /// <summary>
    ///     Set by the server when the weapon fails to fire and must be racked.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Jammed;

    // Cached, not serialized: recomputed from the fitted parts.
    public float Total;
    public float TotalMax;
}
