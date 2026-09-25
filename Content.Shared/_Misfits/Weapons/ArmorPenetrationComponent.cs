using Robust.Shared.GameStates;

// #Misfits Add - Percentage-based armor penetration for ballistic projectiles.
// The value is the fraction of the target's armor (both innate damage modifier
// sets and worn ArmorComponent) that is ignored when the round connects.
// 0 = no penetration, 1 = fully ignore armor.

namespace Content.Shared._Misfits.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArmorPenetrationComponent : Component
{
    /// <summary>
    /// Fraction of armor ignored, 0 (none) to 1 (ignore all armor).
    /// </summary>
    [DataField("penetration"), AutoNetworkedField]
    public float Penetration = 0f;
}