// #Misfits Add - Temporary SPECIAL stat boosts applied by drug reagents during metabolism.
// Set and expired by DrugSpecialBoostSystem; written each tick by SpecialStatBoostEffect.
// NetworkedComponent so client-side prediction can apply the in-progress effects locally.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
///     Full 7-stat drug boost signature. Used both as a per-source contribution
///     (<see cref="DrugBoostSource"/>) and as the resolved effective signature.
/// </summary>
public readonly record struct StatSignature(
    int Strength,
    int Perception,
    int Endurance,
    int Charisma,
    int Intelligence,
    int Agility,
    int Luck);

/// <summary>
///     One active drug's contribution to the SPECIAL boost. Tracks its own expiry so a
///     metabolised-away drug stops contributing even while a different drug keeps ticking
///     (previously the shared component kept the flushed drug's fields alive forever).
///     Runtime-only bookkeeping — never serialized or networked.
/// </summary>
public sealed class DrugBoostSource
{
    /// <summary>Cached expiry of this source's last metabolism tick.</summary>
    public TimeSpan ExpireTime;

    /// <summary>Last write time — resolves per-stat contention: the newest source wins.</summary>
    public TimeSpan Updated;

    public int Strength;
    public int Perception;
    public int Endurance;
    public int Charisma;
    public int Intelligence;
    public int Agility;
    public int Luck;

    public bool Matches(in StatSignature signature)
    {
        return Strength == signature.Strength
            && Perception == signature.Perception
            && Endurance == signature.Endurance
            && Charisma == signature.Charisma
            && Intelligence == signature.Intelligence
            && Agility == signature.Agility
            && Luck == signature.Luck;
    }
}

