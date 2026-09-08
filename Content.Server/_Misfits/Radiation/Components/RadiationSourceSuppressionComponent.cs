// #Misfits Add - Glowing ghoul radiation suppression: wearing a radiation suit
// disables the wearer's own RadiationSource so they no longer irradiate people nearby.
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Radiation.Components;

/// <summary>
///     Marked on clothing (e.g. the radiation suit). When equipped on a mob that
///     emits radiation (such as a glowing ghoul), the wearer's
///     <c>RadiationSourceComponent</c> is disabled until the item is removed.
/// </summary>
[RegisterComponent]
[ComponentProtoName("RadiationSourceSuppression")]
public sealed partial class RadiationSourceSuppressionComponent : Component
{
    /// <summary>
    ///     Which inventory slots activate the suppression. Defaults to the outer clothing slot.
    /// </summary>
    [DataField]
    public SlotFlags AllowedSlots = SlotFlags.OUTERCLOTHING;
}
