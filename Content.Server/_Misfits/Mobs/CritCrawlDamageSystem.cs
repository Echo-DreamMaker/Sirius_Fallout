// Misfits Add: System that deals damage when a crit entity crawls, if the DamageWhileCritMove CVar is enabled.
// This makes crawling while crit a risky choice — you can escape danger but it hurts.
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._Misfits.C27;
using Content.Shared.Silicon.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Mobs;

/// <summary>
///     When the <see cref="CCVars.DamageWhileCritMove"/> CVar is enabled,
///     entities in Critical state that move (crawl) will take damage over time.
///     This makes crawling a trade-off: escape danger but worsen your injuries.
/// </summary>
public sealed class CritCrawlDamageSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Damage dealt per second while crawling in crit
    private const float DamagePerSecond = 1.5f;
    // Minimum velocity squared to count as "crawling"
    private const float MinMoveVelocitySq = 0.01f;

    private TimeSpan _nextCheck = TimeSpan.Zero;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1.0);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_config.GetCVar(CCVars.DamageWhileCritMove))
            return;

        var currentTime = _timing.CurTime;
        if (currentTime < _nextCheck)
            return;

        _nextCheck = currentTime + _checkInterval;

        var query = EntityQueryEnumerator<MobStateComponent, DamageableComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var damageable, out var physics))
        {
            if (mobState.CurrentState != Shared.Mobs.MobState.Critical)
                continue;

            // Must be downed (crawling) to take damage
            if (!_standing.IsDown(uid))
                continue;

            // Only players can crit crawl, NPCs (no ActorComponent etc) don't
            if (!HasComp<ActorComponent>(uid))
                continue;

            // Robots/silicons/C27s never crit crawl here
            if (HasComp<SiliconComponent>(uid)
                || HasComp<MisfitsC27Component>(uid))
                continue;

            // Must actually be moving
            if (physics.LinearVelocity.LengthSquared() < MinMoveVelocitySq)
                continue;

            // Must not be knocked down (stunned) — that's involuntary
            if (HasComp<KnockedDownComponent>(uid))
                continue;

            // Apply damage — crawling worsens your injuries
            var damage = new DamageSpecifier
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Blunt", FixedPoint2.New(DamagePerSecond) }
                }
            };

            _damageable.TryChangeDamage(uid, damage, origin: uid);
        }
    }
}
