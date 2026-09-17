// #Misfits Add - Manages DrugSpecialTimelineComponent lifecycle: advances phases in real
// time after drug ingestion, writes absolute stat values into DrugSpecialBoostComponent,
// manages damage resistance via DamageableComponent.DamageModifierSets, and removes the
// timeline (plus boosts) once the last phase is reached.

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
///     Advances drug timelines phase-by-phase using real game-time, writes the current
///     phase's stat deltas into <see cref="DrugSpecialBoostComponent"/> so existing
///     gameplay hooks (speed, spread, melee) pick them up, and manages a damage
///     resistance set on <see cref="DamageableComponent.DamageModifierSets"/>.
/// </summary>
public sealed class DrugSpecialTimelineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;
    [Dependency] private readonly DrugSpecialBoostSystem _boostSys = default!;

    /// <summary>How long the timeline source keeps itself alive each update (fallback if the boost writer stalls).</summary>
    private const float KeepAliveSeconds = 2f;

    private readonly List<Entity<DrugSpecialTimelineComponent>> _tracked = new();

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;

        SubscribeLocalEvent<DrugSpecialTimelineComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DrugSpecialTimelineComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<DrugSpecialTimelineComponent> ent, ref ComponentStartup args)
    {
        _tracked.Add(ent);
    }

    private void OnShutdown(Entity<DrugSpecialTimelineComponent> ent, ref ComponentShutdown args)
    {
        _tracked.Remove(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        for (var i = _tracked.Count - 1; i >= 0; i--)
        {
            var ent = _tracked[i];
            var comp = ent.Comp;

            if (comp.Deleted)
            {
                _tracked.RemoveAt(i);
                continue;
            }

            if (comp.Phases.Count == 0)
            {
                RemoveTimeline(ent);
                continue;
            }

            // Advance through all phases whose time has been reached.
            while (comp.PhaseIndex < comp.Phases.Count - 1
                && comp.Anchor + TimeSpan.FromSeconds(comp.Phases[comp.PhaseIndex + 1].Time) <= now)
            {
                comp.PhaseIndex++;
            }

            var phase = comp.Phases[comp.PhaseIndex];

            // Write the phase's stat values into DrugSpecialBoostComponent through the shared
            // resolver as the timeline source. Unlike a direct absolute write, this leaves
            // other drugs' concurrent metabolism sources intact (their contribution is
            // re-resolved instead of being stomped to zero), updates only when the resolved
            // signature changes (no per-frame replicated churn / stat flicker), and manages
            // the component keep-alive expiry.
            var boost = EnsureComp<DrugSpecialBoostComponent>(ent.Owner);
            var signature = new StatSignature(
                phase.Strength,
                phase.Perception,
                phase.Endurance,
                phase.Charisma,
                phase.Intelligence,
                phase.Agility,
                phase.Luck);
            _boostSys.SetSource(ent.Owner, boost, DrugSpecialBoostComponent.TimelineSourceKey, signature, KeepAliveSeconds);

            // Manage damage resistance set.
            ApplyDamageResist(ent, phase.DamageResistSet);

            // On the last phase: the timeline is complete.
            if (comp.PhaseIndex == comp.Phases.Count - 1)
            {
                RemoveTimeline(ent);
                continue;
            }
        }
    }

    // ── Damage Resistance ────────────────────────────────────────────────────

    private void ApplyDamageResist(Entity<DrugSpecialTimelineComponent> ent, string? newSet)
    {
        var uid = ent.Owner;
        var currentSet = ent.Comp.ActiveResistSet;

        if (currentSet == newSet)
            return;

        // Remove old set.
        if (!string.IsNullOrEmpty(currentSet)
            && TryComp<DamageableComponent>(uid, out var damageable)
            && damageable.DamageModifierSets.Contains(currentSet))
        {
            damageable.DamageModifierSets.Remove(currentSet);
            Dirty(uid, damageable);
        }

        // Add new set.
        if (!string.IsNullOrEmpty(newSet)
            && _proto.TryIndex<DamageModifierSetPrototype>(newSet, out _)
            && TryComp<DamageableComponent>(uid, out var damageable2))
        {
            if (!damageable2.DamageModifierSets.Contains(newSet))
            {
                damageable2.DamageModifierSets.Add(newSet);
                Dirty(uid, damageable2);
            }
        }

        ent.Comp.ActiveResistSet = newSet;
        Dirty(uid, ent.Comp);
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    private void RemoveTimeline(Entity<DrugSpecialTimelineComponent> ent)
    {
        var uid = ent.Owner;

        // Clear the damage resist if any was active.
        ApplyDamageResist(ent, null);

        // Remove the boost component (values reset to 0 = baseline).
        RemComp<DrugSpecialBoostComponent>(uid);

        // Force a speed recalc so the player returns to normal speed.
        _speedModifier.RefreshMovementSpeedModifiers(uid);

        // Remove the timeline component.
        RemComp<DrugSpecialTimelineComponent>(uid);
    }
}
