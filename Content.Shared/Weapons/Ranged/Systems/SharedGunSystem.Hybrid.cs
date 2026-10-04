using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected virtual void InitializeHybrid()
    {
        // Gate before ammo is taken, so an empty cell never eats a cartridge.
        SubscribeLocalEvent<HybridAmmoProviderComponent, ShotAttemptedEvent>(OnHybridShotAttempted);
        // Raised on the gun by AttemptShoot on both client and server once the shot succeeded.
        SubscribeLocalEvent<HybridAmmoProviderComponent, GunShotEvent>(OnHybridGunShot);
        SubscribeLocalEvent<HybridAmmoProviderComponent, ExaminedEvent>(OnHybridExamine);
    }

    /// <summary>
    /// The battery magazine this weapon shoots from.
    /// </summary>
    protected EntityUid? GetHybridMagazine(EntityUid uid, HybridAmmoProviderComponent component)
    {
        if (!Containers.TryGetContainer(uid, MagazineSlot, out var container) ||
            container is not ContainerSlot slot)
        {
            return null;
        }

        return slot.ContainedEntity;
    }

    /// <summary>
    /// Blocks the shot when the cell cannot supply <see cref="HybridAmmoProviderComponent.FireCost"/>.
    /// Runs on the client too, using the charge mirror, so the player finds out before the trigger
    /// click rather than after a round trip.
    /// </summary>
    private void OnHybridShotAttempted(EntityUid uid, HybridAmmoProviderComponent component, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled)
            return;

        // No magazine is not this component's business; MagazineAmmoProvider reports that itself.
        if (!component.HasCell)
            return;

        if (component.Charge >= component.FireCost)
            return;

        args.Cancel();
        Popup(Loc.GetString("gun-not-enough-energy"), uid, args.User);

        // Predicted like the gunshot itself, so the click lands on the same tick the player pulled
        // the trigger rather than a round trip later. Guarded because predicted handlers run more
        // than once per tick on the client.
        if (Timing.IsFirstTimePredicted)
            Audio.PlayPredicted(component.SoundNoCharge, uid, args.User);
    }

    /// <summary>
    /// Spends charge for a shot that actually went off. <paramref name="shots"/> is the number of
    /// projectiles the ammo providers produced, so an empty magazine costs nothing.
    /// </summary>
    private void OnHybridGunShot(EntityUid uid, HybridAmmoProviderComponent component, ref GunShotEvent args)
    {
        var shots = args.Ammo.Count;
        if (shots == 0 || component.FireCost <= 0f)
            return;

        TakeHybridCharge(uid, component, shots);
    }

    /// <summary>
    /// Server-only hook that draws <c>FireCost * shots</c> from the magazine's battery and refreshes
    /// the networked mirror. The client keeps its mirrored values; only the server owns the battery.
    /// </summary>
    protected virtual void TakeHybridCharge(EntityUid uid, HybridAmmoProviderComponent component, int shots)
    {
    }

    /// <summary>
    /// The ballistic count already comes from the magazine chain; this adds the charge line.
    /// Reported in whole charges rather than joules, because the cell is sized as
    /// <c>capacity * fireCost</c> and the number that matters to a player is "how many more shots".
    /// </summary>
    private void OnHybridExamine(EntityUid uid, HybridAmmoProviderComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!component.HasCell)
        {
            args.PushMarkup(Loc.GetString("gun-no-magazine"));
            return;
        }

        var charges = component.ChargesLeft;
        var max = component.ChargesMax;
        var percent = max > 0 ? charges * 100f / max : 0f;
        args.PushMarkup(Loc.GetString("gun-hybrid-charge-examine",
            ("color", AmmoExamineColor),
            ("charge", charges),
            ("max", max),
            ("percent", (int) percent)));
    }
}