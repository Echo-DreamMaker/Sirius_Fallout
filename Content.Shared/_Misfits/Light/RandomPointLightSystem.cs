// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Random;

namespace Content.Shared._Misfits.Light;

public sealed partial class RandomPointLightSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomPointLightComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<RandomPointLightComponent> ent, ref MapInitEvent args)
    {
        if (!_light.TryGetLight(ent, out var light))
            return;

        var color = _random.Pick(ent.Comp.Colors);
        var energy = _random.NextFloat(ent.Comp.Energy.X, ent.Comp.Energy.Y);
        _light.SetColor(ent.Owner, color, light);
        _light.SetEnergy(ent.Owner, energy, light);
    }
}
