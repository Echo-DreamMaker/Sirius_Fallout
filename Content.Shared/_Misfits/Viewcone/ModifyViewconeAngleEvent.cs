// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;

namespace Content.Shared._Misfits.Viewcone;

/// <summary>
/// Compatibility event relayed to mutation entities. Nuclear-14 does not use Trauma's cone-vision subsystem,
/// but retaining the event keeps the mutation relay and imported data model intact.
/// </summary>
[ByRefEvent]
public record struct ModifyViewconeAngleEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.MASK;

    public float AngleModifier { get; private set; } = 1f;

    public void ModifyAngle(float angle)
    {
        AngleModifier *= angle;
    }
}
