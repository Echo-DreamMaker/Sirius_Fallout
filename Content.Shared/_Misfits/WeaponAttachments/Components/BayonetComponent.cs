// #Misfits Add - Bayonet attachment behaviour.
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._Misfits.WeaponAttachments.Components;

/// <summary>
/// A bayonet. While attached it gives the weapon melee damage so the weapon can be used to stab.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BayonetComponent : Component
{
    /// <summary>
    /// Melee damage granted to the host weapon while the bayonet is attached.
    /// </summary>
    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>
    /// Melee reach granted to the host weapon while the bayonet is attached.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// How many seconds between stabs.
    /// </summary>
    [DataField]
    public float AttackRate = 1f;

    /// <summary>
    /// Rotation of the wide attack animation while the bayonet is attached.
    /// </summary>
    [DataField]
    public Angle WideAnimationRotation = Angle.FromDegrees(-135);

    /// <summary>
    /// Hit sound played when stabbing with the bayonet.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundHit;
}
