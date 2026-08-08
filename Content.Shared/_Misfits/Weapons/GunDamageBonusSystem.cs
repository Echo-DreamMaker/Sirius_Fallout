using Content.Shared._Misfits.Weapons;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
// #Misfits Fix - Use concrete HitscanBatteryAmmoProviderComponent; abstract BatteryAmmoProviderComponent
// is not resolvable via TryComp in Robust ECS and caused NullReferenceException during entity spawn preview.

// #Misfits Add - Handles fire cost multiplier for guns with GunDamageBonusComponent.
// When a cell is inserted into a gun that has a FireCostMultiplier, the cell's
// FireCost is scaled up so fewer shots are available. Restored on ejection.
// Bonus damage is applied separately in GunSystem.cs (server-side hitscan hit path).
namespace Content.Shared._Misfits.Weapons;

public sealed class GunDamageBonusSystem : EntitySystem
{
    private const string MagazineSlot = "gun_magazine";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunDamageBonusComponent, EntInsertedIntoContainerMessage>(OnMagInserted);
        SubscribeLocalEvent<GunDamageBonusComponent, EntRemovedFromContainerMessage>(OnMagRemoved);
        SubscribeLocalEvent<GunDamageBonusComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, GunDamageBonusComponent comp, ComponentStartup args)
    {
        if (comp.FireCostMultiplier == 1.0f)
            return;

        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        if (!containers.Containers.TryGetValue(MagazineSlot, out var container))
            return;

        foreach (var ent in container.ContainedEntities)
        {
            ApplyFireCostMultiplier(uid, ent, comp);
            break;
        }
    }

    private void OnMagInserted(EntityUid uid, GunDamageBonusComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != MagazineSlot)
            return;

        if (comp.FireCostMultiplier == 1.0f)
            return;

        ApplyFireCostMultiplier(uid, args.Entity, comp);
    }

    private void OnMagRemoved(EntityUid uid, GunDamageBonusComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != MagazineSlot)
            return;

        RestoreFireCost(args.Entity, comp);
    }

    // #Sirius Add:

    private bool TryGetFireCost(EntityUid uid, out float fireCost)
    {
        if (TryComp<HitscanBatteryAmmoProviderComponent>(uid, out var hitscan))
        {
            fireCost = hitscan.FireCost;
            return true;
        }

        if (TryComp<ProjectileBatteryAmmoProviderComponent>(uid, out var proj))
        {
            fireCost = proj.FireCost;
            return true;
        }

        if (TryComp<BallisticAmmoProviderComponent>(uid, out var ballistic))
        {
        }

        fireCost = 0;
        return false;
    }

    private bool TrySetFireCost(EntityUid uid, float newCost)
    {
        if (TryComp<HitscanBatteryAmmoProviderComponent>(uid, out var hitscan))
        {
            hitscan.FireCost = newCost;
            Dirty(uid, hitscan);
            return true;
        }

        if (TryComp<ProjectileBatteryAmmoProviderComponent>(uid, out var proj))
        {
            proj.FireCost = newCost;
            Dirty(uid, proj);
            return true;
        }

        if (TryComp<BallisticAmmoProviderComponent>(uid, out var ballistic))
        {
            Dirty(uid, ballistic);
            return true;
        }

        return false;
    }

    // ----- Применение и восстановление множителя -----

    private void ApplyFireCostMultiplier(EntityUid gunUid, EntityUid cellUid, GunDamageBonusComponent comp)
    {
        if (!TryGetFireCost(cellUid, out var currentCost))
            return;

        comp.OriginalFireCost = currentCost;

        float newCost = currentCost * comp.FireCostMultiplier;
        TrySetFireCost(cellUid, newCost);
    }

    private void RestoreFireCost(EntityUid cellUid, GunDamageBonusComponent comp)
    {
        if (comp.OriginalFireCost == null)
            return;

        TrySetFireCost(cellUid, comp.OriginalFireCost.Value);
        comp.OriginalFireCost = null;
    }
}
