using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void InitializeHybrid()
    {
        SubscribeLocalEvent<HybridAmmoProviderComponent, UpdateAmmoCounterEvent>(OnHybridUpdateAmmo);
        SubscribeLocalEvent<HybridAmmoProviderComponent, AmmoCounterControlEvent>(OnHybridControl);
        // #Misfits Add - HybridAmmoProvider only had a TakeAmmoEvent handler on the server, so with
        // misfits.gun_prediction on the client fell into the "empty gun" branch in
        // SharedGunSystem.AttemptShoot and never showed a muzzle flash, gunshot, recoil or bullet.
        SubscribeLocalEvent<HybridAmmoProviderComponent, TakeAmmoEvent>(OnHybridTakeAmmo);
        SubscribeLocalEvent<HybridAmmoProviderComponent, GetAmmoCountEvent>(OnHybridGetAmmoCount);
    }

    private void OnHybridUpdateAmmo(EntityUid uid, HybridAmmoProviderComponent component, UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
        {
            var ev = new GetAmmoCountEvent();
            RaiseLocalEvent(uid, ref ev, false);
            control.Update(ev.Count, ev.Capacity);
        }
    }

    private void OnHybridControl(EntityUid uid, HybridAmmoProviderComponent component, AmmoCounterControlEvent args)
    {
        args.Control = new DefaultStatusControl();
    }

    /// #Misfits Add - Client half of <see cref="HybridAmmoProviderComponent"/> take-ammo.
    /// <remarks>
    /// Mirrors the server's validation but only spawns the projectile; ammo consumption and battery
    /// drain stay server-side because <c>BatteryComponent</c> is a server-only, non-networked
    /// component, so the client cannot know the magazine's charge. A drained cell therefore
    /// mispredicts (bullet appears, then gets rolled back) rather than silently eating every shot.
    ///
    /// The spawned entity is FlagPredicted and the server pairs it with this client entity id via
    /// <c>PredictedProjectileServerComponent.ClientId</c>, so the bullet must stay the first entry
    /// of <see cref="TakeAmmoEvent.Ammo"/> and be spawned from <see cref="TakeAmmoEvent.Coordinates"/>
    /// for the index-based pairing in Content.Server GunSystem.Shoot to line up.
    /// </remarks>
    private void OnHybridTakeAmmo(EntityUid uid, HybridAmmoProviderComponent component, TakeAmmoEvent args)
    {
        var magazine = GetMagazineEntity(uid);

        if (magazine == null || !TryComp<BallisticAmmoProviderComponent>(magazine.Value, out var ballistic))
        {
            args.Reason = Loc.GetString("gun-no-magazine");
            return;
        }

        if (ballistic.AmmoCount <= 0)
        {
            args.Reason = Loc.GetString("gun-no-ammo");
            return;
        }

        var projectile = Spawn(component.Prototype, args.Coordinates.ToMap(EntityManager, _xform));
        FlagPredicted(projectile);

        args.Ammo.Add((projectile, EnsureShootable(projectile)));
    }

    /// #Misfits Add - Client ammo counter had no GetAmmoCountEvent handler for hybrid providers,
    /// so the gauge always read 0 shots. Reads the magazine's networked ballistic count instead.
    private void OnHybridGetAmmoCount(EntityUid uid, HybridAmmoProviderComponent component, ref GetAmmoCountEvent args)
    {
        var magazine = GetMagazineEntity(uid);

        if (magazine != null && TryComp<BallisticAmmoProviderComponent>(magazine.Value, out var ballistic))
        {
            args.Count = ballistic.AmmoCount;
            args.Capacity = ballistic.Capacity;
            return;
        }

        args.Count = 0;
        args.Capacity = 0;
    }
}
