
namespace Content.Shared.Item.ItemToggle.Components;

/// <summary>
/// Handles changes to GunComponent when the item is toggled.
/// </summary>
[RegisterComponent, Access(typeof(MinigunToggleSystem))]
public sealed partial class MinigunToggleComponent : Component
{
    /// <summary>
    /// fire rate when the gun is "inactive"
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("InactiveWeaponFireRate")]
    public float InactiveWeaponFireRate = 1f;

    /// <summary>
    /// speed modifier applied when the gun is "active"
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("ActivatedSpeedModifier")]
    public float ActivatedSpeedModifier = 0.1f;

    /// <summary>
    /// fire rate when the gun is "active". 0 means the prototype fire rate is used.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("ActivatedFireRate")]
    public float ActivatedFireRate = 0f;

    /// <summary>
    /// If true, the movement speed penalty (<see cref="ActivatedSpeedModifier"/>) is applied
    /// while the weapon is active. Ballistic chamber miniguns always apply it;
    /// energy (BypassChamber) weapons opt in explicitly.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("ApplyActiveSpeedModifier")]
    public bool ApplyActiveSpeedModifier;

    /// <summary>
    /// If true, the toggle will ignore the chamber state and always act as a plain on/off switch.
    /// Used by energy gauss weapons that have no ballistic chamber.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("BypassChamber")]
    public bool BypassChamber;

}
