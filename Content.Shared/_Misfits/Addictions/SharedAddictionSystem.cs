// #Misfits Change - Ported from Delta-V addiction system
using Content.Shared.Damage;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Addictions;

/// <summary>
///     Shared addiction system. Handles applying and suppressing addictions
///     via the StatusEffectsSystem. Server overrides provide update/popup logic.
/// </summary>
public abstract class SharedAddictionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly TimeSpan ExposureGap = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     Status effect key used for the addiction status.
    /// </summary>
    public ProtoId<StatusEffectPrototype> StatusEffectKey = "Addicted";

    /// <summary>
    ///     Server-side time bookkeeping for suppression windows.
    /// </summary>
    protected abstract void UpdateTime(EntityUid uid);

    /// <summary>
    ///     Attempts to apply an addiction to the entity.
    ///     If the entity already has the effect, extends its duration.
    ///     If the entity is not yet addicted, repeated exposures to the specific reagent are tracked
    ///     until the configured threshold is reached.
    ///     Calls <see cref="OnAddictionApplied"/> so the server can send drug-specific chat messages.
    /// </summary>
    /// <param name="drugId">Prototype ID of the addictive reagent. Null falls back to immediate addiction.</param>
    /// <param name="drugName">Localized name of the drug (e.g. "hydra"). Empty skips chat messages.</param>
    /// <param name="addictionThreshold">Number of exposures required before addiction starts.</param>
    /// <returns>
    ///     True if the entity is addicted after this call and withdrawal data should be updated.
    ///     False if the exposure was only recorded toward the threshold.
    /// </returns>
    public virtual bool TryApplyAddiction(
        EntityUid uid,
        float addictionTime,
        ProtoId<ReagentPrototype>? drugId = null,
        string drugName = "",
        int addictionThreshold = 4,
        FixedPoint2? currentQuantity = null,
        StatusEffectsComponent? status = null,
        bool permanent = false,
        bool? isNewExposure = null)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        UpdateTime(uid);

        // #Misfits Change /Tweak:/ Track whether this is a new addiction or an existing one deepening
        var isNew = !_statusEffects.HasStatusEffect(uid, StatusEffectKey, status);

        if (isNew)
        {
            var threshold = Math.Max(1, addictionThreshold);

            if (threshold > 1 && drugId != null)
            {
                var exposure = EnsureComp<AddictionExposureComponent>(uid);
                var reagentId = drugId.Value;
                var now = _timing.CurTime;

                // #Misfits Fix - When the caller uses MarkExposureAndIsNewDose for its per-dose
                // gate (DoseChance < 1), it just wrote LastSeenTimes THIS tick, so re-deriving
                // freshness from LastSeenTimes below would always read "not new" and the exposure
                // count could never reach its threshold (addiction never applied). Trust the
                // caller's verdict, and let the caller keep ownership of that tick's bookkeeping.
                var newExposure = isNewExposure ?? IsNewExposure(exposure, reagentId, currentQuantity, now);

                if (isNewExposure == null)
                {
                    exposure.LastSeenTimes[reagentId] = now;

                    if (currentQuantity != null)
                        exposure.LastSeenQuantities[reagentId] = currentQuantity.Value;
                }

                exposure.ExposureCounts.TryGetValue(reagentId, out var count);

                if (newExposure)
                    count++;

                if (count < threshold)
                {
                    exposure.ExposureCounts[reagentId] = count;
                    return false;
                }

                exposure.ExposureCounts.Remove(reagentId);
                exposure.LastSeenTimes.Remove(reagentId);
                exposure.LastSeenQuantities.Remove(reagentId);
            }
        }

        if (isNew)
        {
            var duration = permanent
                ? TimeSpan.FromDays(3650)
                : TimeSpan.FromSeconds(addictionTime);

            _statusEffects.TryAddStatusEffect<AddictedComponent>(
                uid,
                StatusEffectKey,
                duration,
                false,
                status);
        }
        else
        {
            _statusEffects.TryAddTime(uid, StatusEffectKey, TimeSpan.FromSeconds(addictionTime), status);
        }

        // Store drug name and increment dose count on the component
        if (TryComp<AddictedComponent>(uid, out var addicted))
        {
            if (!string.IsNullOrEmpty(drugName))
                addicted.DrugName = drugName;

            addicted.DoseCount++;
            addicted.Permanent |= permanent;
        }

        OnAddictionApplied(uid, isNew);
        return true;
    }

    // #Misfits Change /Add:/ Store per-drug withdrawal effect parameters on the component.
    /// <summary>
    ///     Stores withdrawal gameplay effect parameters on the entity's <see cref="AddictedComponent"/>.
    ///     Called by the <c>Addicting</c> reagent effect after <see cref="TryApplyAddiction"/>.
    ///     Only updates a field if the new value represents a stronger effect than what is already set,
    ///     preventing a milder drug from overriding a harsher one's withdrawal on a multi-drug user.
    /// </summary>
    public void SetWithdrawalEffects(
        EntityUid uid,
        string moodEffect = "",
        DamageSpecifier? damage = null,
        float speedPenalty = 1.0f,
        float staminaDrain = 0.0f)
    {
        if (!TryComp<AddictedComponent>(uid, out var addicted))
            return;

        // Only update mood if not already set (first drug setting mood wins across different drugs)
        if (!string.IsNullOrEmpty(moodEffect) && string.IsNullOrEmpty(addicted.WithdrawalMoodEffect))
            addicted.WithdrawalMoodEffect = moodEffect;

        // Take the highest damage value
        if (damage != null)
            addicted.WithdrawalDamage = damage;

        // Take the lowest (most punishing) speed penalty
        if (speedPenalty < addicted.WithdrawalSpeedPenalty)
            addicted.WithdrawalSpeedPenalty = speedPenalty;

        // Take the highest stamina drain
        if (staminaDrain > addicted.WithdrawalStaminaDrain)
            addicted.WithdrawalStaminaDrain = staminaDrain;
    }

    /// <summary>
    ///     Records the current metabolism observation and reports whether it represents a NEW dose of the
    ///     given reagent (first encounter, a gap longer than <paramref name="gap"/> seconds, or a quantity
    ///     increase). Used to gate per-dose addiction rolls so a continuous bloodstream presence collapses
    ///     into a single dose.
    /// </summary>
    public bool MarkExposureAndIsNewDose(
        EntityUid uid,
        ProtoId<ReagentPrototype>? drugId,
        FixedPoint2? currentQuantity,
        TimeSpan? gap = null)
    {
        if (drugId == null)
            return true;

        gap ??= ExposureGap;

        var exposure = EnsureComp<AddictionExposureComponent>(uid);
        var reagentId = drugId.Value;
        var now = _timing.CurTime;

        var isNew = !exposure.LastSeenTimes.TryGetValue(reagentId, out var lastSeen)
            || now - lastSeen > gap.Value;

        if (!isNew
            && currentQuantity != null
            && exposure.LastSeenQuantities.TryGetValue(reagentId, out var lastQuantity)
            && currentQuantity.Value > lastQuantity)
        {
            isNew = true;
        }

        exposure.LastSeenTimes[reagentId] = now;

        if (currentQuantity != null)
            exposure.LastSeenQuantities[reagentId] = currentQuantity.Value;

        return isNew;
    }

    /// <summary>
    ///     Called after addiction is applied or extended.
    ///     Server override uses this to send drug-specific chat messages based on dose count / severity.
    /// </summary>
    protected virtual void OnAddictionApplied(EntityUid uid, bool isNew) { }

    /// <summary>
    ///     Re-derives whether this metabolism observation counts as a NEW exposure of the given
    ///     reagent (first encounter, gap longer than <see cref="ExposureGap"/>, or a quantity
    ///     increase). Used to build an exposure count toward <see cref="TryApplyAddiction"/>'s
    ///     threshold. Does not write any bookkeeping.
    /// </summary>
    private bool IsNewExposure(
        AddictionExposureComponent exposure,
        ProtoId<ReagentPrototype> reagentId,
        FixedPoint2? currentQuantity,
        TimeSpan now)
    {
        var isNew = true;

        if (exposure.LastSeenTimes.TryGetValue(reagentId, out var lastSeen))
            isNew = now - lastSeen > ExposureGap;

        if (!isNew
            && currentQuantity != null
            && exposure.LastSeenQuantities.TryGetValue(reagentId, out var lastQuantity)
            && currentQuantity.Value > lastQuantity)
        {
            isNew = true;
        }

        return isNew;
    }

    /// <summary>
    ///     Suppresses active addiction symptoms for a duration.
    /// </summary>
    public virtual void TrySuppressAddiction(EntityUid uid, float duration)
    {
        if (!TryComp<AddictedComponent>(uid, out var addicted))
            return;

        UpdateAddictionSuppression(uid, addicted, duration);
    }

    /// <summary>
    ///     Marks the addiction as suppressed and sets the suppression end time.
    ///     #Misfits Fix - the server re-derives <see cref="AddictedComponent.Suppressed"/> from
    ///     <see cref="AddictedComponent.SuppressionEndTime"/> every tick, so a suppression that
    ///     only toggled the flag without opening a window was undone on the very next tick
    ///     (Fixer / methadone had no effect).
    /// </summary>
    protected void UpdateAddictionSuppression(EntityUid uid, AddictedComponent component, float duration)
    {
        component.SuppressionEndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);
        component.Suppressed = true;
        Dirty(uid, component);
    }
}
