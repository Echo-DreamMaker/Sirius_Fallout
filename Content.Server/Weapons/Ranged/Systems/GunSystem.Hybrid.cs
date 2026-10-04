using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void InitializeHybrid()
    {
        base.InitializeHybrid();

        SubscribeLocalEvent<HybridAmmoProviderComponent, MapInitEvent>(OnHybridMapInit);
        SubscribeLocalEvent<HybridAmmoProviderComponent, EntInsertedIntoContainerMessage>(OnHybridSlotChange);
        SubscribeLocalEvent<HybridAmmoProviderComponent, EntRemovedFromContainerMessage>(OnHybridSlotChange);
        SubscribeLocalEvent<BatteryComponent, ChargeChangedEvent>(OnCellChargeChanged);
    }

    private void OnHybridMapInit(EntityUid uid, HybridAmmoProviderComponent component, ref MapInitEvent args)
    {
        SyncHybridCharge(uid, component);
    }

    private void OnHybridSlotChange(EntityUid uid, HybridAmmoProviderComponent component, ContainerModifiedMessage args)
    {
        if (MagazineSlot != args.Container.ID)
            return;

        SyncHybridCharge(uid, component);
    }

    protected override void TakeHybridCharge(EntityUid uid, HybridAmmoProviderComponent component, int shots)
    {
        var magazine = GetHybridMagazine(uid, component);

        if (magazine == null ||
            !TryComp<BatteryComponent>(magazine.Value, out var battery))
        {
            return;
        }

        _battery.TryUseCharge(magazine.Value, component.FireCost * shots, battery);
        SyncHybridCharge(uid, component);
    }

    /// <summary>
    /// Keeps the mirror honest when the cell is drained by something other than the gun: EMP, an
    /// external power draw, a rejuvenation, and so on. Without this the client would gate shots off
    /// a stale value and let through shots the server then refuses, i.e. free bullets.
    ///
    /// This walks the (very few) hybrid guns rather than keeping a magazine -> gun map, so there is
    /// no index to leak when a magazine is transferred or deleted mid-charge.
    /// </summary>
    private void OnCellChargeChanged(EntityUid uid, BatteryComponent battery, ref ChargeChangedEvent args)
    {
        var query = EntityQueryEnumerator<HybridAmmoProviderComponent>();

        while (query.MoveNext(out var gun, out var hybrid))
        {
            var magazine = GetHybridMagazine(gun, hybrid);

            if (magazine != uid)
                continue;

            PublishCharge(gun, hybrid, args.Charge, args.MaxCharge);
        }
    }

    /// <summary>
    /// Republishes the magazine's charge onto the networked component so the client can gate shots
    /// and show the examine line without ever touching the battery itself.
    /// </summary>
    private void SyncHybridCharge(EntityUid uid, HybridAmmoProviderComponent component)
    {
        var magazine = GetHybridMagazine(uid, component);

        if (magazine != null && TryComp<BatteryComponent>(magazine.Value, out var battery))
        {
            PublishCharge(uid, component, battery.CurrentCharge, battery.MaxCharge);
        }
        else
        {
            PublishCharge(uid, component, 0f, 0f);
        }
    }

    private void PublishCharge(EntityUid uid, HybridAmmoProviderComponent component, float charge, float maxCharge)
    {
        component.Charge = charge;
        component.MaxCharge = maxCharge;
        component.HasCell = maxCharge > 0f;
        Dirty(uid, component);
    }
}