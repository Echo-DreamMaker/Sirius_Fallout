using Robust.Shared.GameStates;

namespace Content.Shared._N14.Special.Components;

/// <summary>
/// Marks an entity (a player character) as capable of entering aim mode through
/// the SPECIAL action. Toggled by <see cref="SpecialAimingSystem"/>; grants a
/// Perception-scaled zoom and accuracy bonus. Applied via an action granted to
/// every character that participates in SPECIAL stats.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpecialAimableComponent : Component
{
    /// <summary>
    /// Whether aim mode is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Aiming;

    /// <summary>
    /// The granted toggle action entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    /// <summary>
    /// The weapon the character was aiming with when aim mode was switched on. Aime mode
    /// is cancelled the moment the active weapon changes, so the old gun's reach can never
    /// leak onto a different weapon. Not networked; runtime-only.
    /// </summary>
    public EntityUid? AimedWeapon;
}
