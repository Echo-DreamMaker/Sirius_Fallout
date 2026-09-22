// #Misfits Add - Marks a weapon as accepting attachments in the four standard slots.
using Content.Shared._Misfits.WeaponAttachments;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.WeaponAttachments.Components;

/// <summary>
/// Placed on a weapon to declare which attachment categories it accepts.
/// The matching ItemSlots are named <c>gun_top</c>, <c>gun_muzzle</c>, <c>gun_bottom</c> and <c>gun_handle</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeaponAttachmentHostComponent : Component
{
    /// <summary>
    /// Which categories this weapon has slots for. Slots are only exposed if the matching ItemSlot exists.
    /// </summary>
    [DataField]
    public List<WeaponAttachmentSlot> Slots = new(WeaponAttachmentSlots.All);

    /// <summary>
    /// Set while the underbarrel weapon is selected, if one is attached.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UnderbarrelActive;

    /// <summary>
    /// Toggle action used to switch between the primary and underbarrel weapon modes.
    /// </summary>
    [DataField]
    public EntProtoId? UnderbarrelToggleAction = "N14ActionToggleUnderbarrel";

    /// <summary>
    /// Runtime entity of the underbarrel toggle action, created on demand.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? UnderbarrelToggleActionEntity;

    /// <summary>
    /// Runtime bookkeeping: true while a bayonet has modified this weapon's melee stats.
    /// </summary>
    [ViewVariables]
    public bool BayonetMeleeApplied;

    /// <summary>
    /// Runtime bookkeeping: the weapon's original melee damage, restored when the bayonet is removed.
    /// Null if the weapon had no melee attack before the bayonet was installed.
    /// </summary>
    [ViewVariables]
    public DamageSpecifier? OriginalMeleeDamage;
}
