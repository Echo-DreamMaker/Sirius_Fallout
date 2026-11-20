// #Sirius Add - car console (seats, hull, cargo). Tank steering is handled by the
// predicted mover input in SharedMoverController.
using System.Linq;
using System.Numerics;
using Content.Shared._N14.Vehicles;
using Content.Shared.DragDrop;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._N14.Vehicles;

public sealed partial class CarSystem
{
    /// <summary>Container that holds crates loaded into the car's cargo bay.</summary>
    public const string CargoContainerId = "car-cargo";

    private const float CargoDoAfterDelay = 3f;

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;

    private void InitializeUI()
    {
        SubscribeLocalEvent<CarComponent, CarOpenUiEvent>(OnCarOpenUi);
        SubscribeLocalEvent<CarComponent, AfterActivatableUIOpenEvent>(OnAfterUiOpen);
        SubscribeLocalEvent<CarComponent, CarSelectSeatMessage>(OnSelectSeat);
        SubscribeLocalEvent<CarComponent, CarPassengerBoardDoAfterEvent>(OnPassengerBoardDoAfter);
        SubscribeLocalEvent<CarComponent, CarLoadCargoMessage>(OnLoadCargoMessage);
        SubscribeLocalEvent<CarComponent, CarUnloadCargoMessage>(OnUnloadCargoMessage);
        SubscribeLocalEvent<CarComponent, CarCargoDoAfterEvent>(OnCargoDoAfter);
        // #Sirius Add - crates dropped onto the hull go straight to the cargo DoAfter
        // before the generic mech handler can mistake them for passengers.
        SubscribeLocalEvent<CarComponent, CanDropTargetEvent>(OnCanDropCargo,
            before: new[] { typeof(SharedMechSystem) });
        SubscribeLocalEvent<CarComponent, DragDropTargetEvent>(OnDropCargo,
            before: new[] { typeof(SharedMechSystem) });
    }

    private void OnCarOpenUi(EntityUid uid, CarComponent car, CarOpenUiEvent args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        args.Handled = true;
        ToggleCarUi(uid, mech);
    }

    private void ToggleCarUi(EntityUid uid, MechComponent mech)
    {
        if (mech.PilotSlot.ContainedEntity is not { } pilot || !TryComp<ActorComponent>(pilot, out var actor))
            return;

        _ui.TryToggleUi(uid, CarUiKey.Key, actor.PlayerSession);
        UpdateUi(uid);
    }

    private void OnAfterUiOpen(EntityUid uid, CarComponent car, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUi(uid);
    }

    /// <summary>
    /// #Sirius Add - keep the console in sync with engine, damage and container changes.
    /// </summary>
    public void UpdateUi(EntityUid uid)
    {
        if (_net.IsClient || !TryComp<CarComponent>(uid, out var car) ||
            !TryComp<MechComponent>(uid, out var mech))
            return;

        _ui.SetUiState(uid, CarUiKey.Key, BuildUiState(uid, car, mech));
    }

    private CarBoundUiState BuildUiState(EntityUid uid, CarComponent car, MechComponent mech)
    {
        var seats = new CarSeatUiState[5];
        for (var i = 0; i < seats.Length; i++)
        {
            var occupant = GetSeatOccupant(mech, i);
            var seatName = i == 0
                ? Loc.GetString("car-seat-pilot")
                : Loc.GetString("car-seat-passenger", ("number", i));

            seats[i] = new CarSeatUiState(i, seatName,
                occupant == null ? null : Identity.Name(occupant.Value, EntityManager));
        }

        var stored = GetCargo(uid);
        var cargo = new CarCargoUiState[stored.Count];
        for (var i = 0; i < cargo.Length; i++)
            cargo[i] = new CarCargoUiState(GetNetEntity(stored[i]), Name(stored[i]));

        return new CarBoundUiState(
            Loc.GetString(car.WindowTitleLocId),
            mech.Integrity.Float(),
            mech.MaxIntegrity.Float(),
            mech.EngineRunning,
            seats,
            cargo,
            car.CargoCapacity);
    }

    private static EntityUid? GetSeatOccupant(MechComponent mech, int seat)
    {
        return seat switch
        {
            0 => mech.PilotSlot.ContainedEntity,
            1 => mech.PassengerSlot1.ContainedEntity,
            2 => mech.PassengerSlot2.ContainedEntity,
            3 => mech.PassengerSlot3.ContainedEntity,
            4 => mech.PassengerSlot4.ContainedEntity,
            _ => null,
        };
    }

