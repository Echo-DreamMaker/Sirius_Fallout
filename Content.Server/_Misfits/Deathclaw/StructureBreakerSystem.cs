// # #Cythisiax Added - Server system: the sentient deathclaw (StructureBreaker) smashes structures.
// Anchored structures WITH a Destructible component are broken via massive blunt force through the
// normal damage pipeline (most walls use the Inorganic container, which accepts Brute but rejects
// Structural, so pure Structural melee damage never broke them). Anchored structures WITHOUT a
// Destructible (truly "indestructible" walls) are force-destroyed on melee hit.
using Content.Server.Destructible;
using Content.Shared._Misfits.Deathclaw;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Deathclaw;

public sealed class StructureBreakerSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStructureBreakerSystem _structureBreaker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StructureBreakerComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<StructureBreakerComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (TerminatingOrDeleted(target) || target == ent.Owner)
                continue;

            // Only anchored structures count (walls etc.). Mobs and items are never anchored.
            if (!Transform(target).Anchored)
                continue;

            // An explicit Bwonsamdi exception is absolute, even when the target also has a huge
            // Destructible threshold. These are the targets normal melee is never expected to break.
            if (_structureBreaker.IsSpecialTarget(target))
            {
                _audio.PlayPvs(ent.Comp.BreakSound, target);
                QueueDel(target);
                args.Handled = true;
                return;
            }

            // Has Destructible -> smash it with massive blunt force through the damage pipeline
            // (Inorganic containers accept Brute, so this reliably exceeds wall thresholds).
            if (HasComp<DestructibleComponent>(target))
            {
                _audio.PlayPvs(ent.Comp.BreakSound, target);
                var smash = new DamageSpecifier();
                smash.DamageDict["Blunt"] = 5000;
                _damageable.TryChangeDamage(target, smash, ignoreResistances: true);
                continue;
            }
        }
    }
}
