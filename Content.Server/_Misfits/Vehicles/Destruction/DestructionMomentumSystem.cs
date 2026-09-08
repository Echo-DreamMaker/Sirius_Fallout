using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared._Misfits.Vehicles.Destruction;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Vehicles.Destruction;

// #Misfits Add - Ported from CMU (AU-14) PR #1816 "Gunship overhaul + vehicle movement
// overhaul". Converts an obstruction's remaining effective durability into an impact-speed
// cost, so aircraft and ground vehicles share one way to preserve the momentum left over
// after clearing an obstacle.
public sealed partial class DestructionMomentumSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DestructionMomentumQueryEvent>(OnMomentumQuery);
    }

    private void OnMomentumQuery(ref DestructionMomentumQueryEvent args)
    {
        args.HasRemovalThreshold = TryGetRemovalThreshold(args.Target, out var damageable, out var remainingDamage);
        if (!args.HasRemovalThreshold)
            return;

        if (remainingDamage <= 0f)
        {
            args.CanDestroy = true;
            return;
        }

        if (args.AvailableSpeed <= 0f || args.DamageMultiplier <= 0f)
            return;

        if (GetEffectiveDamage(damageable,
                args.AvailableSpeed * args.AvailableSpeed * args.DamageMultiplier) < remainingDamage)
        {
            return;
        }

        var low = 0f;
        var high = args.AvailableSpeed;
        for (var i = 0; i < 12; i++)
        {
            var middle = (low + high) * 0.5f;
            var rawDamage = middle * middle * args.DamageMultiplier;
            if (GetEffectiveDamage(damageable, rawDamage) >= remainingDamage)
                high = middle;
            else
                low = middle;
        }

        args.CanDestroy = true;
        args.RequiredSpeed = high;
    }

    /// <summary>
    /// Resolves whether the obstruction is destructible at the available speed and, if so,
    /// the minimum speed cost required to clear it.
    /// </summary>
    public bool TryGetBreakCost(
        EntityUid obstruction,
        float availableSpeed,
        float damageMultiplier,
        out float requiredSpeed)
    {
        var query = new DestructionMomentumQueryEvent(obstruction, availableSpeed, damageMultiplier);
        OnMomentumQuery(ref query);
        requiredSpeed = query.RequiredSpeed;
        return query.CanDestroy;
    }

    /// <summary>
    /// Spends a destruction cost from the same squared-speed budget used to calculate
    /// impact damage. Subtracting the required speed directly would discard too much
    /// kinetic energy, especially across multiple obstacles.
    /// </summary>
    public static float GetRemainingSpeed(float availableSpeed, float requiredSpeed)
    {
        var available = MathF.Max(0f, availableSpeed);
        var required = Math.Clamp(requiredSpeed, 0f, available);
        return MathF.Sqrt(MathF.Max(0f, available * available - required * required));
    }

    private bool TryGetRemovalThreshold(
        EntityUid obstruction,
        out DamageableComponent damageable,
        out float remainingDamage)
    {
        remainingDamage = 0f;
        if (!TryComp(obstruction, out damageable!) ||
            !TryComp(obstruction, out DestructibleComponent? destructible))
        {
            return false;
        }

        var destroyedAt = GetRemovalThreshold(destructible);
        if (destroyedAt == FixedPoint2.MaxValue)
            return false;

        remainingDamage = destroyedAt.Float() - damageable.TotalDamage.Float();
        return true;
    }

    /// <summary>
    /// Prefer actual destruction to breakage. Walls commonly break into a still-solid
    /// girder before their later destruction threshold.
    /// </summary>
    private static FixedPoint2 GetRemovalThreshold(DestructibleComponent destructible)
    {
        var destructionAt = FixedPoint2.MaxValue;
        var breakageAt = FixedPoint2.MaxValue;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is not DamageTrigger trigger)
                continue;

            foreach (var behavior in threshold.Behaviors)
            {
                if (behavior is not DoActsBehavior acts)
                    continue;

                if (acts.HasAct(ThresholdActs.Destruction))
                    destructionAt = FixedPoint2.Min(destructionAt, FixedPoint2.New(trigger.Damage));
                else if (acts.HasAct(ThresholdActs.Breakage))
                    breakageAt = FixedPoint2.Min(breakageAt, FixedPoint2.New(trigger.Damage));
            }
        }

        return destructionAt != FixedPoint2.MaxValue ? destructionAt : breakageAt;
    }

    private float GetEffectiveDamage(DamageableComponent damageable, float rawDamage)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = FixedPoint2.New(rawDamage);

        if (damageable.DamageModifierSetId != null &&
            _prototypes.TryIndex<DamageModifierSetPrototype>(damageable.DamageModifierSetId, out var modifierSet))
        {
            damage = DamageSpecifier.ApplyModifierSet(damage, modifierSet);
        }

        foreach (var extraSet in damageable.DamageModifierSets)
        {
            if (_prototypes.TryIndex<DamageModifierSetPrototype>(extraSet, out var resolvedSet))
                damage = DamageSpecifier.ApplyModifierSet(damage, resolvedSet);
        }

        return MathF.Max(0f, damage.GetTotal().Float());
    }
}
