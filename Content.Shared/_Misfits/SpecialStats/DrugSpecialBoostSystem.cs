// #Misfits Add - Manages DrugSpecialBoostComponent lifecycle (expiry) and translates
// drug-sourced SPECIAL stat deltas into REAL temporary modifiers on
// SpecialComponent.Temporary*Modifier via SharedSpecialSystem. Because the base
// SPECIAL consumers (movement, spread, melee, requirements, crafting gates) read
// effective stats, boosting the temporary modifiers makes the actual characteristics
// increase instead of layering on parallel event micro-effects.
// Component expiry is polled every update; on removal the temporary modifiers are cleared.

using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
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

    /// <summary>Source tag so drug-sourced modifiers can be removed without touching other effects.</summary>
    public const string ModifierSource = "drug-special";

    // Maintained across frames for O(n) expiry checking without per-tick querying.
    private readonly List<Entity<DrugSpecialBoostComponent>> _tracked = new();

    // Last-applied signature per entity so we only rewrite SpecialComponent when a
    // drug phase/new dose actually changes the boost values (avoids per-frame churn).
    private readonly Dictionary<EntityUid, StatSignature> _applied = new();

    private readonly record struct StatSignature(
        int Strength,
        int Perception,
        int Endurance,
        int Charisma,
        int Intelligence,
        int Agility,
        int Luck);

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

    // ── Public API (called by SpecialStatBoostEffect each metabolism tick) ──────

    /// <summary>
    /// Pushes the expiry window forward by <paramref name="lifetimeSeconds"/> seconds
    /// relative to the current time (or the existing timer, whichever is later).
    /// This keeps the boost alive as long as the drug is still being metabolised.
    /// </summary>
    public void RefreshTimer(EntityUid uid, DrugSpecialBoostComponent comp, float lifetimeSeconds)
    {
        // Take the later of 'now' or the current timer so we never shorten an existing window.
        var baseSeconds = Math.Max(comp.ExpireTime.TotalSeconds, _timing.CurTime.TotalSeconds);
        comp.ExpireTime = TimeSpan.FromSeconds(baseSeconds + lifetimeSeconds);
        Dirty(uid, comp);
    }
}
