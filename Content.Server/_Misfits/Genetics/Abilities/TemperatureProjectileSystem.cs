// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;
using Content.Shared.Temperature;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;

namespace Content.Server._Misfits.Genetics.Abilities;

[RegisterComponent]
public sealed partial class TemperatureProjectileComponent : Component
{
    [DataField(required: true)]
    public float Heat;
}

public sealed class TemperatureProjectileSystem : EntitySystem
{
    [Dependency] private TemperatureSystem _temperature = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TemperatureProjectileComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<TemperatureProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (TryComp<TemperatureComponent>(args.Target, out var temperature))
            _temperature.ChangeHeat(args.Target, ent.Comp.Heat, temperature: temperature);
    }
}
