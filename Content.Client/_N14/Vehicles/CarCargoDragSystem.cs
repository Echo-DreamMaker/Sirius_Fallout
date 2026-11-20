// #Sirius Add - client half of car cargo loading: marks the car as a valid drop
// target for crates. Drag validity lives on the client because CanDropTargetEvent
// decides what the cursor lets you attempt; the server re-checks and performs the
// actual load in CarSystem.
using Content.Shared._N14.Vehicles;
using Content.Shared.DragDrop;
using Content.Shared.Mech.EntitySystems;

namespace Content.Client._N14.Vehicles;

public sealed class CarCargoDragSystem : EntitySystem
{
    [Dependency] private readonly SharedCarSystem _car = default!;

    public override void Initialize()
    {
        base.Initialize();

        // #Sirius Add - the mech handler marks any drop on a car as handled; run
        // after it and OR in CanDrop for storable crates. Crates are already made
        // draggable by the client's EntityStorage/CanDrag handler.
        SubscribeLocalEvent<CarComponent, CanDropTargetEvent>(OnCanDropTarget,
            after: [typeof(SharedMechSystem)]);
    }

    private void OnCanDropTarget(Entity<CarComponent> ent, ref CanDropTargetEvent args)
    {
        if (!_car.CanStoreCargo(ent, args.Dragged))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }
}
