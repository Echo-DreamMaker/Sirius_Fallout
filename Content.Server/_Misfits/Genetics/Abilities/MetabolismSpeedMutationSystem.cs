// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed partial class MetabolismSpeedMutationSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MetabolismSpeedMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<MetabolismSpeedMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<MetabolismSpeedMutationComponent> ent, ref MutationAddedEvent args)
        => Modify(args.Target, 1f + ent.Comp.Bonus);

    private void OnRemoved(Entity<MetabolismSpeedMutationComponent> ent, ref MutationRemovedEvent args)
        => Modify(args.Target, 1f / (1f + ent.Comp.Bonus));

    private void Modify(EntityUid uid, float multiplier)
    {
        if (TryComp<MetabolizerComponent>(uid, out var mobComp))
        {
            mobComp.UpdateInterval *= multiplier;
            Dirty(uid, mobComp);
        }

        foreach (var (metabolizer, organ) in _body.GetBodyOrganComponents<MetabolizerComponent>(uid))
        {
            metabolizer.UpdateInterval *= multiplier;
            Dirty(organ.Owner, metabolizer);
        }
    }
}
