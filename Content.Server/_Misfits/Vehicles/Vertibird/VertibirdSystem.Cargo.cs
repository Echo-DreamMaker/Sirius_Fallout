// #Misfits Add - Crate hauling. Drag a closed crate onto the aircraft to load it with
// everything still packed inside; unload it and it lands under whoever pulled it out.
using System.Numerics;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared._MultiZ.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Misfits.Vehicles.Vertibird;

public sealed partial class VertibirdSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedVertibirdSystem _sharedVertibird = default!;

    private void InitializeCargo()
    {
        SubscribeLocalEvent<VertibirdComponent, DragDropTargetEvent>(OnCargoDragDrop);
        SubscribeLocalEvent<VertibirdComponent, VertibirdCargoDoAfterEvent>(OnCargoDoAfter);
        SubscribeLocalEvent<VertibirdComponent, GetVerbsEvent<AlternativeVerb>>(OnCargoVerbs);
        SubscribeLocalEvent<VertibirdComponent, VertibirdLoadCargoMessage>(OnLoadCargoMessage);
        SubscribeLocalEvent<VertibirdComponent, VertibirdUnloadCargoMessage>(OnUnloadCargoMessage);
    }

    /// <summary>
    /// The console has no way to choose a crate on the user's behalf, so it loads the one
    /// they pulled over to the aircraft.
    /// </summary>
    private void OnLoadCargoMessage(Entity<VertibirdComponent> ent, ref VertibirdLoadCargoMessage args)
    {
        var user = args.Actor;

        if (!TryComp<PullerComponent>(user, out var puller) || puller.Pulling is not { } crate)
        {
            _popup.PopupEntity(Loc.GetString("vertibird-cargo-nothing-pulled"), ent, user);
            return;
        }

        if (!_sharedVertibird.CanStoreCargo(ent, crate))
        {
            _popup.PopupEntity(Loc.GetString("vertibird-cargo-will-not-fit"), ent, user);
            return;
        }

        StartCargoDoAfter(ent, user, crate, loading: true);
    }

    private void OnUnloadCargoMessage(Entity<VertibirdComponent> ent, ref VertibirdUnloadCargoMessage args)
    {
        if (!TryGetEntity(args.Crate, out var crate) || !_sharedVertibird.GetCargo(ent).Contains(crate.Value))
            return;

        StartCargoDoAfter(ent, args.Actor, crate.Value, loading: false);
    }

    private void OnCargoDragDrop(Entity<VertibirdComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !_sharedVertibird.CanStoreCargo(ent, args.Dragged))
            return;

        args.Handled = true;
        StartCargoDoAfter(ent, args.User, args.Dragged, loading: true);
    }

    private void OnCargoVerbs(Entity<VertibirdComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !_actionBlocker.CanInteract(args.User, ent))
            return;

        var user = args.User;
        foreach (var crate in _sharedVertibird.GetCargo(ent))
        {
            var stored = crate;
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => StartCargoDoAfter(ent, user, stored, loading: false),
                Category = VerbCategory.Eject,
                Text = Name(stored),
            });
        }
    }

    /// <summary>
    /// Hauling a crate either way costs the same as climbing aboard: it is the same
    /// amount of wrestling something heavy through the same cabin door. Only a crate
    /// still out in the world is passed as the do-after's Used, so that dragging it
    /// out of reach cancels the load; one already in the bay cannot go anywhere.
    /// </summary>
    private void StartCargoDoAfter(Entity<VertibirdComponent> ent, EntityUid user, EntityUid crate, bool loading)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            BoardingDuration,
            new VertibirdCargoDoAfterEvent(GetNetEntity(crate)),
            ent.Owner,
            target: ent.Owner,
            used: loading ? crate : null)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCargoDoAfter(Entity<VertibirdComponent> ent, ref VertibirdCargoDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryGetEntity(args.Crate, out var crateUid))
            return;

        args.Handled = true;
        var crate = crateUid.Value;
        var container = _container.EnsureContainer<Container>(ent.Owner, SharedVertibirdSystem.CargoContainerId);

        if (container.Contains(crate))
            DropCargo(ent, crate, container, args.User);
        else if (_sharedVertibird.CanStoreCargo(ent, crate))
            _container.Insert(crate, container);

        UpdateUi(ent);
    }

    /// <summary>
    /// Where a crate ends up when it is hauled out on the same level. Someone standing
    /// outside gets it at their feet, but a crewman unloading from a seat is buckled to the
    /// craft, so their own coordinates are the craft's origin: handing those over parents the
    /// crate to the aircraft, which leaves it hidden under the hull and riding along wherever
    /// the craft flies. Their crates come out clear of the hull, resolved against the grid.
    /// </summary>
    private EntityCoordinates GetUnloadCoordinates(Entity<VertibirdComponent> ent, EntityUid user)
    {
        var userXform = Transform(user);
        if (userXform.ParentUid != ent.Owner)
            return _transform.GetMoverCoordinates(user, userXform);

        // Just past the hull's half-height, so the crate sits at the cabin door rather than
        // beneath the fuselage. Local, so it follows whichever way the craft is facing.
        return _transform.GetMoverCoordinates(new EntityCoordinates(ent.Owner, new Vector2(0f, -1.2f)));
    }

    /// <summary>
    /// On the ground the crate comes out beside whoever hauled it out. Airborne that is not
    /// an option: the one unloading is strapped in, so their coordinates are the craft's, and
    /// a crate spawned inside the hull shoves the whole aircraft around. Those go straight
    /// down to ground level, directly beneath the craft.
    /// </summary>
    private void DropCargo(Entity<VertibirdComponent> ent, EntityUid crate, BaseContainer container, EntityUid user)
    {
        var xform = Transform(ent.Owner);
        var depth = xform.MapUid is { } mapUid && TryComp<MZMapComponent>(mapUid, out var zMap)
            ? zMap.Depth
            : 0;

        if (depth <= 0 || !IsAirborne(ent.Comp.State))
        {
            _container.Remove(crate, container, destination: GetUnloadCoordinates(ent, user));
            return;
        }

        // Teleports rather than routing through MZ falling, same as the combat drop:
        // the crate arrives intact instead of taking impact damage on the way down.
        var landing = _transform.GetWorldPosition(xform);
        _container.Remove(crate, container);
        _multiZ.TryMove(crate, -depth, worldPosition: landing);
    }
}
