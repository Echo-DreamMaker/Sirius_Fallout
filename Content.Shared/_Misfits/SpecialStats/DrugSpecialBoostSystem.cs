// #Misfits Add - Manages DrugSpecialBoostComponent lifecycle (expiry) and translates
// drug-sourced SPECIAL stat deltas into REAL temporary modifiers on
// SpecialComponent.Temporary*Modifier via SharedSpecialSystem. Because the base
// SPECIAL consumers (movement, spread, melee, requirements, crafting gates) read
// effective stats, boosting the temporary modifiers makes the actual characteristics
// increase instead of layering on parallel event micro-effects.
// Component expiry is polled every update; on removal the temporary modifiers are cleared.

using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
/// Handles the lifecycle of <see cref="DrugSpecialBoostComponent"/> and mirrors its
/// active stat deltas into <see cref="SpecialComponent"/> temporary modifiers so every
/// SPECIAL-based system reacts to the drug:
///   • Strength   → melee damage, weapon/speed requirements (SpecialCombatSystem, Wieldable...)
///   • Perception → spread/recoil, traces (SpecialPerceptionSystem, ...)
///   • Agility    → movement speed, action speed (SpecialMovementSystem, ...)
///   • Endurance  → health thresholds, resistances, stamina (SpecialEnduranceSystem, ...)
///   • Charisma / Intelligence / Luck → gated systems read effective values each event.
/// </summary>
public sealed class DrugSpecialBoostSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    /// <summary>Source tag so drug-sourced modifiers can be removed without touching other effects.</summary>
    public const string ModifierSource = "drug-special";

    // Maintained across frames for O(n) expiry checking without per-tick querying.
    private readonly List<Entity<DrugSpecialBoostComponent>> _tracked = new();

    // Last-applied signature per entity so we only rewrite SpecialComponent when a
    // drug phase/new dose actually changes the boost values (avoids per-frame churn).
    private readonly Dictionary<EntityUid, StatSignature> _applied = new();

    public override void Initialize()
    {
        base.Initialize();

        // Update runs even between predicted ticks so the timer expires reliably.
        UpdatesOutsidePrediction = true;

        // Track each new component so Update() can iterate without a full ECS query.
        SubscribeLocalEvent<DrugSpecialBoostComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DrugSpecialBoostComponent, ComponentShutdown>(OnShutdown);
    }

    // ── Startup tracking ────────────────────────────────────────────────────────

    private void OnStartup(Entity<DrugSpecialBoostComponent> ent, ref ComponentStartup args)
    {
        _tracked.Add(ent);
    }

    private void OnShutdown(Entity<DrugSpecialBoostComponent> ent, ref ComponentShutdown args)
    {
        _tracked.Remove(ent);
        _applied.Remove(ent.Owner);

        if (_net.IsServer)
            _special.ClearTemporaryModifiers(ent.Owner, ModifierSource);
    }

    // ── Timer / expiry + mirror ────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        for (var i = _tracked.Count - 1; i >= 0; i--)
        {
            var ent = _tracked[i];

            // Purge stale references to entities that were deleted externally.
            if (ent.Comp.Deleted)
            {
                _tracked.RemoveAt(i);
                _applied.Remove(ent.Owner);
                continue;
}

            // Client-side expiry is handled by the server: it owns the modifier list.
            if (!_net.IsServer)
                continue;

            // Purge sources whose per-drug grace window expired since the last tick and
            // react to the resulting resolution change (a flushed drug drops its fields
            // even while another drug's source keeps the component alive).
            Reconcile(ent.Owner, ent.Comp);

            // Expired — remove mirror + component.
            if (ent.Comp.ExpireTime <= now)
            {
                _tracked.RemoveAt(i);
                _applied.Remove(ent.Owner);
                _special.ClearTemporaryModifiers(ent.Owner, ModifierSource);
                RemComp<DrugSpecialBoostComponent>(ent.Owner);
                continue;
            }

            ApplyMirrorIfChanged(ent.Owner, ent.Comp);
        }
    }

    private void ApplyMirrorIfChanged(EntityUid uid, DrugSpecialBoostComponent comp)
    {
        // Absolute replacement semantics: each phase/dose writes a full signature,
        // so when it changes we clear the drug's old modifiers and re-apply the new set.
        var signature = new StatSignature(
            comp.StrengthBoost,
            comp.PerceptionBoost,
            comp.EnduranceBoost,
            comp.CharismaBoost,
            comp.IntelligenceBoost,
            comp.AgilityBoost,
            comp.LuckBoost);

        if (_applied.TryGetValue(uid, out var last) && last == signature)
            return;

        // The component may simply not exist on entities that have no SPECIAL stats
        // (non-ghost actors without SpecialComponent). TryModifyTemporary handles that.
        _special.ClearTemporaryModifiers(uid, ModifierSource);
        TryModify(uid, SpecialStat.Strength, comp.StrengthBoost);
        TryModify(uid, SpecialStat.Perception, comp.PerceptionBoost);
        TryModify(uid, SpecialStat.Endurance, comp.EnduranceBoost);
        TryModify(uid, SpecialStat.Charisma, comp.CharismaBoost);
        TryModify(uid, SpecialStat.Intelligence, comp.IntelligenceBoost);
        TryModify(uid, SpecialStat.Agility, comp.AgilityBoost);
        TryModify(uid, SpecialStat.Luck, comp.LuckBoost);

        _applied[uid] = signature;
    }

    private void TryModify(EntityUid uid, SpecialStat stat, int value)
    {
        if (value == 0)
            return;

        _special.TryModifyTemporary(uid, stat, value, null, ModifierSource);
    }

// ── Public API (called by SpecialStatBoostEffect / DrugSpecialTimelineSystem) ──────

    /// <summary>
    ///     Upserts a source contribution (keyed by reagent prototype for metabolism effects,
    ///     or <see cref="DrugSpecialBoostComponent.TimelineSourceKey"/> for regimen timelines)
    ///     with its own per-source grace window of <paramref name="lifetimeSeconds"/> seconds.
    ///     Only dirties state and reacts to an agility change when the resolved signature changes.
    /// </summary>
    public void SetSource(EntityUid uid, DrugSpecialBoostComponent comp, string sourceKey, in StatSignature signature, float lifetimeSeconds)
    {
        var now = _timing.CurTime;
        var expiry = comp.ComputeSourceExpiry(sourceKey, now, lifetimeSeconds);

        if (!comp.SetSource(sourceKey, now, expiry, signature))
            return;

        Dirty(uid, comp);
        NotifyIfAgilityChanged(uid, comp);
    }

    /// <summary>
    ///     Drops expired sources and reacts to a changed effective signature. Called from
    ///     Update() so a metabolised-away drug's fields stop contributing on time.
    /// </summary>
    public void Reconcile(EntityUid uid, DrugSpecialBoostComponent comp)
    {
        if (!comp.ExpireSources(_timing.CurTime))
            return;

        Dirty(uid, comp);
        NotifyIfAgilityChanged(uid, comp);
    }

    private void NotifyIfAgilityChanged(EntityUid uid, DrugSpecialBoostComponent comp)
    {
        if (comp.AgilityBoost == comp.LastResolvedAgility)
            return;

        comp.LastResolvedAgility = comp.AgilityBoost;
        _movement.RefreshMovementSpeedModifiers(uid);
    }
}
