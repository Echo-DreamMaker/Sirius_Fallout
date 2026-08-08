// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Melee.Events;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed class StrengthMutationSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<MutatableComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnGetMeleeDamage(Entity<MutatableComponent> ent, ref GetMeleeDamageEvent args)
    {
        foreach (var mutation in ent.Comp.Mutations.Values)
        {
            if (TryComp<StrengthMutationComponent>(mutation, out var strength))
                args.Damage *= strength.MeleeModifier;
        }
    }
}
