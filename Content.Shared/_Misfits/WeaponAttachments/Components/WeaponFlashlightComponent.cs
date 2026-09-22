// #Misfits Add - Mounted weapon flashlight attachment behaviour.
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.WeaponAttachments.Components;

/// <summary>
/// A flashlight mounted on a weapon. Toggles the PointLight placed on the attachment prototype.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeaponFlashlightComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;
}
