// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions.Events;
using Content.Shared.EntityEffects;

namespace Content.Shared._Misfits.Actions;

public sealed class EffectActionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EffectActionComponent, ActionPerformedEvent>(OnPerformed);
        SubscribeLocalEvent<EffectActionComponent, EffectInstantActionEvent>(OnInstant);
        SubscribeLocalEvent<EffectActionComponent, EffectTargetActionEvent>(OnTarget);
    }

    private void OnPerformed(Entity<EffectActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (ent.Comp.OnPerformed)
            Apply(args.Performer, ent.Comp.Effects);
    }

    private void OnInstant(Entity<EffectActionComponent> ent, ref EffectInstantActionEvent args)
    {
        Apply(args.Performer, ent.Comp.Effects);
        args.Handled = true;
    }

    private void OnTarget(Entity<EffectActionComponent> ent, ref EffectTargetActionEvent args)
    {
        Apply(args.Target, ent.Comp.Effects);
        args.Handled = true;
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
