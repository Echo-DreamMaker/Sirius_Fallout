// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Polymorph.Systems;
using Content.Server.Polymorph.Components;
using Content.Shared.Polymorph;
using Content.Shared.Throwing;

namespace Content.Server._Misfits.Genetics.Abilities;

[RegisterComponent]
public sealed partial class TongueSpikeRevertOnLandComponent : Component;

public sealed class TongueSpikeRevertOnLandSystem : EntitySystem
{
    [Dependency] private PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TongueSpikeRevertOnLandComponent, LandEvent>(OnLand);
    }

    private void OnLand(Entity<TongueSpikeRevertOnLandComponent> ent, ref LandEvent args)
    {
        if (TryComp<PolymorphedEntityComponent>(ent, out var polymorphed))
            _polymorph.Revert((ent.Owner, polymorphed));
    }
}
