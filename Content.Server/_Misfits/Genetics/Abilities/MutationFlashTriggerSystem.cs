// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Flash;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;
using Robust.Shared.Random;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed class MutationFlashTriggerSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MutationTriggerSystem _triggers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MutatableComponent, AfterFlashedEvent>(OnFlashed);
    }

    private void OnFlashed(Entity<MutatableComponent> ent, ref AfterFlashedEvent args)
    {
        foreach (var mutation in ent.Comp.Mutations.Values)
        {
            if (TryComp<TriggerOnFlashedComponent>(mutation, out var trigger) && _random.Prob(trigger.Prob))
                _triggers.Apply(mutation);
        }
    }

}
