using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Prototypes;

namespace Content.Server._N14.Special;

/// <summary>
/// Grants damage resistance to Poison and Radiation based on Endurance.
/// Above-average Endurance reduces the damage; below-average increases it.
/// Scalability follows the curved SPECIAL scale via <see cref="SharedSpecialSystem"/>.
/// </summary>
public sealed class EnduranceResistSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    private void OnBeforeDamage(EntityUid uid, DamageableComponent component, ref BeforeDamageChangedEvent args)
    {
        if (!_special.UsesSpecialStats(uid) || _special.GetEffective(uid, SpecialStat.Endurance) <= 5)
            return;

        if (args.Damage.DamageDict.TryGetValue("Poison", out var poison) && poison > FixedPoint2.Zero)
        {
            var tuning = _special.GetTuning();
            var factor = _special.GetEnduranceResistanceFraction(uid, tuning.EndurancePoisonResistancePerPoint);
            if (factor > 0f)
                args.Damage.DamageDict["Poison"] = FixedPoint2.Max(FixedPoint2.Zero, poison * (1f - factor));
        }

        if (args.Damage.DamageDict.TryGetValue("Radiation", out var radiation) && radiation > FixedPoint2.Zero)
        {
            var tuning = _special.GetTuning();
            var factor = _special.GetEnduranceResistanceFraction(uid, tuning.EnduranceRadiationResistancePerPoint);
            if (factor > 0f)
                args.Damage.DamageDict["Radiation"] = FixedPoint2.Max(FixedPoint2.Zero, radiation * (1f - factor));
        }
    }
}
