// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Handles running effects for <see cref="EffectsMutationComponent"/>.
/// </summary>
public sealed partial class EffectsMutationSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EffectsMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<EffectsMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<EffectsMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (args.Automatic && ent.Comp.IgnoreAutomatic)
            return;

        Apply(args.Target, ent.Comp.Added);
    }

    private void OnRemoved(Entity<EffectsMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (args.Automatic && ent.Comp.IgnoreAutomatic)
            return;

        Apply(args.Target, ent.Comp.Removed);
    }

    private void Apply(EntityUid target, IEnumerable<EntityEffect> effects)
    {
        if (_net.IsClient)
            return;

        var effectArgs = new EntityEffectBaseArgs(target, EntityManager);
        foreach (var effect in effects)
        {
            if (effect.ShouldApply(effectArgs))
                effect.Effect(effectArgs);
        }
    }
}
