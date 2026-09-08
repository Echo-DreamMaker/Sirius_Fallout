
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
    /// fire rate when the gun is "active"
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("ActivatedFireRate")]
    public float ActivatedFireRate = 8f;

}
