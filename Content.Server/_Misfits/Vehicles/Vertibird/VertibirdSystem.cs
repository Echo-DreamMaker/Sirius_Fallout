// #Misfits Add - Server-side flyable vertibird POC.
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared._Misfits.Vehicles.Aircraft;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared._MultiZ.Core.Components;
using Content.Server._MultiZ.Core;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weather;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Vehicles.Vertibird;

public sealed partial class VertibirdSystem : EntitySystem
{
    private const float PilotCameraMaxOffset = 10f;
    private const float PilotCameraPvsScale = 1.75f;
    private const float BoardingDuration = 5f;

    private static readonly ProtoId<DamageTypePrototype> FallDamageType = "Blunt";

    private static readonly Vector2[] LandingFootprintSamples =
    [
        Vector2.Zero,
        new(-1.6f, -0.6f),
        new(-1.6f, 0f),
        new(-1.6f, 0.6f),
        new(0f, -0.6f),
        new(0f, 0.6f),
        new(1.6f, -0.6f),
        new(1.6f, 0f),
        new(1.6f, 0.6f),
    ];

    [Dependency] private AnchorableSystem _anchorable = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MZSystem _multiZ = default!;
    [Dependency] private MZPvsSystem _multiZPvs = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly Dictionary<EntityUid, int> _pendingSeatSelections = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VertibirdComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<VertibirdComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VertibirdComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<VertibirdComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<VertibirdComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<VertibirdComponent, VertibirdFlightActionEvent>(OnFlightAction);
        SubscribeLocalEvent<VertibirdComponent, VertibirdLandActionEvent>(OnLandAction);
        SubscribeLocalEvent<VertibirdComponent, VertibirdMoveUpActionEvent>(OnMoveUpAction);
        SubscribeLocalEvent<VertibirdComponent, VertibirdMoveDownActionEvent>(OnMoveDownAction);
        SubscribeLocalEvent<VertibirdComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VertibirdComponent, AfterActivatableUIOpenEvent>(OnAfterUiOpen);
        SubscribeLocalEvent<VertibirdComponent, VertibirdSelectSeatMessage>(OnSelectSeat);
        SubscribeLocalEvent<VertibirdComponent, VertibirdBoardDoAfterEvent>(OnBoardDoAfter);
        SubscribeLocalEvent<VertibirdComponent, SolutionTransferAttemptEvent>(OnFuelTransferAttempt);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<VertibirdControlInputMessage>(OnControlInput);
        SubscribeNetworkEvent<VertibirdCameraOffsetMessage>(OnCameraOffset);

        // #Cythisiax Removed - Vertibird co-pilot turret disabled: unwanted feature whose
        // per-frame camera tracking caused lag.
        // InitializeTurret();
        InitializeCombatDrop();
        InitializeCargo();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // #Cythisiax Removed - Vertibird co-pilot turret disabled; this per-frame camera
        // tracking was the lag source and is no longer needed.
        // UpdateTurretEyes();

