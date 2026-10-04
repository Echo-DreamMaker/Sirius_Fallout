// #Misfits Add - Timed ammo-belt insertion for belt-fed machine guns.
// Belt-fed weapons (GunBeltInsertDelayComponent) never auto-insert a belt via ItemSlots;
// instead clicking a belt on the gun starts an Agility-scaled do-after and the belt is
// placed in the gun_magazine slot when it completes. Base delay is 4s (= exact at 5 Agility).
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected void InitializeWeaponBeltInteractions()
    {
        SubscribeLocalEvent<GunBeltInsertDelayComponent, InteractUsingEvent>(OnBeltInsertInteractUsing);
        SubscribeLocalEvent<GunBeltInsertDelayComponent, GunBeltInsertDoAfterEvent>(OnBeltInsertDoAfterComplete);
    }

    private void OnBeltInsertInteractUsing(EntityUid gunUid, GunBeltInsertDelayComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || Deleted(args.Used))
            return;

        // Only ammo belts that fit the gun_magazine whitelist go through the timed insert;
        // attachments and other slots keep their instant interact behavior.
        if (!_slots.TryGetSlot(gunUid, "gun_magazine", out var magazineSlot))
            return;

        // Mirror the instant insert gate (swap support included) so stale belts can be swapped.
        var swap = magazineSlot.Swap ?? _config.GetCVar(CCVars.AllowSlotQuickSwap);
        if (!_slots.CanInsert(gunUid, args.Used, args.User, magazineSlot, swap: swap, popup: args.User))
            return;

        args.Handled = true;

        if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.Delay,
            new GunBeltInsertDoAfterEvent(), used: args.Used, target: gunUid, eventTarget: gunUid)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = true,
            BlockDuplicate = true,
            RequireCanInteract = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true
        }))
            return;

        // Do-after could not start (duplicate already running, blocked, etc). Leave the belt in hand.
        args.Handled = false;
    }

    private void OnBeltInsertDoAfterComplete(EntityUid gunUid, GunBeltInsertDelayComponent comp, GunBeltInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || Deleted(gunUid) || Deleted(args.User))
            return;

        if (_slots.TryGetSlot(gunUid, "gun_magazine", out var magazineSlot)
            && TryComp<HandsComponent>(args.User, out var hands)
            && hands.ActiveHandEntity == args.Used)
        {
            // Swap out an old belt like the instant insert path would, so combat reloads still work.
            if (magazineSlot.HasItem)
                _slots.TryEjectToHands(gunUid, magazineSlot, args.User);

            _slots.TryInsertFromHand(gunUid, magazineSlot, args.User);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class GunBeltInsertDoAfterEvent : SimpleDoAfterEvent
{
}