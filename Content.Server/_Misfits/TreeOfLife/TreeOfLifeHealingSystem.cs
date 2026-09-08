using Content.Server.Chat.Systems;
using Content.Server._Misfits.SmokeSignal;
using Content.Shared._Misfits.SmokeSignal;
using Content.Shared._Misfits.TreeOfLife;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Misfits.TreeOfLife;

/// <summary>
///     Restores player-controlled living mobs near the Tree of Life.
/// </summary>
public sealed partial class TreeOfLifeHealingSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SmokeSignalSystem _signals = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TreeOfLifeHealingComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            component.HealingAccumulator += frameTime;
            var healThisTick = component.HealingAccumulator >= component.HealingCooldown;
            if (healThisTick)
                component.HealingAccumulator %= component.HealingCooldown;

            component.HearthHealingAccumulator += frameTime;
            var hearthThisTick = component.HearthHealingAccumulator >= component.HearthHealingCooldown;
            if (hearthThisTick)
                component.HearthHealingAccumulator %= component.HearthHealingCooldown;

            var hearthActive = TryComp<TreeOfLifeRitesComponent>(uid, out var rites)
                && rites.ActiveRite == TreeOfLifeRite.Hearth;
            var treeSignal = Comp<SmokeSignalComponent>(uid);

            var nearby = _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, component.Range);
            var currentPlayers = new HashSet<EntityUid>();

            foreach (var target in nearby)
            {
                if (!TryComp<ActorComponent>(target, out var actor)
                    || actor.PlayerSession.AttachedEntity != target
                    || _mobState.IsDead(target))
                    continue;

                currentPlayers.Add(target);
                if (component.PlayersInRange.Add(target))
                    _chat.SendPrivateDoMessage(actor.PlayerSession, Loc.GetString("tree-of-life-healing-aura"));

                if (healThisTick && HasComp<DamageableComponent>(target))
                    _damageable.TryChangeDamage(target, component.Healing, true, origin: uid, canSever: false);

                if (hearthThisTick && hearthActive && _signals.IsInDepartment(target, treeSignal))
                {
                    if (HasComp<DamageableComponent>(target))
                        _damageable.TryChangeDamage(target, component.Healing, true, origin: uid, canSever: false);

                    _stamina.TakeStaminaDamage(target, -component.HearthStaminaRecovery, source: uid);
                }
            }

            component.PlayersInRange.IntersectWith(currentPlayers);
        }
    }
}
