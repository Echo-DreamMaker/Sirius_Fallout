using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Misfits.Fire;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Robust.Shared.Random;

namespace Content.Server._Misfits.Fire;

/// <summary>
/// Handles volatile fuel containers exploding when ignited or shot.
/// Also checks wielder inventory when the wielder catches fire.
/// </summary>
public sealed class VolatileFuelContainerSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        // Direct ignition of the container entity itself.
        SubscribeLocalEvent<VolatileFuelContainerComponent, IgnitedEvent>(OnDirectIgnited);

        // Projectile hit on the container.
        SubscribeLocalEvent<VolatileFuelContainerComponent, ProjectileHitEvent>(OnProjectileHit);

        // When a wielder catches fire, check their hands and inventory for volatile fuel.
        SubscribeLocalEvent<FlammableComponent, IgnitedEvent>(OnWielderIgnited);
    }

    private void OnDirectIgnited(Entity<VolatileFuelContainerComponent> ent, ref IgnitedEvent args)
    {
        if (!HasFlammableFuel(ent))
            return;

        if (!_random.Prob(ent.Comp.IgniteChance))
            return;

        Detonate(ent);
    }

    private void OnProjectileHit(Entity<VolatileFuelContainerComponent> ent, ref ProjectileHitEvent args)
    {
        if (!HasFlammableFuel(ent))
            return;

        if (!_random.Prob(ent.Comp.ShotChance))
            return;

        Detonate(ent);
    }

    /// <summary>
    /// When a player/mob catches fire, scan their hands and inventory slots
    /// for volatile fuel containers that might explode.
    /// </summary>
    private void OnWielderIgnited(Entity<FlammableComponent> ent, ref IgnitedEvent args)
    {
        // Check held items.
        foreach (var hand in _hands.EnumerateHands(ent.Owner))
        {
            if (hand.HeldEntity is { } held &&
                TryComp<VolatileFuelContainerComponent>(held, out var vol) &&
                HasFlammableFuel((held, vol)) &&
                _random.Prob(vol.IgniteChance))
            {
                Detonate((held, vol));
            }
        }

        // Check inventory slots.
        if (_inventory.TryGetContainerSlotEnumerator(ent.Owner, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } item &&
                    TryComp<VolatileFuelContainerComponent>(item, out var vol) &&
                    HasFlammableFuel((item, vol)) &&
                    _random.Prob(vol.IgniteChance))
                {
                    Detonate((item, vol));
                }
            }
        }
    }

    private bool HasFlammableFuel(Entity<VolatileFuelContainerComponent> ent)
    {
        if (!TryComp<SolutionContainerManagerComponent>(ent.Owner, out var manager))
            return false;

        if (ent.Comp.SolutionId != null)
        {
            if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out _, out var solution))
                return false;

            foreach (var reagent in ent.Comp.FlammableReagents)
            {
                if (solution.ContainsPrototype(reagent))
                    return true;
            }

            return false;
        }

        foreach (var (_, sol) in _solution.EnumerateSolutions((ent.Owner, manager)))
        {
            foreach (var reagent in ent.Comp.FlammableReagents)
            {
                if (sol.Comp.Solution.ContainsPrototype(reagent))
                    return true;
            }
        }

        return false;
    }

    private void Detonate(Entity<VolatileFuelContainerComponent> ent)
    {
        _explosion.QueueExplosion(
            ent.Owner,
            ent.Comp.ExplosionPrototype,
            ent.Comp.TotalIntensity,
            ent.Comp.Dropoff,
            ent.Comp.MaxTileIntensity
        );

        QueueDel(ent);
    }
}
