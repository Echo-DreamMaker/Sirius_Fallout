// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed partial class BleedingMutationSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BleedingMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<BleedingMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<BleedingMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (!TryComp<BloodstreamComponent>(args.Target, out var blood))
            return;

        blood.BloodRefreshAmount *= ent.Comp.RefreshModifier;
        blood.BleedReductionAmount /= ent.Comp.BleedModifier;
        Dirty(args.Target, blood);
    }

    private void OnRemoved(Entity<BleedingMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (!TryComp<BloodstreamComponent>(args.Target, out var blood))
            return;

        blood.BloodRefreshAmount /= ent.Comp.RefreshModifier;
        blood.BleedReductionAmount *= ent.Comp.BleedModifier;
        Dirty(args.Target, blood);
    }
}
