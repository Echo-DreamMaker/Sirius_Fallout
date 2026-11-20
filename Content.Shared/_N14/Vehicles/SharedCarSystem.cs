// #Sirius Add - shared car rules needed on both sides: the client uses CanStoreCargo
// to decide whether dragging a crate onto the hull is a valid drop, the server
// re-validates the drop before running the load DoAfter in CarSystem.
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Shared._N14.Vehicles;

public sealed class SharedCarSystem : EntitySystem
{
    /// <summary>Container holding crates loaded into the cargo bay.</summary>
    public const string CargoContainerId = "car-cargo";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;

    /// <summary>
    /// #Sirius Add - a closed crate can be loaded while the bay still has room.
    /// Open crates are refused: their contents are on the floor, so only the
    /// empty shell would travel.
    /// </summary>
    public bool CanStoreCargo(Entity<CarComponent> car, EntityUid crate)
    {
        var cargo = GetCargo(car.Owner);
        if (cargo.Contains(crate) || cargo.Count >= car.Comp.CargoCapacity)
            return false;

        SharedEntityStorageComponent? storage = null;
        return _entityStorage.ResolveStorage(crate, ref storage) && !storage.Open;
    }

    public IReadOnlyList<EntityUid> GetCargo(EntityUid car)
    {
        return _container.TryGetContainer(car, CargoContainerId, out var container)
            ? container.ContainedEntities
            : Array.Empty<EntityUid>();
    }
}
