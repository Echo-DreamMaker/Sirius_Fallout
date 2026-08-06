using Content.Shared._Misfits.Mounts;
using Content.Shared._NC.Mountable;
using Content.Shared._NC.Mountable.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Misfits.Mounts;

/// <summary>
/// Spooks a Brahdo when its rider fires a gun, making it ignore input,
/// wander randomly, and possibly buck the rider off.
/// Prevents horseback dominance in combat.
/// </summary>
public sealed class BrahdoSpookSystem : EntitySystem
{
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<BrahdoSpookedComponent> _spookedQuery;
    private EntityQuery<MountableComponent> _mountableQuery;
    private EntityQuery<RiderComponent> _riderQuery;

    public override void Initialize()
    {
        base.Initialize();
        _spookedQuery = GetEntityQuery<BrahdoSpookedComponent>();
        _mountableQuery = GetEntityQuery<MountableComponent>();
        _riderQuery = GetEntityQuery<RiderComponent>();

        // Fires on the user BEFORE the shot resolves.
        SubscribeLocalEvent<RiderComponent, SelfBeforeGunShotEvent>(OnBeforeGunShot);

        // Cancel mount control when spooked, preventing the relay from being set up.
        SubscribeLocalEvent<BrahdoSpookedComponent, MountMovementControlAttemptEvent>(OnMountControlAttempt);
    }

    /// <summary>
    /// When a rider shoots, spook the Brahdo they are mounted on.
    /// </summary>
    private void OnBeforeGunShot(Entity<RiderComponent> rider, ref SelfBeforeGunShotEvent args)
    {
        if (rider.Comp.Mount is not { } mount)
            return;

        if (!_tag.HasTag(mount, "Brahdo"))
            return;

        if (!_mountableQuery.TryGetComponent(mount, out var mountable))
            return;

        Spook((mount, mountable), rider.Owner);
    }

    /// <summary>
    /// Cancel mount movement control while spooked, preventing the relay from being set up.
    /// </summary>
    private void OnMountControlAttempt(Entity<BrahdoSpookedComponent> ent, ref MountMovementControlAttemptEvent args)
    {
        if (ent.Comp.Accumulator > 0f)
            args.Cancelled = true;
    }

    /// <summary>
    /// Spook the Brahdo: break relay, enable NPC wandering, maybe buck.
    /// </summary>
    private void Spook(Entity<MountableComponent> mount, EntityUid rider)
    {
        var spooked = EnsureComp<BrahdoSpookedComponent>(mount.Owner);
        spooked.Accumulator = spooked.SpookDuration;

        // Break input relay so rider cannot control the mount.
        BreakRelay(rider, mount.Owner);

        // Let the Brahdo wander on its own via NPC AI.
        EnsureComp<ActiveNPCComponent>(mount.Owner);
        mount.Comp.HadActiveNpcBeforeMount = true;
        Dirty(mount.Owner, mount.Comp);

        // Chance to buck the rider.
        if (_random.NextFloat() < spooked.BuckChance)
        {
            _buckle.TryUnbuckle(rider, mount.Owner);
            _popup.PopupEntity(Loc.GetString("brahdo-spook-buck"), rider, rider, PopupType.LargeCaution);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("brahdo-spook-alert"), rider, rider, PopupType.LargeCaution);
        }
    }

    /// <summary>
    /// Removes RelayInputMoverComponent from the rider to sever input forwarding to the mount.
    /// </summary>
    private void BreakRelay(EntityUid rider, EntityUid mount)
    {
        if (TryComp<RelayInputMoverComponent>(rider, out var relay) && relay.RelayEntity == mount)
        {
            RemComp<RelayInputMoverComponent>(rider);
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BrahdoSpookedComponent, MountableComponent>();
        while (query.MoveNext(out var uid, out var spooked, out var mountable))
        {
            spooked.Accumulator -= frameTime;
            if (spooked.Accumulator > 0f)
                continue;

            // Spook expired - Brahdo recovers.
            RemComp<BrahdoSpookedComponent>(uid);

            if (mountable.Rider is { } rider && mountable.RiderControlsMovement)
            {
                // Re-establish input relay so the rider can control the mount again.
                _mover.SetRelay(rider, uid);
                RemComp<ActiveNPCComponent>(uid);
            }
        }
    }
}
