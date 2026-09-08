// #Misfits Add - Bunker tunnel teleporter: a surface hatch that drops you somewhere in the tunnels,
// and a sealed vault door down there that brings you back up.
using System.Numerics;
using Content.Server.Popups;
using Content.Server.Warps;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Misfits.Warps;

/// <summary>
/// Drives the bunker tunnel teleporter pair.
/// <para>
/// The surface hatch rolls one <see cref="BunkerTunnelExitComponent"/> marker at random the first
/// time it is used and keeps it for the rest of the round, so players can learn where a given hatch
/// comes out. The tunnel door has no fixed destination — it finds the nearest hatch every time, so
/// it keeps working when hatches are added, moved or removed mid-round.
/// </para>
/// <para>
/// Nothing is stored on disk and no ids are matched up, which is what lets an admin spawn a pair
/// anywhere, in any order, and have it work immediately.
/// </para>
/// </summary>
public sealed class BunkerTeleporterSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly WarperSystem _warper = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Same three events WarperSystem handles, so this feels identical to a normal ladder.
        SubscribeLocalEvent<BunkerTeleporterComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<BunkerTeleporterComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<BunkerTeleporterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractHand(EntityUid uid, BunkerTeleporterComponent component, InteractHandEvent args)
    {
        TryTraverse(uid, component, args.User, args.Target);
    }

    private void OnActivateInWorld(EntityUid uid, BunkerTeleporterComponent component, ActivateInWorldEvent args)
    {
        if (TryTraverse(uid, component, args.User, args.Target))
            args.Handled = true;
    }

    private void OnExamined(EntityUid uid, BunkerTeleporterComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Regular ghosts cannot hand-interact, so close-range examine is how they travel. Admin
        // ghosts interact normally and are already covered by the events above.
        if (!TryComp(args.Examiner, out GhostComponent? ghost) || ghost.CanGhostInteract)
            return;

        TryTraverse(uid, component, args.Examiner, uid);
    }

    private bool TryTraverse(EntityUid uid, BunkerTeleporterComponent component, EntityUid user, EntityUid target)
    {
        var destination = ResolveDestination(uid, component);
        if (destination is null)
        {
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", target)), user, Filter.Entities(user), true);
            return false;
        }

        return _warper.WarpEntityTo(user, destination.Value);
    }

    /// <summary>
    /// Works out where this end sends you, right now.
    /// </summary>
    private EntityUid? ResolveDestination(EntityUid uid, BunkerTeleporterComponent component)
    {
        // The tunnel door has no fixed destination; it always heads back to the nearest hatch.
        if (!component.IsSurface)
            return FindNearestPartner(uid, component);

        // A hatch keeps whatever it rolled, as long as that exit still exists.
        if (component.CachedDestination is { } cached
            && !Deleted(cached)
            && LifeStage(cached) < EntityLifeStage.Terminating)
        {
            return cached;
        }

        var exits = new List<EntityUid>();
        var query = EntityQueryEnumerator<BunkerTunnelExitComponent>();
        while (query.MoveNext(out var exit, out var exitComponent))
        {
            if (exitComponent.Channel == component.Channel)
                exits.Add(exit);
        }

        if (exits.Count > 0)
        {
            var picked = _random.Pick(exits);
            component.CachedDestination = picked;
            return picked;
        }

        // No exit markers placed anywhere. Fall back to the paired door so that admin-spawning just
        // a hatch and a door on a bare map still gives a working round trip.
        return FindNearestPartner(uid, component);
    }

    /// <summary>
    /// Finds the closest teleporter of the opposite kind on the same channel. Only entities carrying
    /// <see cref="BunkerTeleporterComponent"/> are considered, so ordinary ladders, manholes and the
    /// old cryo bunker hatches are never picked up by mistake.
    /// </summary>
    private EntityUid? FindNearestPartner(EntityUid uid, BunkerTeleporterComponent component)
    {
        var origin = _transform.GetMapCoordinates(uid);

        EntityUid? nearestOnMap = null;
        var nearestDistance = float.MaxValue;
        EntityUid? anywhere = null;

        var query = EntityQueryEnumerator<BunkerTeleporterComponent>();
        while (query.MoveNext(out var other, out var otherComponent))
        {
            if (other == uid
                || otherComponent.IsSurface == component.IsSurface
                || otherComponent.Channel != component.Channel)
            {
                continue;
            }

            anywhere ??= other;

            var otherCoordinates = _transform.GetMapCoordinates(other);
            if (otherCoordinates.MapId != origin.MapId)
                continue;

            var distance = Vector2.Distance(origin.Position, otherCoordinates.Position);
            if (distance >= nearestDistance)
                continue;

            nearestOnMap = other;
            nearestDistance = distance;
        }

        // Prefer a partner on this map. Failing that, one on another map still beats going nowhere.
        return nearestOnMap ?? anywhere;
    }
}
