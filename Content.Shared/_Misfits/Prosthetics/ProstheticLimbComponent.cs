// #Misfits Add - Gameplay penalties supplied by installed prosthetic hands and feet.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Prosthetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProstheticLimbComponent : Component
{
    /// <summary>
    /// Multiplier applied to melee damage while this hand is installed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeDamageModifier = 1f;

    /// <summary>
    /// Multiplier applied to walking and sprinting while this foot is installed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementSpeedModifier = 1f;
}
