// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed partial class ThermalRegulatorMutationSystem : EntitySystem
{
    [Dependency] private EntityQuery<ThermalRegulatorComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalRegulatorMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<ThermalRegulatorMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<ThermalRegulatorMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (!_query.TryComp(args.Target, out var comp))
            return;

        comp.ShiveringHeatRegulation *= ent.Comp.Shivering;
        comp.SweatHeatRegulation *= ent.Comp.Sweating;
        comp.MetabolismHeat *= ent.Comp.Metabolism;
        comp.ImplicitHeatRegulation *= ent.Comp.Regulation;
    }

    private void OnRemoved(Entity<ThermalRegulatorMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (!_query.TryComp(args.Target, out var comp))
            return;

        comp.ShiveringHeatRegulation /= ent.Comp.Shivering;
        comp.SweatHeatRegulation /= ent.Comp.Sweating;
        comp.MetabolismHeat /= ent.Comp.Metabolism;
        comp.ImplicitHeatRegulation /= ent.Comp.Regulation;
    }
}
