// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Temperature.Components;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed partial class TemperatureDamageMutationSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TemperatureDamageMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<TemperatureDamageMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<TemperatureDamageMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (!TryComp<TemperatureComponent>(args.Target, out var comp))
            return;
        comp.ColdDamageThreshold += ent.Comp.ColdOffset;
        comp.HeatDamageThreshold += ent.Comp.HeatOffset;
        Dirty(args.Target, comp);
    }

    private void OnRemoved(Entity<TemperatureDamageMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (!TryComp<TemperatureComponent>(args.Target, out var comp))
            return;
        comp.ColdDamageThreshold -= ent.Comp.ColdOffset;
        comp.HeatDamageThreshold -= ent.Comp.HeatOffset;
        Dirty(args.Target, comp);
    }
}