    private void OnSelectSeat(EntityUid uid, CarComponent car, ref CarSelectSeatMessage args)
    {
        if (!TryComp<MechComponent>(uid, out var mech))
            return;
        if (args.SeatIndex < 0 || args.SeatIndex > 4)
            return;

        var user = args.Actor;
        var current = _mech.GetSeatIndex(mech, user);
        if (current == args.SeatIndex)
        {
            UpdateUi(uid);
            return;
        }

        if (GetSeatOccupant(mech, args.SeatIndex) != null)
        {
            _popup.PopupEntity(Loc.GetString("car-seat-taken"), uid, user);
            return;
        }

        // Seat swaps never leave a body behind in the old slot.
        if (current != null)
            _mech.TryLeaveSeat(uid, user, mech);

        if (args.SeatIndex == 0)
        {
            var doAfter = new DoAfterArgs(EntityManager, user, mech.EntryDelay,
                new MechEntryEvent(), uid, target: uid)
            {
                BreakOnMove = true,
            };
            _doAfter.TryStartDoAfter(doAfter);
        }
        else
        {
            var doAfter = new DoAfterArgs(EntityManager, user, mech.EntryDelay,
                new CarPassengerBoardDoAfterEvent(args.SeatIndex), uid, target: uid)
            {
                BreakOnMove = true,
            };
            _doAfter.TryStartDoAfter(doAfter);
        }

        UpdateUi(uid);
    }

    private void OnPassengerBoardDoAfter(EntityUid uid, CarComponent car, ref CarPassengerBoardDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target != uid)
            return;
        if (!TryComp<MechComponent>(uid, out var mech))
            return;

        args.Handled = true;
        if (!_mech.TryInsertPassengerIntoSlot(uid, args.User, args.SeatIndex, mech))
        {
            _popup.PopupEntity(Loc.GetString("car-seat-taken"), uid, args.User);
            return;
        }

        RefreshCarActions(uid, mech);
        UpdateUi(uid);
    }

    // ---- Cargo bay ----

    private IReadOnlyList<EntityUid> GetCargo(EntityUid car)
    {
        return _container.TryGetContainer(car, CargoContainerId, out var container)
            ? container.ContainedEntities
            : Array.Empty<EntityUid>();
    }

    private bool CanStoreCargo(EntityUid car, EntityUid crate, CarComponent carComp)
    {
        var cargo = GetCargo(car);
        if (cargo.Contains(crate) || cargo.Count >= carComp.CargoCapacity)
            return false;

        SharedEntityStorageComponent? storage = null;
        return _entityStorage.ResolveStorage(crate, ref storage) && !storage.Open;
    }

    private void OnCanDropCargo(EntityUid uid, CarComponent car, ref CanDropTargetEvent args)
    {
        if (args.Dragged == uid || !CanStoreCargo(uid, args.Dragged, car))
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnDropCargo(EntityUid uid, CarComponent car, ref DragDropTargetEvent args)
    {
        if (args.Handled || args.Dragged == uid || !CanStoreCargo(uid, args.Dragged, car))
            return;

        args.Handled = true;
        StartCargoDoAfter(uid, args.User, args.Dragged, loading: true);
    }

    private void OnLoadCargoMessage(EntityUid uid, CarComponent car, ref CarLoadCargoMessage args)
    {
        var user = args.Actor;
        if (!TryComp<PullerComponent>(user, out var puller) || puller.Pulling is not { } crate)
        {
            _popup.PopupEntity(Loc.GetString("car-cargo-nothing-pulled"), uid, user);
            return;
        }

        if (!CanStoreCargo(uid, crate, car))
        {
            _popup.PopupEntity(Loc.GetString("car-cargo-will-not-fit"), uid, user);
            return;
        }

        StartCargoDoAfter(uid, user, crate, loading: true);
    }

    private void OnUnloadCargoMessage(EntityUid uid, CarComponent car, ref CarUnloadCargoMessage args)
    {
        if (!TryGetEntity(args.Crate, out var crate) || !GetCargo(uid).Contains(crate.Value))
            return;

        StartCargoDoAfter(uid, args.Actor, crate.Value, loading: false);
    }

    private void StartCargoDoAfter(EntityUid car, EntityUid user, EntityUid crate, bool loading)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, CargoDoAfterDelay,
            new CarCargoDoAfterEvent(GetNetEntity(crate)), car, target: car,
            used: loading ? crate : null)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCargoDoAfter(EntityUid uid, CarComponent car, ref CarCargoDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryGetEntity(args.Crate, out var crateUid))
            return;

        args.Handled = true;
        var crate = crateUid.Value;
        var container = _container.EnsureContainer<Container>(uid, CargoContainerId);

        if (container.Contains(crate))
        {
            // Occupants are parented to the car, so unload them beside the hull instead
            // of at their own (car-local) coordinates.
            var userXform = Transform(args.User);
            var destination = userXform.ParentUid == uid
                ? _transform.GetMoverCoordinates(new EntityCoordinates(uid, new Vector2(0f, -1.2f)))
                : _transform.GetMoverCoordinates(args.User, userXform);
            _container.Remove(crate, container, destination: destination);
        }
        else if (CanStoreCargo(uid, crate, car))
        {
            _container.Insert(crate, container);
        }

        UpdateUi(uid);
    }
}
