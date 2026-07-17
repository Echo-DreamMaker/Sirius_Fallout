using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Misfits.Weapons.Ranged.Flamer;

public abstract class SharedFlamerAmmoSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FlamerAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<FlamerAmmoProviderComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<FlamerAmmoProviderComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnTakeAmmo(Entity<FlamerAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out var solutionEnt, out var solution))
            return;

        var totalCost = ent.Comp.FireCost * args.Shots;
        if (solution.Volume < totalCost)
        {
            args.Reason = Loc.GetString("gun-no-magazine");
            return;
        }

        for (var i = 0; i < args.Shots; i++)
        {
            var spent = _solution.SplitSolution(solutionEnt.Value, ent.Comp.FireCost);
            if (spent.Volume < FixedPoint2.Zero || spent.Volume == FixedPoint2.Zero)
                break;

            args.Ammo.Add((ent.Owner, new FlamerShot(ent.Comp.FireTilePrototype, ent.Comp.ProjectileProto, ent.Comp.Range)));
        }
    }

    private void OnGetAmmoCount(Entity<FlamerAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out _, out var solution))
            return;

        args.Count = (int) (solution.Volume / ent.Comp.FireCost);
        args.Capacity = (int) (solution.MaxVolume / ent.Comp.FireCost);
    }

    private void OnAttemptShoot(Entity<FlamerAmmoProviderComponent> ent, ref AttemptShootEvent args)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out _, out var solution) ||
            solution.Volume < ent.Comp.FireCost)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("gun-no-magazine");
        }
    }
}
