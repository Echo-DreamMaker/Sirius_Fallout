// #Misfits Add - Underbarrel weapon attachment marker.
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.WeaponAttachments.Components;

/// <summary>
/// Marks a bottom attachment as an alternate underbarrel weapon (shotgun, flamethrower, grenade launcher...).
/// The attachment itself is a full gun with its own <see cref="GunComponent"/> and ammo provider.
/// While <see cref="WeaponAttachmentHostComponent.UnderbarrelActive"/> is set the host weapon forwards its
/// shots to this entity, and it can be reloaded by using ammunition on the host weapon.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnderslungWeaponComponent : Component
{
}