        var query = EntityQueryEnumerator<VertibirdComponent, MZPhysicsComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vertibird, out var mzPhysics, out var physics, out var xform))
        {
            UpdateFuelUi((uid, vertibird));
            UpdateFuelWarnings((uid, vertibird));

            if (ConsumesFuel(vertibird.State) && !TryConsumeFuel(uid, vertibird, frameTime))
                HandleFuelEmergency((uid, vertibird), xform);

            if (vertibird.EmergencyLandingActive)
                HandleEmergencyLanding((uid, vertibird), xform);

            switch (vertibird.State)
            {
                case VertibirdFlightState.Starting:
                    UpdateStartup((uid, vertibird));
                    break;
                case VertibirdFlightState.TakingOff:
                    UpdateTakeoff(uid, vertibird, mzPhysics, frameTime);
                    break;
                case VertibirdFlightState.Landing:
                    UpdateLanding(uid, vertibird, mzPhysics, frameTime);
                    break;
                case VertibirdFlightState.Cruising:
                    HoldHover(uid, vertibird, mzPhysics);
                    UpdateCruising(uid, vertibird, physics, xform, frameTime);
                    break;
                case VertibirdFlightState.ChangingAltitude:
                    UpdateAltitudeTransition(uid, vertibird, mzPhysics, xform);
                    break;
            }
        }
    }

    private void OnStartup(Entity<VertibirdComponent> ent, ref ComponentStartup args)
    {
        // Seat capacity is per-vehicle; resize the
        // server-side seat array to match the prototype's SeatCount.
        if (ent.Comp.SeatOccupants.Length != ent.Comp.SeatCount)
            ent.Comp.SeatOccupants = new EntityUid?[ent.Comp.SeatCount];
    }

    private void OnStrapAttempt(Entity<VertibirdComponent> ent, ref StrapAttemptEvent args)
    {
        var occupant = args.Buckle.Owner;

        if (!_pendingSeatSelections.TryGetValue(occupant, out var seatIndex))
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("vertibird-use-seat-manifest"), ent, occupant);
            return;
        }

        if (!IsValidSeat(ent.Comp, seatIndex) || ent.Comp.SeatOccupants[seatIndex] != null)
        {
            args.Cancelled = true;
            return;
        }

        if (seatIndex == 0 && !HasComp<VertibirdPilotPerkComponent>(occupant))
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("vertibird-pilot-required"), ent, occupant);
        }
    }

    private void OnStrapped(Entity<VertibirdComponent> ent, ref StrappedEvent args)
    {
        var occupant = args.Buckle.Owner;
        if (!_pendingSeatSelections.Remove(occupant, out var seatIndex) || !IsValidSeat(ent.Comp, seatIndex))
            return;

        ent.Comp.SeatOccupants[seatIndex] = occupant;
        HideOccupant(occupant);

        if (seatIndex == 0)
        {
            ent.Comp.Pilot = occupant;
            AddPilotActions(occupant, ent);
        }

        RefreshTurretSeat(ent, seatIndex, occupant);
        RefreshCombatDropSeat(ent, occupant, boarding: true);

        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnUnstrapAttempt(Entity<VertibirdComponent> ent, ref UnstrapAttemptEvent args)
    {
        // Pressing Land commits the craft to a controlled descent and should
        // immediately unlock the cabin for egress; waiting for the final
        // Grounded tick left occupants trapped through the landing sequence.
        if (ent.Comp.State is VertibirdFlightState.Landing or VertibirdFlightState.Grounded)
            return;

        // #Misfits Add - power armour combat drop unbuckles deliberately while airborne.
        if (args.Buckle.Owner == _combatDropUnbuckling)
            return;

        // #Misfits Add - stepping out under your own power is allowed at any altitude;
        // the fall in OnUnstrapped is the price. Being thrown out by someone else is not,
        // so a passenger cannot be murdered by whoever is sitting next to them.
        if (args.User == args.Buckle.Owner)
            return;

        args.Cancelled = true;

        if (args.Popup && args.User is { } user)
            _popup.PopupEntity(Loc.GetString("vertibird-unbuckle-blocked"), ent, user);
    }

    private void OnUnstrapped(Entity<VertibirdComponent> ent, ref UnstrappedEvent args)
    {
        if (ent.Comp.Pilot != args.Buckle.Owner)
        {
            var seat = GetSeatIndex(ent.Comp, args.Buckle.Owner);
            if (seat != null)
            {
                ent.Comp.SeatOccupants[seat.Value] = null;
                RefreshTurretSeat(ent, seat.Value, null);
            }

            RefreshCombatDropSeat(ent, args.Buckle.Owner, boarding: false);
            UnhideOccupant(args.Buckle.Owner);
            DropUnbuckledOccupant(ent, args.Buckle.Owner);
            Dirty(ent);
            UpdateUi(ent);
            return;
        }

        RemovePilotAction(args.Buckle.Owner, ent.Comp);
        RemovePilotRelay(args.Buckle.Owner, ent.Owner);
        ent.Comp.Pilot = null;

        var pilotSeat = GetSeatIndex(ent.Comp, args.Buckle.Owner);
        if (pilotSeat != null)
        {
            ent.Comp.SeatOccupants[pilotSeat.Value] = null;
            RefreshTurretSeat(ent, pilotSeat.Value, null);
        }

        RefreshCombatDropSeat(ent, args.Buckle.Owner, boarding: false);
        UnhideOccupant(args.Buckle.Owner);
        DropUnbuckledOccupant(ent, args.Buckle.Owner);

        // The pilot just stepped out of a flying craft. Same outcome as losing them
        // to a disconnect, so route it through the same automatic descent.
        if (TryComp(ent.Owner, out TransformComponent? craftXform))
            HandlePilotLost(ent.Owner, ent.Comp, craftXform);

        Dirty(ent);
        UpdateUi(ent);
    }

    /// <summary>
    /// Sends someone who left an airborne craft down to ground level, hurt in proportion
    /// to how far they fell. Depth drives both, so a craft hovering at ground level or
    /// parked on an upper level drops nobody.
    /// </summary>
    private void DropUnbuckledOccupant(Entity<VertibirdComponent> ent, EntityUid occupant)
    {
        // The combat drop unbuckles as part of its own controlled descent: one level,
        // no damage. Letting this run as well would slam them to ground level first
        // and then drop them a further level below that.
        if (occupant == _combatDropUnbuckling)
            return;

        if (!IsAirborne(ent.Comp.State))
            return;

        if (!TryComp(ent.Owner, out TransformComponent? xform) ||
            xform.MapUid is not { } mapUid ||
            !TryComp<MZMapComponent>(mapUid, out var zMap) ||
            zMap.Depth <= 0)
        {
            return;
        }

        var depth = zMap.Depth;

        // Land where the craft is rather than where the occupant's transform sits, so a
        // seat offset cannot drop someone through a wall on the level below.
        if (!_multiZ.TryMove(occupant, -depth, worldPosition: _transform.GetWorldPosition(xform)))
            return;

        var damage = ent.Comp.FallDamagePerLevel * depth;
        if (damage > 0f && HasComp<DamageableComponent>(occupant))
        {
            _damageable.TryChangeDamage(
                occupant,
                new DamageSpecifier(_proto.Index(FallDamageType), damage),
                origin: ent.Owner);
        }

        _popup.PopupEntity(Loc.GetString("vertibird-fall-landed"), occupant, occupant);
        SendVertibirdEmote(ent.Owner, "vertibird-rp-occupant-fell");
    }

    /// <summary>
    /// Resolves the map directly below the craft, but only while it is genuinely above
    /// ground level. At depth 0 the map below is the underground, so putting a gunner's
    /// eye or a dropping passenger down there would place them beneath the world.
    /// </summary>
    private bool TryGetLevelBelow(EntityUid vertibird, out Entity<MZMapComponent> belowMap, out Vector2 worldPosition)
    {
        belowMap = default;
        worldPosition = Vector2.Zero;

        if (!TryComp(vertibird, out TransformComponent? xform) ||
            xform.MapUid is not { } mapUid ||
            !TryComp<MZMapComponent>(mapUid, out var zMap) ||
            zMap.Depth <= 0 ||
            !_multiZ.TryMapOffset(mapUid, -1, out var below))
        {
            return false;
        }

        belowMap = below.Value;
        worldPosition = _transform.GetWorldPosition(xform);
        return true;
    }

    /// <summary>
    /// Whether the craft is off the deck. Grounded and Starting both sit on the ground.
    /// </summary>
    private static bool IsAirborne(VertibirdFlightState state)
    {
        return state is VertibirdFlightState.TakingOff
            or VertibirdFlightState.Cruising
            or VertibirdFlightState.ChangingAltitude
            or VertibirdFlightState.Landing;
    }

    private void OnFlightAction(Entity<VertibirdComponent> ent, ref VertibirdFlightActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Pilot != args.Performer)
            return;

        if (!HasComp<VertibirdPilotPerkComponent>(args.Performer))
            return;

        switch (ent.Comp.State)
        {
            case VertibirdFlightState.Grounded:
                if (!HasMinimumTakeoffFuel(ent))
                {
                    _popup.PopupEntity(Loc.GetString("vertibird-fuel-takeoff-blocked"), ent, args.Performer);
                    args.Handled = true;
                    break;
                }

                StartTakeoff(ent);
                args.Handled = true;
                break;
        }
    }

    private void OnLandAction(Entity<VertibirdComponent> ent, ref VertibirdLandActionEvent args)
    {
        if (args.Handled || !CanUsePilotAction(ent, args.Performer))
            return;

        if (ent.Comp.State is VertibirdFlightState.TakingOff or VertibirdFlightState.Cruising)
        {
            if (!CanLandHere(ent, out var failureMessage))
            {
                _popup.PopupEntity(Loc.GetString(failureMessage), ent, args.Performer);
                args.Handled = true;
                return;
            }

            StartLanding(ent);
            args.Handled = true;
        }
        else if (ent.Comp.State == VertibirdFlightState.Starting)
        {
            CancelStartup(ent);
            args.Handled = true;
        }
    }

    private void OnMoveUpAction(Entity<VertibirdComponent> ent, ref VertibirdMoveUpActionEvent args)
    {
        if (args.Handled || !CanUsePilotAction(ent, args.Performer))
            return;

        args.Handled = TryMoveZ(ent, 1);
    }

    private void OnMoveDownAction(Entity<VertibirdComponent> ent, ref VertibirdMoveDownActionEvent args)
    {
        if (args.Handled || !CanUsePilotAction(ent, args.Performer))
            return;

        args.Handled = TryMoveZ(ent, -1);
    }

    private bool CanUsePilotAction(Entity<VertibirdComponent> ent, EntityUid performer)
    {
        if (ent.Comp.Pilot != performer)
            return false;

        if (HasComp<VertibirdPilotPerkComponent>(performer))
            return true;

        _popup.PopupEntity(Loc.GetString("vertibird-pilot-required"), ent, performer);
        return false;
    }

    private void StartTakeoff(Entity<VertibirdComponent> ent)
    {
        ent.Comp.FuelEmergencyActive = false;
        ent.Comp.EmergencyLandingActive = false;
        ent.Comp.State = VertibirdFlightState.Starting;
        ent.Comp.StartupStartedAt = _timing.CurTime;
        ent.Comp.StartupFinishedAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.StartupDuration);
        ent.Comp.StartupEmoteIndex = 0;
        ent.Comp.DriftVelocity = Vector2.Zero;
        ent.Comp.HeldInputs = VertibirdControlInput.None;
        if (ent.Comp.Pilot is { } pilot)
            ResetPilotView(pilot);
        ent.Comp.StartupSoundStream = _audio.Stop(ent.Comp.StartupSoundStream);

        if (ent.Comp.StartupSound != null)
            ent.Comp.StartupSoundStream = _audio.PlayPvs(ent.Comp.StartupSound, ent.Owner)?.Entity;

        SendVertibirdEmote(ent.Owner, ent.Comp.StartupEmote);
        Dirty(ent);
        UpdateUi(ent);
    }

    private void CancelStartup(Entity<VertibirdComponent> ent)
    {
        ent.Comp.StartupSoundStream = _audio.Stop(ent.Comp.StartupSoundStream);
        ent.Comp.State = VertibirdFlightState.Grounded;
        ent.Comp.StartupStartedAt = TimeSpan.Zero;
        ent.Comp.StartupFinishedAt = TimeSpan.Zero;
        ent.Comp.StartupEmoteIndex = 0;
        ent.Comp.DriftVelocity = Vector2.Zero;
        ent.Comp.HeldInputs = VertibirdControlInput.None;
        Dirty(ent);
        UpdateUi(ent);
    }

    private void UpdateStartup(Entity<VertibirdComponent> ent)
    {
        SendStartupProgressEmotes(ent);

        if (!ent.Comp.DebugInstantFlight && _timing.CurTime < ent.Comp.StartupFinishedAt)
            return;

        StartTakeoffLift(ent);
    }

    private void SendStartupProgressEmotes(Entity<VertibirdComponent> ent)
    {
        if (ent.Comp.StartupStartedAt == TimeSpan.Zero || ent.Comp.StartupDuration <= 0f)
            return;

        var progressEmotes = ent.Comp.StartupProgressEmotes;
        if (progressEmotes.Length == 0)
            return;

        var startupElapsed = (_timing.CurTime - ent.Comp.StartupStartedAt).TotalSeconds;
        var emoteInterval = ent.Comp.StartupDuration / (progressEmotes.Length + 1);

        while (ent.Comp.StartupEmoteIndex < progressEmotes.Length &&
               startupElapsed >= emoteInterval * (ent.Comp.StartupEmoteIndex + 1))
        {
            SendVertibirdEmote(ent.Owner, progressEmotes[ent.Comp.StartupEmoteIndex]);
            ent.Comp.StartupEmoteIndex++;
            Dirty(ent);
        }
    }

    private void StartTakeoffLift(Entity<VertibirdComponent> ent)
    {
        if (!TryComp<MZPhysicsComponent>(ent, out var mzPhysics) ||
            !TryComp(ent.Owner, out TransformComponent? xform) ||
            !TryComp<PhysicsComponent>(ent, out var physics) ||
            xform.MapUid is not { } mapUid)
            return;

        // MultiZ only supports entities parented directly to their map. Using
        // SetMapCoordinates here reattaches the vertibird to the grid under it,
        // causing MultiZ to reset LocalPosition to zero every update.
        var worldPosition = _transform.GetWorldPosition(ent.Owner);
        _transform.SetCoordinates(ent.Owner, new EntityCoordinates(mapUid, worldPosition));

        mzPhysics.LocalPosition = 0f;
        mzPhysics.Velocity = 0f;

        // #Misfits Fix - Clear the rigid body, not just our own bookkeeping value below.
        // While parked, occupants sit on the craft's own tile and overlap its fixture, so the
        // solver pushes to separate them. A power armour wearer cancels being pushed back
        // (PowerArmorWornComponent cancels AttemptMobTargetCollideEvent to act as an immovable
        // wall), so the whole separation impulse lands on the craft instead of being shared.
        // On the ground grid friction hides it. The moment we lift off we reparent to the map
        // and go InAir, friction stops applying, and that stored velocity threw the aircraft.
        // Takeoff must always begin from rest.
        _physics.SetLinearVelocity(ent.Owner, Vector2.Zero, body: physics);
        _physics.SetAngularVelocity(ent.Owner, 0f, body: physics);

        _physics.SetBodyStatus(ent.Owner, physics, BodyStatus.InAir);
        ent.Comp.State = VertibirdFlightState.TakingOff;
        ent.Comp.StartupStartedAt = TimeSpan.Zero;
        ent.Comp.StartupFinishedAt = TimeSpan.Zero;
        ent.Comp.StartupEmoteIndex = 0;
        ent.Comp.StartupSoundStream = _audio.Stop(ent.Comp.StartupSoundStream);
        ent.Comp.DriftVelocity = Vector2.Zero;
        ent.Comp.HeldInputs = VertibirdControlInput.None;
        SendVertibirdEmote(ent.Owner, ent.Comp.TakeoffEmote);
        Dirty(ent);
        RemComp<MZFallingComponent>(ent.Owner);
        UpdateUi(ent);
    }

    private void StartLanding(Entity<VertibirdComponent> ent)
    {
        if (!TryComp<MZPhysicsComponent>(ent, out var mzPhysics))
            return;

        ent.Comp.StartupSoundStream = _audio.Stop(ent.Comp.StartupSoundStream);
        StopFlightLoop(ent.Comp);

        if (ent.Comp.LandingSound != null)
            _audio.PlayPvs(ent.Comp.LandingSound, ent.Owner);

        mzPhysics.Velocity = 0f;
        ent.Comp.State = VertibirdFlightState.Landing;
        ent.Comp.DriftVelocity = Vector2.Zero;
        ent.Comp.HeldInputs = VertibirdControlInput.None;
        Dirty(ent);
        RemComp<MZFallingComponent>(ent.Owner);
        UpdateUi(ent);
    }

    private void UpdateTakeoff(EntityUid uid, VertibirdComponent vertibird, MZPhysicsComponent mzPhysics, float frameTime)
    {
        var next = vertibird.DebugInstantFlight
            ? vertibird.HoverAltitude
            : MathF.Min(vertibird.HoverAltitude, mzPhysics.LocalPosition + vertibird.VerticalSpeed * frameTime);
        mzPhysics.LocalPosition = next;
        mzPhysics.Velocity = 0f;
        RemComp<MZFallingComponent>(uid);

        // #Misfits Fix - The climb is purely vertical, so pin horizontal velocity to zero the
        // whole way up. Occupants still overlap the fixture during the climb and can keep
        // feeding the craft separation impulses until Cruising takes control of velocity.
        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            _physics.SetAngularVelocity(uid, 0f, body: physics);
        }

        if (next < vertibird.HoverAltitude)
            return;

        vertibird.State = VertibirdFlightState.Cruising;
        StartFlightLoop(uid, vertibird);
        Dirty(uid, vertibird);
        UpdateUi((uid, vertibird));
    }

    private void UpdateLanding(EntityUid uid, VertibirdComponent vertibird, MZPhysicsComponent mzPhysics, float frameTime)
    {
        vertibird.DriftVelocity = Vector2.Zero;

        var next = vertibird.DebugInstantFlight
            ? 0f
            : MathF.Max(0f, mzPhysics.LocalPosition - vertibird.VerticalSpeed * frameTime);
        mzPhysics.LocalPosition = next;
        mzPhysics.Velocity = 0f;
        RemComp<MZFallingComponent>(uid);

        if (next > 0f)
            return;

        vertibird.State = VertibirdFlightState.Grounded;
        vertibird.HeldInputs = VertibirdControlInput.None;
        vertibird.EmergencyLandingActive = false;
        StopFlightLoop(vertibird);
        SendVertibirdEmote(uid, vertibird.LandingEmote);

        if (vertibird.Pilot is { } pilot)
            ResetPilotView(pilot);

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetBodyStatus(uid, physics, BodyStatus.OnGround);

        // Landing is the one point where the craft should become grid-parented
        // again so ordinary ground collision and grid movement semantics resume.
        _transform.SetMapCoordinates(uid, _transform.GetMapCoordinates(uid));
        Dirty(uid, vertibird);
        RemComp<MZFallingComponent>(uid);
        UpdateUi((uid, vertibird));
    }

    private void HoldHover(EntityUid uid, VertibirdComponent vertibird, MZPhysicsComponent mzPhysics)
    {
        mzPhysics.LocalPosition = vertibird.HoverAltitude;
        mzPhysics.Velocity = 0f;
        RemComp<MZFallingComponent>(uid);
    }

    private void UpdateCruising(EntityUid uid, VertibirdComponent vertibird, PhysicsComponent physics, TransformComponent xform, float frameTime)
    {
        // Read the server-authoritative input state sent by the pilot's controls.
        var inputs = vertibird.HeldInputs;

        var rotation = _transform.GetWorldRotation(uid);
        var turn = 0f;

        if ((inputs & VertibirdControlInput.Left) != 0)
            turn += 1f;

        if ((inputs & VertibirdControlInput.Right) != 0)
            turn -= 1f;

        if (turn != 0f)
            rotation += Angle.FromDegrees(vertibird.TurnSpeedDegrees * turn * frameTime);

        var thrust = Vector2.Zero;
        var forward = rotation.ToWorldVec();

        if ((inputs & VertibirdControlInput.Forward) != 0)
            thrust += forward * vertibird.ThrustAcceleration;

        if ((inputs & VertibirdControlInput.Back) != 0)
            thrust -= forward * vertibird.ReverseAcceleration;

        vertibird.DriftVelocity += thrust * frameTime;

        if (thrust == Vector2.Zero)
        {
            var drag = MathF.Max(0f, 1f - vertibird.FlightDrag * frameTime);
            vertibird.DriftVelocity *= drag;
        }

        var speed = vertibird.DriftVelocity.Length();
        if (speed > vertibird.MaxFlightSpeed)
            vertibird.DriftVelocity = vertibird.DriftVelocity / speed * vertibird.MaxFlightSpeed;

        // The vertibird is a dynamic body. Drive its physics velocity instead of
        // teleporting its transform, which the physics step immediately corrects.
        _physics.SetLinearVelocity(uid, vertibird.DriftVelocity, body: physics);
        _physics.SetAngularVelocity(uid, 0f, body: physics);
        _transform.SetWorldRotation(uid, rotation);
    }

    private bool TryMoveZ(Entity<VertibirdComponent> ent, int offset)
    {
        if (ent.Comp.State != VertibirdFlightState.Cruising)
            return false;

        if (!TryComp<MZPhysicsComponent>(ent, out var mzPhysics))
            return false;

        if (!TryComp(ent.Owner, out TransformComponent? xform) ||
            xform.MapUid is not { } mapUid ||
            !TryComp<MZMapComponent>(mapUid, out var currentZMap) ||
            currentZMap.Depth + offset < 0 ||
            !_multiZ.TryMapOffset(mapUid, offset, out var targetMap) ||
            targetMap is not { } resolvedTargetMap ||
            !HasComp<MapComponent>(resolvedTargetMap.Owner))
            return false;

        ent.Comp.State = VertibirdFlightState.ChangingAltitude;
        ent.Comp.AltitudeTransitionFinishedAt = _timing.CurTime +
            TimeSpan.FromSeconds(ent.Comp.AltitudeTransitionDuration);
        ent.Comp.AltitudeTargetMap = resolvedTargetMap.Owner;
        ent.Comp.AltitudeOffset = offset;
        ent.Comp.HeldInputs = VertibirdControlInput.None;
        mzPhysics.Velocity = 0f;
        ent.Comp.DriftVelocity = Vector2.Zero;
        Dirty(ent);
        UpdateUi(ent);
        return true;
    }

    private void UpdateAltitudeTransition(
        EntityUid uid,
        VertibirdComponent vertibird,
        MZPhysicsComponent mzPhysics,
        TransformComponent xform)
    {
        mzPhysics.Velocity = 0f;
        vertibird.DriftVelocity = Vector2.Zero;

        if (!vertibird.DebugInstantFlight && _timing.CurTime < vertibird.AltitudeTransitionFinishedAt)
            return;

        if (vertibird.AltitudeTargetMap is not { } targetMap ||
            !HasComp<MapComponent>(targetMap))
        {
            vertibird.State = VertibirdFlightState.Cruising;
            vertibird.AltitudeTargetMap = null;
            Dirty(uid, vertibird);
            UpdateUi((uid, vertibird));
            return;
        }

        var worldPosition = _transform.GetWorldPosition(uid);
        var altitudeOffset = vertibird.AltitudeOffset;
        _transform.SetCoordinates(uid, new EntityCoordinates(targetMap, worldPosition));
        RefreshOccupantLowerViews(vertibird);
        mzPhysics.LocalPosition = altitudeOffset > 0 ? 0.05f : 0.95f;
        mzPhysics.Velocity = 0f;
        vertibird.State = VertibirdFlightState.Cruising;
        vertibird.AltitudeTargetMap = null;
        vertibird.AltitudeTransitionFinishedAt = TimeSpan.Zero;
        vertibird.AltitudeOffset = 0;
        Dirty(uid, vertibird);
        UpdateUi((uid, vertibird));
        SendVertibirdEmote(uid, altitudeOffset > 0 ? vertibird.ZUpEmote : vertibird.ZDownEmote);
    }

    private void RefreshOccupantLowerViews(VertibirdComponent vertibird)
    {
        var pilotRefreshed = false;
        foreach (var occupant in vertibird.SeatOccupants)
        {
            if (occupant is not { } uid ||
                !TryComp<ActorComponent>(uid, out var actor))
            {
                continue;
            }

            _multiZPvs.RefreshSession(actor.PlayerSession);
            pilotRefreshed |= uid == vertibird.Pilot;
        }

        // Defensive fallback if seat bookkeeping and Pilot briefly disagree during a transfer.
        if (!pilotRefreshed &&
            vertibird.Pilot is { } pilot &&
            TryComp<ActorComponent>(pilot, out var pilotActor))
        {
            _multiZPvs.RefreshSession(pilotActor.PlayerSession);
        }
    }

    private void StartFlightLoop(EntityUid uid, VertibirdComponent vertibird)
    {
        if (vertibird.FlightSoundStream != null || vertibird.FlightLoopSound == null)
            return;

        vertibird.FlightSoundStream = _audio.PlayPvs(vertibird.FlightLoopSound, uid)?.Entity;
    }

    private void StopFlightLoop(VertibirdComponent vertibird)
    {
        vertibird.FlightSoundStream = _audio.Stop(vertibird.FlightSoundStream);
    }

    private static bool ConsumesFuel(VertibirdFlightState state)
    {
        return state is VertibirdFlightState.Starting or
            VertibirdFlightState.TakingOff or
            VertibirdFlightState.Cruising or
            VertibirdFlightState.ChangingAltitude or
            VertibirdFlightState.Landing;
    }

    private bool TryConsumeFuel(EntityUid uid, VertibirdComponent vertibird, float frameTime)
    {
        if (vertibird.DebugInfiniteFuel)
            return true;

        if (!_solution.TryGetSolution(uid, vertibird.FuelSolution, out var fuelEntity, out var fuelSolution))
            return false;

        var available = fuelSolution.GetTotalPrototypeQuantity(vertibird.FuelReagent);
        if (available <= FixedPoint2.Zero)
            return false;

        var accumulated = vertibird.FuelUsePerSecond.Float() * frameTime + vertibird.FuelAccumulator;
        var wholeFuel = FixedPoint2.New(MathF.Floor(accumulated));
        vertibird.FuelAccumulator = accumulated - wholeFuel.Float();

        if (wholeFuel <= FixedPoint2.Zero)
            return true;

        var consumed = FixedPoint2.Min(wholeFuel, available);
        if (!_solution.RemoveReagent(fuelEntity.Value, vertibird.FuelReagent, consumed))
            return false;

        return available > consumed;
    }

    private bool HasMinimumTakeoffFuel(Entity<VertibirdComponent> ent)
    {
        return ent.Comp.DebugInfiniteFuel ||
               TryGetFuel(ent, out var fuel, out _) && fuel >= ent.Comp.MinimumTakeoffFuel;
    }

    private bool TryGetFuel(Entity<VertibirdComponent> ent, out FixedPoint2 fuel, out FixedPoint2 capacity)
    {
        fuel = FixedPoint2.Zero;
        capacity = FixedPoint2.Zero;

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.FuelSolution, out _, out var solution))
            return false;

        fuel = solution.GetTotalPrototypeQuantity(ent.Comp.FuelReagent);
        capacity = solution.MaxVolume;
        return true;
    }

    private void HandleFuelEmergency(Entity<VertibirdComponent> ent, TransformComponent xform)
    {
        if (!ent.Comp.FuelEmergencyActive)
        {
            ent.Comp.FuelEmergencyActive = true;
            SendVertibirdEmote(ent.Owner, ent.Comp.FuelEmergencyEmote);
            Dirty(ent);
        }

        ent.Comp.EmergencyLandingActive = true;

        switch (ent.Comp.State)
        {
            case VertibirdFlightState.Starting:
                CancelStartup(ent);
                return;
            case VertibirdFlightState.ChangingAltitude:
            case VertibirdFlightState.Landing:
                return;
        }

        HandleEmergencyLanding(ent, xform);
    }

    private void HandleEmergencyLanding(Entity<VertibirdComponent> ent, TransformComponent xform)
    {
        ent.Comp.HeldInputs = VertibirdControlInput.None;
        ent.Comp.DriftVelocity = Vector2.Zero;

        if (ent.Comp.State is VertibirdFlightState.ChangingAltitude or VertibirdFlightState.Landing)
            return;

        if (xform.MapUid is { } mapUid &&
            TryComp<MZMapComponent>(mapUid, out var zMap) &&
            zMap.Depth > 0 &&
            ent.Comp.State == VertibirdFlightState.Cruising &&
            TryMoveZ(ent, -1))
        {
            return;
        }

        // Emergency descent deliberately bypasses normal landing clearance.
        // With no pilot or fuel, remaining airborne forever is the worse state.
        if (ent.Comp.State is VertibirdFlightState.TakingOff or VertibirdFlightState.Cruising)
            StartLanding(ent);
    }

    private void UpdateFuelWarnings(Entity<VertibirdComponent> ent)
    {
        // A craft that cannot run out has nothing to warn about, even on an empty tank.
        if (ent.Comp.DebugInfiniteFuel)
            return;

        if (!TryGetFuel(ent, out var fuel, out var capacity) || capacity <= FixedPoint2.Zero)
            return;

        var fraction = fuel.Float() / capacity.Float();
        if (fraction > ent.Comp.LowFuelWarningFraction)
        {
            ent.Comp.LowFuelWarningIssued = false;
            ent.Comp.CriticalFuelWarningIssued = false;
            return;
        }

        if (!ConsumesFuel(ent.Comp.State))
            return;

        if (fraction <= ent.Comp.CriticalFuelWarningFraction && !ent.Comp.CriticalFuelWarningIssued)
        {
            ent.Comp.LowFuelWarningIssued = true;
            ent.Comp.CriticalFuelWarningIssued = true;
            WarnPilot(ent, "vertibird-fuel-warning-critical");
            return;
        }

        if (!ent.Comp.LowFuelWarningIssued)
        {
            ent.Comp.LowFuelWarningIssued = true;
            WarnPilot(ent, "vertibird-fuel-warning-low");
        }
    }

    private void WarnPilot(Entity<VertibirdComponent> ent, string message)
    {
        if (ent.Comp.Pilot is { } pilot && Exists(pilot))
            _popup.PopupEntity(Loc.GetString(message), ent, pilot, PopupType.LargeCaution);
    }

    private bool CanLandHere(Entity<VertibirdComponent> ent, out string failureMessage)
    {
        failureMessage = "vertibird-landing-blocked";

        if (!TryComp(ent.Owner, out TransformComponent? xform) ||
            !TryComp(ent.Owner, out PhysicsComponent? physics))
            return false;

        var worldPosition = _transform.GetWorldPosition(ent.Owner);
        var worldRotation = _transform.GetWorldRotation(ent.Owner);

        foreach (var localOffset in LandingFootprintSamples)
        {
            var samplePosition = worldPosition + worldRotation.RotateVec(localOffset);
            var sampleCoordinates = new MapCoordinates(samplePosition, xform.MapID);

            if (!_map.TryFindGridAt(sampleCoordinates, out var gridUid, out var grid))
            {
                failureMessage = "vertibird-landing-no-ground";
                return false;
            }

            var tile = _map.WorldToTile(gridUid, grid, samplePosition);
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            {
                failureMessage = "vertibird-landing-no-ground";
                return false;
            }

            if (TryComp<RoofComponent>(gridUid, out var roof) && _roof.IsRooved((gridUid, grid, roof), tile))
            {
                failureMessage = "vertibird-landing-roofed";
                return false;
            }

            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
            while (anchored.MoveNext(out var anchoredEntity))
            {
                if (!HasComp<BlockWeatherComponent>(anchoredEntity.Value))
                    continue;

                failureMessage = "vertibird-landing-roofed";
                return false;
            }

            if (!_anchorable.TileFree(gridUid, grid, tile, physics.CollisionLayer, physics.CollisionMask))
                return false;
        }

        return true;
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        ResetPilotView(args.Entity);

        var query = EntityQueryEnumerator<VertibirdComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vertibird, out var xform))
        {
            if (vertibird.Pilot != args.Entity)
                continue;

            HandlePilotLost(uid, vertibird, xform);
            return;
        }
    }

    /// <summary>
    /// Puts a craft that has lost its pilot into an automatic descent, whether they
    /// disconnected or stepped out in flight. Boarding requires the craft to be
    /// grounded, so without this an abandoned craft would hover unreachable forever.
    /// </summary>
    private void HandlePilotLost(EntityUid uid, VertibirdComponent vertibird, TransformComponent xform)
    {
        // Admin debug: hold altitude through the pilot's player leaving the body.
        if (vertibird.DebugIgnorePilotLoss)
            return;

        if (vertibird.State == VertibirdFlightState.Starting)
        {
            CancelStartup((uid, vertibird));
            return;
        }

        if (vertibird.State is VertibirdFlightState.Grounded or VertibirdFlightState.Landing)
            return;

        if (!vertibird.EmergencyLandingActive)
            SendVertibirdEmote(uid, vertibird.PilotDisconnectedEmote);

        vertibird.EmergencyLandingActive = true;
        vertibird.HeldInputs = VertibirdControlInput.None;
        vertibird.DriftVelocity = Vector2.Zero;
        Dirty(uid, vertibird);
        HandleEmergencyLanding((uid, vertibird), xform);
    }

    private void UpdateFuelUi(Entity<VertibirdComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextFuelUiUpdate)
            return;

        ent.Comp.NextFuelUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        UpdateUi(ent);
    }

    private void OnFuelTransferAttempt(Entity<VertibirdComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner)
            return;

        if (ent.Comp.State != VertibirdFlightState.Grounded)
        {
            args.Cancel(Loc.GetString("vertibird-fuel-refuel-running"));
            return;
        }

        if (!_solution.TryGetDrainableSolution(args.From, out _, out var source))
            return;

        var fuel = source.GetTotalPrototypeQuantity(ent.Comp.FuelReagent);
        if (fuel != source.Volume)
            args.Cancel(Loc.GetString("vertibird-fuel-contaminated"));
    }

    private void OnShutdown(Entity<VertibirdComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.StartupSoundStream = _audio.Stop(ent.Comp.StartupSoundStream);
        StopFlightLoop(ent.Comp);

        foreach (var occupant in ent.Comp.SeatOccupants)
        {
            if (occupant != null)
                UnhideOccupant(occupant.Value);
        }
    }

    private void OnAfterUiOpen(Entity<VertibirdComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSelectSeat(Entity<VertibirdComponent> ent, ref VertibirdSelectSeatMessage args)
    {
        var user = args.Actor;
        var seatIndex = args.SeatIndex;

        if (!IsValidSeat(ent.Comp, seatIndex))
            return;

        // #Misfits Change - boarding is no longer gated on the craft being grounded.
        // Reach is the real gate: the buckle and do-after range checks already stop
        // anyone climbing aboard a craft they cannot physically get to.
        if (ent.Comp.SeatOccupants[seatIndex] != null)
            return;

        if (seatIndex == 0 && !HasComp<VertibirdPilotPerkComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("vertibird-pilot-required"), ent, user);
            return;
        }

        var currentSeat = GetSeatIndex(ent.Comp, user);
        if (currentSeat != null)
        {
            ent.Comp.SeatOccupants[currentSeat.Value] = null;
            ent.Comp.SeatOccupants[seatIndex] = user;

            if (currentSeat.Value == 0)
            {
                RemovePilotAction(user, ent.Comp);
                ent.Comp.Pilot = null;
            }

            if (seatIndex == 0)
            {
                ent.Comp.Pilot = user;
                AddPilotActions(user, ent);
            }

            // Swapping seats never unbuckles, so the turret has to be handed over here.
            RefreshTurretSeat(ent, currentSeat.Value, null);
            RefreshTurretSeat(ent, seatIndex, user);

            Dirty(ent);
            UpdateUi(ent);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            BoardingDuration,
            new VertibirdBoardDoAfterEvent(seatIndex),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);

        UpdateUi(ent);
    }

    private void OnBoardDoAfter(Entity<VertibirdComponent> ent, ref VertibirdBoardDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target != ent.Owner)
            return;

        args.Handled = true;
        var user = args.User;
        var seatIndex = args.SeatIndex;

        if (!IsValidSeat(ent.Comp, seatIndex) ||
            ent.Comp.SeatOccupants[seatIndex] != null ||
            GetSeatIndex(ent.Comp, user) != null ||
            seatIndex == 0 && !HasComp<VertibirdPilotPerkComponent>(user))
        {
            UpdateUi(ent);
            return;
        }

        _pendingSeatSelections[user] = seatIndex;
        if (!_buckle.TryBuckle(user, user, ent.Owner))
            _pendingSeatSelections.Remove(user);

        UpdateUi(ent);
    }

    private void HideOccupant(EntityUid occupant)
    {
        if (HasComp<VertibirdHiddenOccupantComponent>(occupant))
            return;

        var hidden = EnsureComp<VertibirdHiddenOccupantComponent>(occupant);
        hidden.HadStealth = TryComp<StealthComponent>(occupant, out var stealth);
        hidden.PreviousVisibility = hidden.HadStealth && stealth != null
            ? _stealth.GetVisibility(occupant, stealth)
            : 1f;

        stealth ??= EnsureComp<StealthComponent>(occupant);
        _stealth.SetVisibility(occupant, -1f, stealth);
    }

    private void UnhideOccupant(EntityUid occupant)
    {
        if (!TryComp<VertibirdHiddenOccupantComponent>(occupant, out var hidden))
            return;

        if (hidden.HadStealth)
        {
            if (TryComp<StealthComponent>(occupant, out var stealth))
                _stealth.SetVisibility(occupant, hidden.PreviousVisibility, stealth);
        }
        else
        {
            RemComp<StealthComponent>(occupant);
        }

        RemComp<VertibirdHiddenOccupantComponent>(occupant);
    }

    private static bool IsValidSeat(VertibirdComponent vertibird, int seatIndex)
    {
        return seatIndex >= 0 && seatIndex < vertibird.SeatOccupants.Length;
    }

    private static int? GetSeatIndex(VertibirdComponent vertibird, EntityUid occupant)
    {
        for (var i = 0; i < vertibird.SeatOccupants.Length; i++)
        {
            if (vertibird.SeatOccupants[i] == occupant)
                return i;
        }

        return null;
    }

    private void UpdateUi(Entity<VertibirdComponent> ent)
    {
        _ui.SetUiState(ent.Owner, VertibirdUiKey.Key, BuildUiState(ent));
    }

    private VertibirdSeatBoundUserInterfaceState BuildUiState(Entity<VertibirdComponent> ent)
    {
        var vertibird = ent.Comp;
        var seats = new VertibirdSeatUiState[vertibird.SeatOccupants.Length];
        for (var i = 0; i < seats.Length; i++)
        {
            var occupant = vertibird.SeatOccupants[i];
            var seatName = i switch
            {
                0 => Loc.GetString("vertibird-seat-pilot"),
                1 => Loc.GetString("vertibird-seat-crew-chief"),
                _ => Loc.GetString("vertibird-seat-passenger", ("number", i - 1)),
            };

            seats[i] = new VertibirdSeatUiState(
                i,
                seatName,
                occupant == null ? null : Identity.Name(occupant.Value, EntityManager),
                i == 0);
        }

        TryGetFuel(ent, out var fuel, out var capacity);
        var maxIntegrity = 1f;
        var integrity = 1f;
        if (TryComp<AircraftImpactDamageComponent>(ent, out var impact))
        {
            maxIntegrity = MathF.Max(impact.MaxIntegrity, 1f);
            var structuralDamage = 0f;
            if (TryComp<DamageableComponent>(ent, out var damageable) &&
                damageable.Damage.DamageDict.TryGetValue("Structural", out var damage))
            {
                structuralDamage = damage.Float();
            }

            integrity = MathF.Max(0f, maxIntegrity - structuralDamage);
        }

        var altitude = 0;
        if (TryComp<TransformComponent>(ent, out var xform) &&
            xform.MapUid is { } mapUid &&
            TryComp<MZMapComponent>(mapUid, out var zMap))
        {
            altitude = zMap.Depth;
        }

        var stored = _sharedVertibird.GetCargo(ent);
        var cargo = new VertibirdCargoUiState[stored.Count];
        for (var i = 0; i < cargo.Length; i++)
        {
            cargo[i] = new VertibirdCargoUiState(GetNetEntity(stored[i]), Name(stored[i]));
        }

        return new VertibirdSeatBoundUserInterfaceState(
            Loc.GetString(vertibird.WindowTitleLocId),
            vertibird.State,
            altitude,
            fuel.Float(),
            capacity.Float(),
            integrity,
            maxIntegrity,
            seats,
            cargo,
            vertibird.CargoCapacity);
    }

    private void SendVertibirdEmote(EntityUid vertibird, string locId)
    {
        _chat.TrySendInGameICMessage(
            vertibird,
            Loc.GetString(locId),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
    }

    private void RemovePilotRelay(EntityUid pilot, EntityUid vertibird)
    {
        if (TryComp<VertibirdComponent>(vertibird, out var component))
            component.HeldInputs = VertibirdControlInput.None;

        ResetPilotView(pilot);
    }

    private void OnControlInput(VertibirdControlInputMessage message, EntitySessionEventArgs session)
    {
        if (message.Input is not (VertibirdControlInput.Forward or VertibirdControlInput.Back or
            VertibirdControlInput.Left or VertibirdControlInput.Right))
        {
            return;
        }

        if (session.SenderSession.AttachedEntity is not { } pilot)
            return;

        var query = EntityQueryEnumerator<VertibirdComponent>();
        while (query.MoveNext(out var uid, out var vertibird))
        {
            if (vertibird.Pilot != pilot || vertibird.State != VertibirdFlightState.Cruising)
                continue;

            if (message.Pressed)
                vertibird.HeldInputs |= message.Input;
            else
                vertibird.HeldInputs &= ~message.Input;

            return;
        }
    }

    private void OnCameraOffset(VertibirdCameraOffsetMessage message, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } pilot ||
            !float.IsFinite(message.Offset.X) ||
            !float.IsFinite(message.Offset.Y))
        {
            return;
        }

        var activePilot = false;
        var query = EntityQueryEnumerator<VertibirdComponent>();
        while (query.MoveNext(out _, out var vertibird))
        {
            if (vertibird.Pilot == pilot && vertibird.State != VertibirdFlightState.Grounded)
            {
                activePilot = true;
                break;
            }
        }

        if (!activePilot)
        {
            ResetPilotView(pilot);
            return;
        }

        if (!TryComp<EyeComponent>(pilot, out var eye))
            return;

        var offset = message.Offset;
        var length = offset.Length();
        if (length > PilotCameraMaxOffset)
            offset = offset / length * PilotCameraMaxOffset;

        _eye.SetOffset(pilot, offset, eye);
        _eye.SetPvsScale((pilot, eye), PilotCameraPvsScale);
    }

    private void ResetPilotView(EntityUid pilot)
    {
        if (!TryComp<EyeComponent>(pilot, out var eye))
            return;

        _eye.SetOffset(pilot, Vector2.Zero, eye);
        _eye.SetPvsScale((pilot, eye), 1f);
    }

    private void AddPilotActions(EntityUid pilot, Entity<VertibirdComponent> ent)
    {
        _actions.AddAction(pilot, ref ent.Comp.FlightActionEntity, ent.Comp.FlightAction, ent.Owner);
        _actions.AddAction(pilot, ref ent.Comp.LandActionEntity, ent.Comp.LandAction, ent.Owner);
        _actions.AddAction(pilot, ref ent.Comp.MoveUpActionEntity, ent.Comp.MoveUpAction, ent.Owner);
        _actions.AddAction(pilot, ref ent.Comp.MoveDownActionEntity, ent.Comp.MoveDownAction, ent.Owner);
    }

    private void RemovePilotAction(EntityUid pilot, VertibirdComponent vertibird)
    {
        _actions.RemoveAction(pilot, vertibird.FlightActionEntity);
        _actions.RemoveAction(pilot, vertibird.LandActionEntity);
        _actions.RemoveAction(pilot, vertibird.MoveUpActionEntity);
        _actions.RemoveAction(pilot, vertibird.MoveDownActionEntity);
        vertibird.FlightActionEntity = null;
        vertibird.LandActionEntity = null;
        vertibird.MoveUpActionEntity = null;
        vertibird.MoveDownActionEntity = null;
    }

}
