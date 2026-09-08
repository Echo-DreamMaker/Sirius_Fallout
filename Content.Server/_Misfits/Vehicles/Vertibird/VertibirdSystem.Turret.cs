// #Misfits Add - Co-pilot turret. The gunner's mind occupies a camera entity that
// tracks the craft from one Z-level below, so they can see and shoot the ground
// while the vertibird holds altitude. Their body stays buckled in the seat.
using Content.Server._N14.Support;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;

namespace Content.Server._Misfits.Vehicles.Vertibird;

public sealed partial class VertibirdSystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private VertibirdSupportSystem _support = default!;

    private void InitializeTurret()
    {
        SubscribeLocalEvent<VertibirdTurretComponent, VertibirdEnterTurretActionEvent>(OnEnterTurret);
        SubscribeLocalEvent<VertibirdTurretComponent, ComponentShutdown>(OnTurretShutdown);

        // Exit and fire are provided by the eye, and PerformAction raises an action's event
        // on its container, so these land on the eye rather than on the craft.
        SubscribeLocalEvent<VertibirdTurretEyeComponent, VertibirdExitTurretActionEvent>(OnExitTurret);
        SubscribeLocalEvent<VertibirdTurretEyeComponent, VertibirdFireTurretActionEvent>(OnFireTurret);
    }

    /// <summary>
    /// Keeps every active turret camera pinned over its craft. Called from the main update loop.
    /// </summary>
    private void UpdateTurretEyes()
    {
        var query = EntityQueryEnumerator<VertibirdTurretComponent>();
        while (query.MoveNext(out var uid, out var turret))
        {
            if (turret.TurretEye is not { } eye || Deleted(eye))
                continue;

            // Someone shooting the gunner's unattended body, or the body being
            // removed outright, has to drop them out of the turret.
            if (turret.Gunner is not { } gunner || Deleted(gunner) || _mobState.IsDead(gunner))
            {
                ExitTurret((uid, turret));
                continue;
            }

            // The craft has to be airborne over a level for the camera to have
            // anywhere to sit. Landing or losing the level below ends the session.
            if (!TryGetTurretEyeCoordinates(uid, out var coords) || coords is not { } resolved)
            {
                ExitTurret((uid, turret));
                continue;
            }

            _transform.SetCoordinates(eye, resolved);
        }
    }

    /// <summary>
    /// Resolves where the camera should sit: the craft's world position, on the map one Z below it.
    /// Ground level has no usable level below it, so the turret does not bear there.
    /// </summary>
    private bool TryGetTurretEyeCoordinates(EntityUid vertibird, out EntityCoordinates? coords)
    {
        coords = null;

        if (!TryComp<VertibirdComponent>(vertibird, out var comp) ||
            comp.State != VertibirdFlightState.Cruising)
        {
            return false;
        }

        if (!TryGetLevelBelow(vertibird, out var belowMap, out var worldPosition))
            return false;

        coords = new EntityCoordinates(belowMap.Owner, worldPosition);
        return true;
    }

    /// <summary>
    /// Grants or revokes the turret actions as the co-pilot seat changes hands.
    /// </summary>
    private void RefreshTurretSeat(Entity<VertibirdComponent> ent, int seatIndex, EntityUid? occupant)
    {
        if (!TryComp<VertibirdTurretComponent>(ent, out var turret) || seatIndex != turret.GunnerSeat)
            return;

        if (occupant == null)
        {
            if (turret.Gunner is { } previous)
            {
                ExitTurret((ent.Owner, turret));
                _actions.RemoveAction(previous, turret.EnterActionEntity);
                turret.EnterActionEntity = null;
            }

            turret.Gunner = null;
            Dirty(ent.Owner, turret);
            return;
        }

        turret.Gunner = occupant;
        _actions.AddAction(occupant.Value, ref turret.EnterActionEntity, turret.EnterAction, ent.Owner);
        Dirty(ent.Owner, turret);
    }

    private void OnEnterTurret(Entity<VertibirdTurretComponent> ent, ref VertibirdEnterTurretActionEvent args)
    {
        if (args.Handled || ent.Comp.Gunner != args.Performer)
            return;

        args.Handled = true;

        if (ent.Comp.TurretEye != null)
            return;

        if (!TryGetTurretEyeCoordinates(ent.Owner, out var coords))
        {
            _popup.PopupEntity(Loc.GetString("vertibird-turret-needs-altitude"), args.Performer, args.Performer);
            return;
        }

        if (!_mind.TryGetMind(args.Performer, out var mindId, out _))
            return;

        var eye = Spawn(ent.Comp.EyeProto, coords!.Value);
        var eyeComp = EnsureComp<VertibirdTurretEyeComponent>(eye);
        eyeComp.Vertibird = ent.Owner;
        eyeComp.Gunner = args.Performer;

        _mind.Visit(mindId, eye);

        ent.Comp.TurretEye = eye;

        // Container defaults to the performer, which is what we want here: action entities
        // are parented into their container, so putting them on the craft would leave them
        // a Z-level above the gunner and outside the PVS of the eye they are attached to.
        // The client would receive no action entities and draw an empty hotbar.
        _actions.AddAction(eye, ref ent.Comp.ExitActionEntity, ent.Comp.ExitAction);
        _actions.AddAction(eye, ref ent.Comp.FireActionEntity, ent.Comp.FireAction);
        Dirty(ent);
    }

    private void OnExitTurret(Entity<VertibirdTurretEyeComponent> ent, ref VertibirdExitTurretActionEvent args)
    {
        if (args.Handled || !TryGetTurretFromEye(ent, out var turret))
            return;

        args.Handled = true;
        ExitTurret(turret);
    }

    /// <summary>
    /// Walks back from the camera to the craft that owns it. The eye keeps this reference
    /// precisely so the actions it carries can find their way home.
    /// </summary>
    private bool TryGetTurretFromEye(Entity<VertibirdTurretEyeComponent> eye, out Entity<VertibirdTurretComponent> turret)
    {
        turret = default;

        if (eye.Comp.Vertibird is not { } vertibird ||
            !TryComp<VertibirdTurretComponent>(vertibird, out var comp) ||
            comp.TurretEye != eye.Owner)
        {
            return false;
        }

        turret = (vertibird, comp);
        return true;
    }

    /// <summary>
    /// Returns the gunner's mind to their body and tears down the camera.
    /// Safe to call when no turret session is active.
    /// </summary>
    private void ExitTurret(Entity<VertibirdTurretComponent> ent)
    {
        if (ent.Comp.TurretEye is not { } eye)
            return;

        if (ent.Comp.Gunner is { } gunner && _mind.TryGetMind(gunner, out var mindId, out var mind) &&
            mind.VisitingEntity == eye)
        {
            _mind.UnVisit(mindId, mind);
        }
        else if (TryComp<VertibirdTurretEyeComponent>(eye, out var eyeComp) &&
                 eyeComp.Gunner is { } trackedGunner &&
                 _mind.TryGetMind(trackedGunner, out var fallbackMindId, out var fallbackMind))
        {
            // Seat changed hands mid-session; still get whoever is in the camera back out.
            _mind.UnVisit(fallbackMindId, fallbackMind);
        }

        _actions.RemoveAction(eye, ent.Comp.ExitActionEntity);
        _actions.RemoveAction(eye, ent.Comp.FireActionEntity);
        ent.Comp.ExitActionEntity = null;
        ent.Comp.FireActionEntity = null;
        ent.Comp.TurretEye = null;
        QueueDel(eye);
        Dirty(ent);
    }

    private void OnFireTurret(Entity<VertibirdTurretEyeComponent> ent, ref VertibirdFireTurretActionEvent args)
    {
        if (args.Handled || !TryGetTurretFromEye(ent, out var turret))
            return;

        args.Handled = true;

        var infiniteAmmo = TryComp<VertibirdComponent>(turret, out var vertibird) && vertibird.DebugInfiniteAmmo;

        // Popups go on the eye: the gunner is looking through it and cannot see the craft.
        if (!infiniteAmmo && _charges.IsEmpty(turret.Owner))
        {
            _popup.PopupEntity(Loc.GetString("vertibird-turret-no-ammo"), ent, args.Performer);
            return;
        }

        var target = _transform.ToMapCoordinates(args.Target);
        if (target.MapId == MapId.Nullspace)
            return;

        if (!infiniteAmmo)
            _charges.UseCharge(turret.Owner);

        _support.ScheduleSupport(
            target,
            approachDelay: TimeSpan.Zero,
            delay: turret.Comp.FireDelay,
            shots: turret.Comp.Shots,
            interval: turret.Comp.ShotInterval,
            spread: turret.Comp.Spread,
            lineLength: turret.Comp.LineLength,
            intensity: turret.Comp.Intensity,
            slope: turret.Comp.Slope,
            maxIntensity: turret.Comp.MaxIntensity,
            tileBreakScale: turret.Comp.TileBreakScale,
            fire: turret.Comp.FireSound);
    }

    private void OnTurretShutdown(Entity<VertibirdTurretComponent> ent, ref ComponentShutdown args)
    {
        ExitTurret(ent);
    }

    /// <summary>
    /// Tops the craft's magazine up from a restock item, consuming only the charges
    /// actually needed. Returns true if any rounds were transferred.
    /// </summary>
    public bool TryRestockTurret(EntityUid vertibird, EntityUid restock)
    {
        if (!TryComp<LimitedChargesComponent>(vertibird, out var magazine) ||
            !TryComp<LimitedChargesComponent>(restock, out var supply))
        {
            return false;
        }

        var needed = magazine.MaxCharges - magazine.Charges;
        if (needed <= 0 || supply.Charges <= 0)
            return false;

        var transferred = Math.Min(needed, supply.Charges);
        _charges.AddCharges(vertibird, transferred, magazine);
        _charges.AddCharges(restock, -transferred, supply);
        return true;
    }
}
