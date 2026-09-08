using Content.Shared.Buckle.Components;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Shared._Misfits.Vehicles.Vertibird;

/// <summary>
/// Shared Vertibird rules that both sides need to agree on: collision handling for
/// weapons fired from inside the cabin, and whether a crate may be loaded into the
/// cargo bay (the client uses it to decide if a drag is a valid drop, the server to
/// validate the drop it is handed).
/// </summary>
public sealed class SharedVertibirdSystem : EntitySystem
{
    /// <summary>Container holding crates loaded into the cargo bay.</summary>
    public const string CargoContainerId = "vertibird-cargo";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;

    /// <summary>
    /// A closed crate can be loaded while the bay still has room. Open crates are
    /// refused: their contents are sitting on the floor, so only the empty shell
    /// would travel.
    /// </summary>
    public bool CanStoreCargo(Entity<VertibirdComponent> vertibird, EntityUid crate)
    {
        SharedEntityStorageComponent? storage = null;
        if (!_entityStorage.ResolveStorage(crate, ref storage) || storage.Open)
            return false;

        return GetCargo(vertibird).Count < vertibird.Comp.CargoCapacity;
    }

    /// <summary>
    /// Crates currently in the bay. Empty until something has been loaded, since the
    /// container is only created on the first insertion.
    /// </summary>
    public IReadOnlyList<EntityUid> GetCargo(EntityUid vertibird)
    {
        return _container.TryGetContainer(vertibird, CargoContainerId, out var container)
            ? container.ContainedEntities
            : Array.Empty<EntityUid>();
    }

    public override void Initialize()
    {
        base.Initialize();
        // #Misfits Edited - Subscribe to the broadcast ProjectilePreventCollideEvent instead of
        // PreventCollideEvent directly (the event bus allows only one directed subscription per
        // component/event pair, which the base SharedProjectileSystem already owns).
        SubscribeLocalEvent<ProjectilePreventCollideEvent>(OnProjectilePreventCollide);
    }

    private void OnProjectilePreventCollide(ref ProjectilePreventCollideEvent args)
    {
        var projectile = args.Projectile;

        // The aircraft has no projectile health pool: rounds pass through its
        // broad fixture and strike the occupants sharing its cabin origin.
        if (HasComp<VertibirdComponent>(args.OtherEntity))
        {
            args.Cancelled = true;
            return;
        }

        // A passenger firing outward should not immediately hit another
        // passenger stacked at the same hidden cabin origin.
        if (projectile.Comp.Shooter is not { } shooter ||
            !TryComp<BuckleComponent>(shooter, out var shooterBuckle) ||
            shooterBuckle.BuckledTo is not { } vertibird ||
            !HasComp<VertibirdComponent>(vertibird))
        {
            return;
        }

        if (TryComp<BuckleComponent>(args.OtherEntity, out var otherBuckle) &&
            otherBuckle.BuckledTo == vertibird)
        {
            args.Cancelled = true;
        }
    }
}
