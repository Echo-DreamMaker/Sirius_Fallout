// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Radio;
using Content.Server.Temperature.Components;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared.Atmos;
using Content.Shared.Temperature;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed class GeneticsDependencySystem : EntitySystem
{
    [Dependency] private readonly FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TemperatureComponent, OnTemperatureChangeEvent>(OnTemperatureChanged);
        SubscribeLocalEvent<FireImmunityComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<FireImmunityComponent, ComponentStartup>(OnFireImmunityStartup);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    private void OnTemperatureChanged(Entity<TemperatureComponent> ent, ref OnTemperatureChangeEvent args)
    {
        if (HasComp<SpecialLowTempImmunityComponent>(ent) &&
            ent.Comp.CurrentTemperature < ent.Comp.ColdDamageThreshold)
            ent.Comp.CurrentTemperature = ent.Comp.ColdDamageThreshold;

        if (HasComp<SpecialHighTempImmunityComponent>(ent) &&
            ent.Comp.CurrentTemperature > ent.Comp.HeatDamageThreshold)
            ent.Comp.CurrentTemperature = ent.Comp.HeatDamageThreshold;
    }

    private void OnIgnited(Entity<FireImmunityComponent> ent, ref IgnitedEvent args)
    {
        if (TryComp<FlammableComponent>(ent, out var flammable))
            _flammable.Extinguish(ent, flammable);
    }

    private void OnFireImmunityStartup(Entity<FireImmunityComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<FlammableComponent>(ent, out var flammable))
            _flammable.Extinguish(ent, flammable);
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        var receiver = args.RadioReceiver;
        if (HasComp<DeafComponent>(receiver) || HasComp<DeafComponent>(Transform(receiver).ParentUid))
            args.Cancelled = true;
    }
}
