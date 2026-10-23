// #Misfits Add - Carries a weapon's ammunition across the entity swap that
// construction performs when a finished weapon is sawn into a shorter variant.
//
// ChangeEntity only transfers the construction-owned "craft_*" containers, so
// the magazine sitting in "gun_magazine", any chambered round and a ballistic
// weapon's loose ammo would all be lost. The stock the player saws off is handed
// back by the graph's own "completed" action instead, so it is not handled here.
using Content.Server.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;

namespace Content.Server._Sirius.Weapons.Parts;

public sealed class WeaponAmmoCarrySystem : EntitySystem
{
    private const string MagazineSlot = "gun_magazine";
    private const string ChamberSlot = "gun_chamber";
    private const string CraftMagazine = "craft_magazine";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnChangeEntity);
    }

    private void OnChangeEntity(ConstructionChangeEntityEvent args)
    {
        CarryBallisticAmmo(args.Old, args.New);
        CarryMagazine(args.Old, args.New);
    }

    private void CarryBallisticAmmo(EntityUid old, EntityUid @new)
    {
        // Only gun-to-gun swaps carry loose ammo; a frame has none.
        if (!TryComp<BallisticAmmoProviderComponent>(old, out var from) ||
            !TryComp<BallisticAmmoProviderComponent>(@new, out var to))
            return;

        to.Proto = from.Proto;
        to.UnspawnedCount = Math.Clamp(from.AmmoCount, 0, to.Capacity);
        to.SpawnedCountPredict = 0;
        Dirty(@new, to);
    }

    private void CarryMagazine(EntityUid old, EntityUid @new)
    {
        if (!_container.TryGetContainer(@new, CraftMagazine, out var craft) ||
            !TryComp(old, out ItemSlotsComponent? slots))
            return;

        // The finished weapon seats whatever sits in craft_magazine on startup,
        // exactly like a hand-built one, so stage the old magazine (and any
        // chambered round) there - magazine first, so it gets the slot.
        Move(old, slots, MagazineSlot, craft);
        Move(old, slots, ChamberSlot, craft);
    }

    private void Move(EntityUid old, ItemSlotsComponent slots, string slotId, BaseContainer craft)
    {
        if (!_itemSlots.TryGetSlot(old, slotId, out var slot, slots) ||
            slot.ContainerSlot is not { } container ||
            container.ContainedEntity is not { } item)
            return;

        _container.Remove(item, container, reparent: false, force: true);
        _container.Insert(item, craft);
    }
}