/// <summary>
///     Added to a player entity while a SPECIAL-boosting drug is being metabolised.
///     The seven boost fields expose the RESOLVED effective delta for each stat: the
///     combination of every active source in <see cref="Sources"/>, so a drug that has been
///     metabolised away stops contributing the moment its per-drug grace window passes,
///     even while another drug is still being metabolised.
///     <see cref="Sources"/> is not networked: clients resolve the same effective fields
///     locally, while the server owns expiry and the mirrored temporary modifiers.
///     Managed (refreshed, expired) by <see cref="DrugSpecialBoostSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DrugSpecialBoostComponent : Component
{
    /// <summary>Source key used by <c>DrugSpecialTimelineSystem</c> for its phase signature.</summary>
    public const string TimelineSourceKey = "timeline";

    /// <summary>Temporary Strength delta — scales melee damage bonus via DrugSpecialBoostSystem.</summary>
    [DataField, AutoNetworkedField]
    public int StrengthBoost;

    /// <summary>Temporary Perception delta — reduces gun spread/recoil (stacks on SpecialPerceptionSystem).</summary>
    [DataField, AutoNetworkedField]
    public int PerceptionBoost;

    /// <summary>Temporary Endurance delta — stored for future dynamic stamina effects.</summary>
    [DataField, AutoNetworkedField]
    public int EnduranceBoost;

    /// <summary>Temporary Charisma delta — stored for future dialogue/barter effects.</summary>
    [DataField, AutoNetworkedField]
    public int CharismaBoost;

    /// <summary>Temporary Agility delta — adds movement speed bonus via DrugSpecialBoostSystem.</summary>
    [DataField, AutoNetworkedField]
    public int AgilityBoost;

    /// <summary>Temporary Intelligence delta — stored for future crafting/XP effects.</summary>
    [DataField, AutoNetworkedField]
    public int IntelligenceBoost;

    /// <summary>Temporary Luck delta — stored for future loot/crit effects.</summary>
    [DataField, AutoNetworkedField]
    public int LuckBoost;

    /// <summary>Game-time at which all boost effects expire (max over the active sources).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ExpireTime = TimeSpan.Zero;

    /// <summary>
    ///     Per-source active contributions keyed by reagent prototype ID (metabolism effects)
    ///     or <see cref="TimelineSourceKey"/> (regimen timelines). Runtime-only bookkeeping:
    ///     not serialized/networked — the effective fields above are authoritative on the server.
    /// </summary>
    public Dictionary<string, DrugBoostSource> Sources = new();

    /// <summary>
    ///     Effective agility from the last resolution; used to fire one-shot reactions
    ///     (movement speed refresh) exactly when the effective value changes.
    /// </summary>
    public int LastResolvedAgility = int.MinValue;

    /// <summary>
    ///     Computes the expiry for <paramref name="sourceKey"/> relative to its own current
    ///     window (never shortened), so the grace period belongs to the individual drug
    ///     rather than the whole component.
    /// </summary>
    public TimeSpan ComputeSourceExpiry(string sourceKey, TimeSpan now, float lifetimeSeconds)
    {
        if (Sources.TryGetValue(sourceKey, out var source) && source.ExpireTime > now)
            return source.ExpireTime + TimeSpan.FromSeconds(lifetimeSeconds);

        return now + TimeSpan.FromSeconds(lifetimeSeconds);
    }

    /// <summary>
    ///     Upserts a source contribution and re-resolves the effective fields.
    ///     Returns true if the resolved signature changed (i.e. a mirror + state write is needed).
    /// </summary>
    public bool SetSource(string sourceKey, TimeSpan now, TimeSpan expiry, in StatSignature signature)
    {
        var hasSource = Sources.TryGetValue(sourceKey, out var source);

        if (!hasSource)
        {
            source = new DrugBoostSource();
            Sources[sourceKey] = source;
        }

        source!.ExpireTime = expiry;
        source.Updated = now;
        source.Strength = signature.Strength;
        source.Perception = signature.Perception;
        source.Endurance = signature.Endurance;
        source.Charisma = signature.Charisma;
        source.Intelligence = signature.Intelligence;
        source.Agility = signature.Agility;
        source.Luck = signature.Luck;

        return Resolve(now);
    }

    /// <summary>
    ///     Removes expired sources (a drug whose grace window passed without another metabolism
    ///     tick) and re-resolves. Returns true if the resolved signature changed.
    /// </summary>
    public bool ExpireSources(TimeSpan now)
    {
        if (Sources.Count == 0)
            return false;

        // Collect before removing — mutating the dictionary inside a foreach is illegal.
        var toRemove = new List<string>(Sources.Count);
        foreach (var (key, source) in Sources)
        {
            if (source.ExpireTime <= now)
                toRemove.Add(key);
        }

        if (toRemove.Count == 0)
            return false;

        foreach (var key in toRemove)
            Sources.Remove(key);

        return Resolve(now);
    }

    /// <summary>
    ///     Recomputes the effective fields from the active sources and derives the component
    ///     expiry. Returns true if the effective signature changed.
    /// </summary>
    private bool Resolve(TimeSpan now)
    {
        var resolved = new StatSignature(
            Pick(now, s => s.Strength),
            Pick(now, s => s.Perception),
            Pick(now, s => s.Endurance),
            Pick(now, s => s.Charisma),
            Pick(now, s => s.Intelligence),
            Pick(now, s => s.Agility),
            Pick(now, s => s.Luck));

        var previous = new StatSignature(
            StrengthBoost,
            PerceptionBoost,
            EnduranceBoost,
            CharismaBoost,
            IntelligenceBoost,
            AgilityBoost,
            LuckBoost);

        StrengthBoost = resolved.Strength;
        PerceptionBoost = resolved.Perception;
        EnduranceBoost = resolved.Endurance;
        CharismaBoost = resolved.Charisma;
        IntelligenceBoost = resolved.Intelligence;
        AgilityBoost = resolved.Agility;
        LuckBoost = resolved.Luck;

        ExpireTime = TimeSpan.Zero;
        foreach (var source in Sources.Values)
        {
            if (source.ExpireTime > ExpireTime)
                ExpireTime = source.ExpireTime;
        }

        return resolved != previous;
    }

    /// <summary>
    ///     Picks the per-stat winner among active (unexpired) sources: the most recently
    ///     updated source with a non-zero value. Zero means "no contribution", so a stat
    ///     only resolves to a boost while at least one active drug contributes to it.
    /// </summary>
    private int Pick(TimeSpan now, Func<DrugBoostSource, int> selector)
    {
        DrugBoostSource? best = null;

        foreach (var source in Sources.Values)
        {
            if (source.ExpireTime <= now || selector(source) == 0)
                continue;

            if (best == null || source.Updated > best.Updated)
                best = source;
        }

        return best == null ? 0 : selector(best);
    }
}