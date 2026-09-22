// #Misfits Add - Marks an item as a weapon attachment and holds its passive gun modifiers.
using Content.Shared._Misfits.WeaponAttachments;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.WeaponAttachments.Components;

/// <summary>
/// Attached to an item that can be slotted into a weapon's attachment slot.
/// Passive stat changes are applied through <see cref="Content.Shared.Weapons.Ranged.Events.GunRefreshModifiersEvent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeaponAttachmentComponent : Component
{
    /// <summary>
    /// Which attachment category this item belongs to.
    /// </summary>
    [DataField(required: true)]
    public WeaponAttachmentSlot Slot = WeaponAttachmentSlot.Top;

    /// <summary>
    /// Additive change applied to the gun's minimum spread angle.
    /// Negative values tighten the spread.
    /// </summary>
    [DataField]
    public Angle MinAngleModifier = Angle.Zero;

    /// <summary>
    /// Additive change applied to the gun's maximum spread angle.
    /// Negative values tighten the spread.
    /// </summary>
    [DataField]
    public Angle MaxAngleModifier = Angle.Zero;

    /// <summary>
    /// Additive change applied to how much spread grows per shot.
    /// </summary>
    [DataField]
    public Angle AngleIncreaseModifier = Angle.Zero;

    /// <summary>
    /// Additive change applied to how fast spread decays.
    /// </summary>
    [DataField]
    public Angle AngleDecayModifier = Angle.Zero;

    /// <summary>
    /// Multiplier applied to camera recoil. Less than 1 reduces recoil.
    /// </summary>
    [DataField]
    public float CameraRecoilScalarModifier = 1f;

    /// <summary>
    /// Multiplier applied to fire rate.
    /// </summary>
    [DataField]
    public float FireRateModifier = 1f;

    /// <summary>
    /// If set, replaces the gun's gunshot sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundGunshotOverride;

    /// <summary>
    /// If true, the gunshot sound is removed entirely (suppressor).
    /// </summary>
    [DataField]
    public bool SuppressGunshot;

    /// <summary>
    /// Action prototype toggled while this attachment is held in hand or mounted
    /// on a weapon that is being held. Null for passive attachments.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? ToggleAction;

    /// <summary>
    /// Runtime: the spawned toggle action entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;
}
