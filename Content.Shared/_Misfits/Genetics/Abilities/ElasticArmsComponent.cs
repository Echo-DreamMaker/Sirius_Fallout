// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Rubbery arms: great reach, but the fingers are too soft to work gloves onto and too
/// floppy to keep hold of anything that needs both hands.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ElasticArmsComponent : Component
{
    /// <summary>
    /// Inventory slots that can't be worn with these arms.
    /// </summary>
    [DataField]
    public List<string> BlockedSlots = new() { "gloves" };

    /// <summary>
    /// Whether items requiring more than one hand can be picked up.
    /// </summary>
    [DataField]
    public bool BlockTwoHanded = true;
}

public sealed class ElasticArmsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ElasticArmsComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<ElasticArmsComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnEquipAttempt(Entity<ElasticArmsComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (!ent.Comp.BlockedSlots.Contains(args.Slot))
            return;

        args.Reason = "elastic-arms-no-gloves";
        args.Cancel();
    }

    private void OnPickupAttempt(Entity<ElasticArmsComponent> ent, ref PickupAttemptEvent args)
    {
        if (!ent.Comp.BlockTwoHanded || !HasComp<MultiHandedItemComponent>(args.Item))
            return;

        _popup.PopupClient(Loc.GetString("elastic-arms-too-floppy", ("item", args.Item)), ent, ent);
        args.Cancel();
    }
}
