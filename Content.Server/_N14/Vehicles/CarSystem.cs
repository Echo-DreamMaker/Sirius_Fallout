using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._N14.Vehicles;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Vehicles;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._N14.Vehicles;

/// <summary>
/// #Sirius: Car/ground-vehicle server behavior split from MechSystem.
/// Handles ignition keys, engine running, passengers, and car-specific interactions for entities with CarComponent.
/// </summary>
public sealed class CarSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!; //Sirius Fix
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CarComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<CarComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<CarComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CarComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<CarComponent, RemoveBatteryEvent>(OnRemoveBattery);
        SubscribeLocalEvent<CarComponent, RemoveIgnitionKeyEvent>(OnRemoveIgnitionKey);
        SubscribeLocalEvent<CarComponent, MechEntryEvent>(OnMechEntry);
        SubscribeLocalEvent<CarComponent, MechExitEvent>(OnMechExit);
        SubscribeLocalEvent<CarComponent, MechPassengerEntryEvent>(OnMechPassengerEntry);
        SubscribeLocalEvent<CarComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CarComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<CarComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CarComponent, MechEjectPassenger1Event>(OnEjectPassenger1);
        SubscribeLocalEvent<CarComponent, MechEjectPassenger2Event>(OnEjectPassenger2);
        SubscribeLocalEvent<CarComponent, MechEjectPassenger3Event>(OnEjectPassenger3);
        SubscribeLocalEvent<CarComponent, HornActionEvent>(OnHornAction);
        SubscribeLocalEvent<CarComponent, SirenActionEvent>(OnSirenAction);
    }

    private void OnCanMove(EntityUid uid, CarComponent car, UpdateCanMoveEvent args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        //Sirius Fix - engine stops if key/battery conditions are no longer met
        if (mech.EngineRunning && !CanEngineRun(mech))
        {
            mech.EngineRunning = false;
            Dirty(uid, mech);
            UpdateEngineRunning(uid, mech);
        }

        if (mech.Broken || mech.Integrity <= 0 || mech.Energy <= 0 || !mech.EngineRunning)
            args.Cancel();
    }

    private bool CanEngineRun(MechComponent mech)
    {
        return !mech.Broken && mech.Integrity > 0 && mech.Energy > 0 && mech.IgnitionSlot.ContainedEntity != null;
    }

    private void TryUpdateEngineState(EntityUid uid, MechComponent mech)
    {
        var shouldRun = CanEngineRun(mech);
        if (mech.EngineRunning == shouldRun)
            return;

        mech.EngineRunning = shouldRun;
        Dirty(uid, mech);
        UpdateEngineRunning(uid, mech);
    }

    private void UpdateEngineRunning(EntityUid uid, MechComponent mech)
    {
        if (mech.PilotSlot.ContainedEntity != null)
        {
            if (mech.EngineRunning)
                _mover.SetRelay(mech.PilotSlot.ContainedEntity.Value, uid);
            else
                RemComp<RelayInputMoverComponent>(mech.PilotSlot.ContainedEntity.Value);
        }

        // Keep ambient engine sound in sync with engine state.
        if (TryComp<AmbientSoundComponent>(uid, out var ambient))
            _ambient.SetAmbience(uid, mech.EngineRunning, ambient);

        _actionBlocker.UpdateCanMove(uid);
    }

    private bool CanInsertKey(EntityUid user, MechComponent mech)
    {
        //Sirius Fix - only pilot or passenger in slot 1 can insert the ignition key
        return mech.PilotSlot.ContainedEntity == user || mech.PassengerSlot1.ContainedEntity == user;
    }

    private bool CanExtractKey(EntityUid user, MechComponent mech)
    {
        var pilot = mech.PilotSlot.ContainedEntity;
        var passenger1 = mech.PassengerSlot1.ContainedEntity;

        //Sirius Fix - extraction rules depend on occupancy of pilot/passenger-1 slots
        if (pilot != null)
            return pilot == user;
        if (passenger1 != null)
            return passenger1 == user;
        return true; // both empty -> anyone can extract (e.g. from outside)
    }

    private void TryExtractKey(EntityUid uid, MechComponent mech, EntityUid user)
    {
        if (mech.IgnitionSlot.ContainedEntity is not { } key)
            return;
        if (!CanExtractKey(user, mech))
            return;

        if (_hands.TryPickupAnyHand(user, key))
            _popup.PopupEntity(Loc.GetString("mech-key-removed"), uid, user);
        else
        {
            Transform(key).Coordinates = Transform(uid).Coordinates;
            _popup.PopupEntity(Loc.GetString("mech-key-dropped"), uid, user);
        }

        _container.Remove(key, mech.IgnitionSlot);
    }

    private void OnInteractUsing(EntityUid uid, CarComponent car, InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<MechComponent>(uid, out var mech))
            return;

        //Sirius Fix - ignition key can be inserted by pilot or passenger-1 to start the engine
        if (HasComp<IgnitionKeyComponent>(args.Used))
        {
            if (mech.IgnitionSlot.ContainedEntity != null)
            {
                _popup.PopupEntity(Loc.GetString("mech-key-slot-occupied"), uid, args.User);
                args.Handled = true;
                return;
            }

            if (!CanInsertKey(args.User, mech))
                return;

            _container.Insert(args.Used, mech.IgnitionSlot);
            TryUpdateEngineState(uid, mech);
            args.Handled = true;
            return;
        }

        if (TryComp<WiresPanelComponent>(uid, out var panel) && !panel.Open)
            return;

        if (mech.BatterySlot.ContainedEntity == null && TryComp<BatteryComponent>(args.Used, out var battery))
        {
            InsertBattery(uid, args.Used, mech, battery);
            TryUpdateEngineState(uid, mech);
            args.Handled = true;
            return;
        }

        if (_toolSystem.HasQuality(args.Used, "Prying") && mech.BatterySlot.ContainedEntity != null)
        {
            if (TryComp<WiresPanelComponent>(uid, out var panelCheck) && !panelCheck.Open)
                return;

            var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, mech.BatteryRemovalDelay,
                new RemoveBatteryEvent(), uid, target: uid, used: args.Target)
            {
                BreakOnMove = true
            };
            _doAfter.TryStartDoAfter(doAfterEventArgs);
            args.Handled = true;
        }
    }

    private void OnContainerInserted(EntityUid uid, CarComponent car, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        if (args.Container == mech.BatterySlot && TryComp<BatteryComponent>(args.Entity, out var battery))
        {
            mech.Energy = battery.CurrentCharge;
            mech.MaxEnergy = battery.MaxCharge;
            Dirty(uid, mech);
            _actionBlocker.UpdateCanMove(uid);
            _mech.UpdateUserInterface(uid, mech);
            TryUpdateEngineState(uid, mech);
            return;
        }

        if (args.Container == mech.IgnitionSlot)
        {
            //Sirius Fix - engine starts when key is inserted (battery+key required)
            TryUpdateEngineState(uid, mech);
            return;
        }

        if (args.Container == mech.PassengerSlot1 || args.Container == mech.PassengerSlot2 || args.Container == mech.PassengerSlot3)
            RefreshCarActions(uid, mech);
    }

    private void OnContainerRemoved(EntityUid uid, CarComponent car, EntRemovedFromContainerMessage args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        if (args.Container == mech.IgnitionSlot)
        {
            mech.EngineRunning = false;
            Dirty(uid, mech);
            UpdateEngineRunning(uid, mech);
            return;
        }

        if (args.Container == mech.BatterySlot)
        {
            mech.Energy = 0;
            mech.MaxEnergy = 0;
            Dirty(uid, mech);
            _mech.UpdateUserInterface(uid, mech);
            TryUpdateEngineState(uid, mech);
            return;
        }

        if (args.Container == mech.PassengerSlot1 || args.Container == mech.PassengerSlot2 || args.Container == mech.PassengerSlot3)
            RefreshCarActions(uid, mech);
    }

    private void OnRemoveIgnitionKey(EntityUid uid, CarComponent car, RemoveIgnitionKeyEvent args)
    {
        //Sirius Fix - no doafter-based ignition extraction is started currently; kept for compatibility.
        if (args.Cancelled || args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;
        if (mech.IgnitionSlot.ContainedEntity == null)
            return;

        _container.EmptyContainer(mech.IgnitionSlot);
        args.Handled = true;
    }

    private void InsertBattery(EntityUid uid, EntityUid toInsert, MechComponent mech, BatteryComponent battery)
    {
        _container.Insert(toInsert, mech.BatterySlot);
        mech.Energy = battery.CurrentCharge;
        mech.MaxEnergy = battery.MaxCharge;
        _actionBlocker.UpdateCanMove(uid);
        Dirty(uid, mech);
        _mech.UpdateUserInterface(uid, mech);
    }

    private void OnRemoveBattery(EntityUid uid, CarComponent car, RemoveBatteryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        _container.EmptyContainer(mech.BatterySlot);
        mech.Energy = 0;
        mech.MaxEnergy = 0;
        _actionBlocker.UpdateCanMove(uid);
        Dirty(uid, mech);
        _mech.UpdateUserInterface(uid, mech);
        TryUpdateEngineState(uid, mech);
        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, CarComponent car, MapInitEvent args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        var xform = Transform(uid);
        foreach (var equipment in mech.StartingEquipment)
        {
            var ent = Spawn(equipment, xform.Coordinates);
            _mech.InsertEquipment(uid, ent, mech);
        }

        mech.Integrity = mech.MaxIntegrity;
        mech.Energy = mech.MaxEnergy;
        mech.EngineRunning = false; //Sirius Fix - engine starts only with key+power

        // #Misfits Merge: MechAirComponent not present in this merge; airtight air handled by mech/car systems as applicable.

        _actionBlocker.UpdateCanMove(uid);
        Dirty(uid, mech);
    }

    private void OnAlternativeVerb(EntityUid uid, CarComponent car, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;
        if (!args.CanAccess || !args.CanInteract || mech.Broken)
            return;

        if (_mech.CanInsert(uid, args.User, mech))
        {
            var enterVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-enter"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, mech.EntryDelay, new MechEntryEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(enterVerb);
        }
        else if (!_mech.IsEmpty(mech) && _mech.CanInsertPassenger(uid, args.User, mech))
        {
            var passengerVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-enter-passenger"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, mech.EntryDelay, new MechPassengerEntryEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(passengerVerb);
        }

        if (mech.IgnitionSlot.ContainedEntity != null && CanExtractKey(args.User, mech))
        {
            //Sirius Fix - extract ignition key via context menu
            var extractKeyVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-key-extract-verb"),
                Act = () => TryExtractKey(uid, mech, args.User),
                Priority = 2
            };
            args.Verbs.Add(extractKeyVerb);
        }

        var openUiVerb = new AlternativeVerb
        {
            Act = () => ToggleUi(uid, mech, args.User),
            Text = Loc.GetString("mech-ui-open-verb")
        };
        args.Verbs.Add(openUiVerb);

        if (!_mech.IsEmpty(mech) || !IsPassengerEmpty(mech))
        {
            var ejectVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-exit"),
                Priority = 1,
                Act = () =>
                {
                    if (mech.PilotSlot.ContainedEntity == args.User)
                    {
                        _mech.TryEject(uid, mech);
                        _actionBlocker.UpdateCanMove(uid);
                        return;
                    }
                    if (IsPassenger(args.User, mech))
                    {
                        _mech.TryEjectPassenger(uid, args.User, mech);
                        return;
                    }
                    if (args.User == uid)
                        return;

                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, mech.ExitDelay,
                        new MechExitEvent(), uid, target: uid);
                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(ejectVerb);
        }
    }

    private void ToggleUi(EntityUid uid, MechComponent mech, EntityUid? user)
    {
        user ??= mech.PilotSlot.ContainedEntity;
        if (user == null)
            return;
        if (!TryComp<ActorComponent>(user, out var actor))
            return;
        _ui.TryToggleUi(uid, MechUiKey.Key, actor.PlayerSession);
        _mech.UpdateUserInterface(uid, mech);
    }

    private static bool IsPassengerEmpty(MechComponent mech)
    {
        return mech.PassengerSlot1.ContainedEntity == null &&
               mech.PassengerSlot2.ContainedEntity == null &&
               mech.PassengerSlot3.ContainedEntity == null;
    }

    private static bool IsPassenger(EntityUid uid, MechComponent mech)
    {
        return mech.PassengerSlot1.ContainedEntity == uid ||
               mech.PassengerSlot2.ContainedEntity == uid ||
               mech.PassengerSlot3.ContainedEntity == uid;
    }

    private void OnMechEntry(EntityUid uid, CarComponent car, MechEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        if (_whitelistSystem.IsWhitelistFail(mech.PilotWhitelist, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), args.User);
            return;
        }

        if (!_mech.TryInsert(uid, args.Args.User, mech))
        {
            _popup.PopupEntity("Cannot enter!", args.User);
            return;
        }

        //Sirius Fix - boarding pilot does not start engine; key+power does. Refresh actions for pilot.
        TryUpdateEngineState(uid, mech);
        RefreshCarActions(uid, mech);
        args.Handled = true;
    }

    private void OnMechPassengerEntry(EntityUid uid, CarComponent car, MechPassengerEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        if (!_mech.TryInsertPassenger(uid, args.User, mech))
        {
            _popup.PopupEntity("Cannot enter as passenger!", args.User);
            return;
        }

        RefreshCarActions(uid, mech); //Sirius Fix - eject-passenger actions depend on occupants
        _popup.PopupEntity("You enter as passenger.", args.User);
        args.Handled = true;
    }

    private void OnMechExit(EntityUid uid, CarComponent car, MechExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        if (mech.PilotSlot.ContainedEntity == args.User)
        {
            _mech.TryEject(uid, mech);
        }
        else if (IsPassenger(args.User, mech))
        {
            _mech.TryEjectPassenger(uid, args.User, mech);
        }
        else
        {
            if (mech.PassengerSlot1.ContainedEntity != null)
                _mech.TryEjectPassenger(uid, mech.PassengerSlot1.ContainedEntity.Value, mech);
            else if (mech.PassengerSlot2.ContainedEntity != null)
                _mech.TryEjectPassenger(uid, mech.PassengerSlot2.ContainedEntity.Value, mech);
            else if (mech.PassengerSlot3.ContainedEntity != null)
                _mech.TryEjectPassenger(uid, mech.PassengerSlot3.ContainedEntity.Value, mech);
            else if (mech.PilotSlot.ContainedEntity != null)
                _mech.TryEject(uid, mech);
        }

        TryUpdateEngineState(uid, mech);
        RefreshCarActions(uid, mech);
        args.Handled = true;
    }

    private void OnDamageChanged(EntityUid uid, CarComponent car, DamageChangedEvent args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;
        if (args.DamageIncreased && args.DamageDelta != null && mech.PilotSlot.ContainedEntity != null)
        {
            var damage = args.DamageDelta * mech.MechToPilotDamageMultiplier;
            _damageable.TryChangeDamage(mech.PilotSlot.ContainedEntity, damage);
        }
    }

    private void OnHornAction(EntityUid uid, CarComponent car, HornActionEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;
        if (mech.PilotSlot.ContainedEntity != args.Performer)
            return;
        if (mech.HornSound == null)
            return;
        _audio.PlayPvs(mech.HornSound, uid);
        args.Handled = true;
    }

    private void OnSirenAction(EntityUid uid, CarComponent car, SirenActionEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;
        if (mech.PilotSlot.ContainedEntity != args.Performer)
            return;
        if (mech.SirenSound == null)
            return;
        if (mech.SirenEnabled)
        {
            mech.SirenStream = _audio.Stop(mech.SirenStream);
        }
        else
        {
            mech.SirenStream = _audio.PlayPvs(mech.SirenSound, uid)?.Entity;
        }
        mech.SirenEnabled = !mech.SirenEnabled;
        Dirty(uid, mech);
        args.Handled = true;
    }

    private void OnActivate(EntityUid uid, CarComponent car, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        //Sirius Fix - key extraction rules:
        // pilot occupied -> only pilot; pilot empty & passenger-1 present -> only passenger-1; both empty -> anyone
        if (mech.IgnitionSlot.ContainedEntity == null)
            return;

        if (!CanExtractKey(args.User, mech))
            return;

        TryExtractKey(uid, mech, args.User);
        args.Handled = true;
    }

    private void OnEjectPassenger1(EntityUid uid, CarComponent car, MechEjectPassenger1Event args)
    {
        if (args.Handled) return;
        if (!TryComp<MechComponent>(uid, out var mech)) return;
        if (mech.PilotSlot.ContainedEntity != args.Performer) return;
        args.Handled = true;
        if (mech.PassengerSlot1.ContainedEntity is { } p)
            _mech.TryEjectPassenger(uid, p, mech);
        else
            _popup.PopupEntity(Loc.GetString("mech-no-passenger-in-slot", ("slot", 1)), uid, args.Performer);
        RefreshCarActions(uid, mech);
    }

    private void OnEjectPassenger2(EntityUid uid, CarComponent car, MechEjectPassenger2Event args)
    {
        if (args.Handled) return;
        if (!TryComp<MechComponent>(uid, out var mech)) return;
        if (mech.PilotSlot.ContainedEntity != args.Performer) return;
        args.Handled = true;
        if (mech.PassengerSlot2.ContainedEntity is { } p)
            _mech.TryEjectPassenger(uid, p, mech);
        else
            _popup.PopupEntity(Loc.GetString("mech-no-passenger-in-slot", ("slot", 2)), uid, args.Performer);
        RefreshCarActions(uid, mech);
    }

    private void OnEjectPassenger3(EntityUid uid, CarComponent car, MechEjectPassenger3Event args)
    {
        if (args.Handled) return;
        if (!TryComp<MechComponent>(uid, out var mech)) return;
        if (mech.PilotSlot.ContainedEntity != args.Performer) return;
        args.Handled = true;
        if (mech.PassengerSlot3.ContainedEntity is { } p)
            _mech.TryEjectPassenger(uid, p, mech);
        else
            _popup.PopupEntity(Loc.GetString("mech-no-passenger-in-slot", ("slot", 3)), uid, args.Performer);
        RefreshCarActions(uid, mech);
    }

    /// <summary>
    /// Sirius Fix - grant/remove car actions on pilot dynamically based on available features and occupants.
    /// </summary>
    private void RefreshCarActions(EntityUid uid, MechComponent mech)
    {
        if (_net.IsClient)
            return;
        if (mech.PilotSlot.ContainedEntity is not { } pilot)
            return;

        // Core actions always available to pilot
        _actions.AddAction(pilot, ref mech.MechCycleActionEntity, mech.MechCycleAction, uid);
        _actions.AddAction(pilot, ref mech.MechUiActionEntity, mech.MechUiAction, uid);
        _actions.AddAction(pilot, ref mech.MechEjectActionEntity, mech.MechEjectAction, uid);

        // Flashlight toggle only if the vehicle has a light
        if (HasComp<PointLightComponent>(uid) || HasComp<UnpoweredFlashlightComponent>(uid))
            _actions.AddAction(pilot, ref mech.ToggleActionEntity, mech.ToggleAction, uid);
        else if (mech.ToggleActionEntity != null)
            _actions.RemoveAction(pilot, mech.ToggleActionEntity);

        // Horn only if sound configured
        if (mech.HornSound != null)
            _actions.AddAction(pilot, ref mech.MechHornActionEntity, mech.MechHornAction, uid);
        else if (mech.MechHornActionEntity != null)
            _actions.RemoveAction(pilot, mech.MechHornActionEntity);

        // Siren only if sound configured
        if (mech.SirenSound != null)
            _actions.AddAction(pilot, ref mech.MechSirenActionEntity, mech.MechSirenAction, uid);
        else if (mech.MechSirenActionEntity != null)
            _actions.RemoveAction(pilot, mech.MechSirenActionEntity);

        // Eject passenger actions only for occupied slots
        if (mech.PassengerSlot1.ContainedEntity != null)
            _actions.AddAction(pilot, ref mech.MechEjectPassenger1ActionEntity, mech.MechEjectPassenger1Action, uid);
        else if (mech.MechEjectPassenger1ActionEntity != null)
            _actions.RemoveAction(pilot, mech.MechEjectPassenger1ActionEntity);

        if (mech.PassengerSlot2.ContainedEntity != null)
            _actions.AddAction(pilot, ref mech.MechEjectPassenger2ActionEntity, mech.MechEjectPassenger2Action, uid);
        else if (mech.MechEjectPassenger2ActionEntity != null)
            _actions.RemoveAction(pilot, mech.MechEjectPassenger2ActionEntity);

        if (mech.PassengerSlot3.ContainedEntity != null)
            _actions.AddAction(pilot, ref mech.MechEjectPassenger3ActionEntity, mech.MechEjectPassenger3Action, uid);
        else if (mech.MechEjectPassenger3ActionEntity != null)
            _actions.RemoveAction(pilot, mech.MechEjectPassenger3ActionEntity);
    }
}
