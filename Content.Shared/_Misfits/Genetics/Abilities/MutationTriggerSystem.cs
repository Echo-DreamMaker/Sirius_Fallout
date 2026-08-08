// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared._Misfits.Common.Movement;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Shared._Misfits.Genetics.Abilities;

public sealed class MutationTriggerSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TriggerOnWalkComponent, FootStepEvent>(OnWalk);
    }

    private void OnWalk(Entity<TriggerOnWalkComponent> ent, ref FootStepEvent args) => Apply(ent.Owner);

    public void Apply(EntityUid mutation)
    {
        if (_net.IsClient || !TryComp<EffectOnTriggerMutationComponent>(mutation, out var effects) ||
            CompOrNull<MutationComponent>(mutation)?.Target is null)
            return;

        var effectArgs = new EntityEffectBaseArgs(mutation, EntityManager);
        foreach (var effect in effects.Effects)
        {
            if (effect.ShouldApply(effectArgs))
                effect.Effect(effectArgs);
        }
    }
}
