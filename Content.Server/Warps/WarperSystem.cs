using Content.Server.Ghost.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Shared.Mobs.Systems;
using Content.Shared._Misfits.NPC;
using Content.Shared._Misfits.NPC.Components;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using System.Numerics;

namespace Content.Server.Warps;

public sealed class WarperSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly WarpPointSystem _warpPointSystem = default!;
    [Dependency] private readonly SharedTransformSystem _sharedTransform = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarperComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<WarperComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<WarperComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractHand(EntityUid uid, WarperComponent component, InteractHandEvent args)
    {
        TryWarpUser(uid, component, args.User, args.Target);
    }

    private void OnActivateInWorld(EntityUid uid, WarperComponent component, ActivateInWorldEvent args)
    {
        // #Misfits Change /Fix/: support hotkey world activation so E triggers ladder warps too.
        if (TryWarpUser(uid, component, args.User, args.Target))
            args.Handled = true;
    }

    private void OnExamined(EntityUid uid, WarperComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!TryComp(args.Examiner, out GhostComponent? ghost) || ghost.CanGhostInteract)
            return;

        // #Misfits Change /Fix/: regular ghosts cannot hand-interact, so let close-range examine traverse ladders.
        TryWarpUser(uid, component, args.Examiner, uid);
    }

    private bool TryWarpUser(EntityUid uid, WarperComponent component, EntityUid user, EntityUid target)
    {
        if (component.ID is null)
        {
            Log.Debug("Warper has no destination");
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", target)), user, Filter.Entities(user), true);
            return false;
        }

        var dest = _warpPointSystem.FindWarpPoint(component.ID);
        if (dest is null)
        {
            Log.Debug($"Warp destination '{component.ID}' not found");
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", target)), user, Filter.Entities(user), true);
            return false;
        }

        if (!HasComp<TransformComponent>(dest.Value))
        {
            Log.Debug($"Warp destination '{component.ID}' has no transform");
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", target)), user, Filter.Entities(user), true);
            return false;
        }

        // #Misfits Change /Refactor/: the move itself lives in WarpEntityTo so the bunker tunnel
        // teleporter reuses it rather than duplicating the pull/follower/velocity handling.
        if (WarpEntityTo(user, dest.Value))
            return true;

        Log.Debug($"Player tried to warp to '{component.ID}', which is not on a running map");
        _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", target)), user, Filter.Entities(user), true);
        return false;
    }

    /// <summary>
    /// #Misfits Change /Refactor/: moves a user, or the mech they are piloting, onto a destination
    /// entity тАФ bringing whatever they are pulling and any recruited followers along, and killing
    /// their velocity so they do not slide on arrival.
    /// </summary>
    /// <returns>
    /// False if the destination has no transform, or its map is not running and the user is not an
    /// admin ghost.
    /// </returns>
    public bool WarpEntityTo(EntityUid user, EntityUid destination)
    {
        if (!TryComp(destination, out TransformComponent? destXform))
            return false;

        // Check that the destination map is initialized and return unless in aghost mode.
        var destMap = destXform.MapID;
        if (!_mapSystem.MapExists(destMap) || !_mapSystem.IsInitialized(destMap) || _mapSystem.IsPaused(destMap))
        {
            // Normal ghosts cannot interact, so if we're here this is already an admin ghost.
            if (!HasComp<GhostComponent>(user))
                return false;
        }

        var warpEntity = user;
        if (TryComp<MechPilotComponent>(user, out var pilot))
            warpEntity = pilot.Mech;

        // Forge-Change-Start
        if (TryComp(warpEntity, out PullerComponent? puller) && puller.Pulling != null)
        {
            var pullerItem = puller.Pulling.Value;
            _sharedTransform.SetCoordinates(pullerItem, destXform.Coordinates);
            _sharedTransform.AttachToGridOrMap(pullerItem);
            _sharedTransform.SetCoordinates(warpEntity, destXform.Coordinates);
            _sharedTransform.AttachToGridOrMap(warpEntity);
            _pullingSystem.TryStartPull(warpEntity, pullerItem); // Throws a client error, not critical but unpleasant.
        }

        else
        {
            _sharedTransform.SetCoordinates(warpEntity, destXform.Coordinates);
            _sharedTransform.AttachToGridOrMap(warpEntity);
        }

        if (HasComp<PhysicsComponent>(warpEntity))
        {
            _physics.SetLinearVelocity(warpEntity, Vector2.Zero);
        }
        // Forge-Change-End

        if (HasComp<FollowerCommanderComponent>(user))
        {
            var followerQuery = EntityManager.EntityQueryEnumerator<RecruitedFollowerComponent>();
            while (followerQuery.MoveNext(out var follower, out var recruited))
            {
                if (recruited.Commander != user)
                    continue;
                if (recruited.Order == FollowerOrderType.HoldPosition && !recruited.WasAutoHeld)
                    continue;
                if (!_mobState.IsAlive(follower))
                    continue;

                _sharedTransform.SetCoordinates(follower, destXform.Coordinates);
                _sharedTransform.AttachToGridOrMap(follower);
                if (HasComp<PhysicsComponent>(follower))
                    _physics.SetLinearVelocity(follower, Vector2.Zero);
                _npcSystem.OnFollowerWarped(follower);
            }
        }

        return true;
    }
}
