// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared._Misfits.Genetics.Mutations;
using Robust.Shared.Network;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Force-unequips the listed inventory slots from the mob when this mutation is added.
/// Used by Headless so hats, glasses and masks fall off instead of floating in the air.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnequipSlotsMutationComponent : Component
{
    /// <summary>
    /// Inventory slot names to force-unequip when the mutation is added.
    /// </summary>
    [DataField(required: true)]
    public List<string> Slots = new();
}

public sealed class UnequipSlotsMutationSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnequipSlotsMutationComponent, MutationAddedEvent>(OnAdded);
    }

    private void OnAdded(Entity<UnequipSlotsMutationComponent> ent, ref MutationAddedEvent args)
    {
        // server-authoritative: items dropping is networked to clients anyway
        if (_net.IsClient)
            return;

        var target = args.Target.Owner;
        foreach (var slot in ent.Comp.Slots)
        {
            _inventory.TryUnequip(target, slot, force: true);
        }
    }
}
