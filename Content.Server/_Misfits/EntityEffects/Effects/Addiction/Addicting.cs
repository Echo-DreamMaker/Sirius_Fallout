// #Misfits Change - Ported from Delta-V addiction system
using Content.Shared._Misfits.Addictions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.EntityEffects.Effects.Addiction;

/// <summary>
///     Reagent effect that applies an addiction to the entity.
///     Duration scales with reagent quantity.
///     Also registers per-drug withdrawal effects (mood, damage, speed, stamina)
///     that are applied by <see cref="Content.Server._Misfits.Addictions.AddictionSystem"/>
///     while the entity is in active withdrawal (not suppressed).
/// </summary>
[UsedImplicitly]
public sealed partial class Addicting : EntityEffect
{
    /// <summary>
    ///     Base addiction time in seconds per 1u of reagent metabolized.
    /// </summary>
    [DataField]
    public float Time = 5f;

    // #Misfits Change /Add:/ Per-drug withdrawal effect parameters.
    // These are authored in chems.yml on each !type:Addicting block.

    /// <summary>
    ///     Mood prototype ID to apply during active withdrawal.
    ///     Leave empty for drugs that handle their own mood via ChemAddMoodlet (e.g. Jet/MovespeedMixture).
    /// </summary>
    [DataField]
    public string WithdrawalMoodEffect = string.Empty;

    /// <summary>
    ///     Damage applied periodically while in active withdrawal.
    ///     Thematically opposite to the drug's benefit (e.g. a healing drug causes damage on withdrawal).
    /// </summary>
    [DataField]
    public DamageSpecifier? WithdrawalDamage;

    /// <summary>
    ///     Multiplicative movement speed penalty while in active withdrawal (1.0 = none).
    ///     Used for stimulant drugs whose absence causes lethargy.
    /// </summary>
    [DataField]
    public float WithdrawalSpeedPenalty = 1.0f;

    /// <summary>
    ///     Stamina damage applied periodically while in active withdrawal.
    ///     0.0 = none.
    /// </summary>
    [DataField]
    public float WithdrawalStaminaDrain = 0.0f;

    /// <summary>
    ///     Number of exposures to this specific drug required before a full addiction is applied.
    ///     Values less than or equal to 1 preserve the old first-use behavior.
    /// </summary>
    [DataField]
    public int AddictionThreshold = 4;

    /// <summary>
    ///     Chance (0..1) that a single NEW dose of the drug causes an addiction.
    ///     The roll happens once per dose (not per metabolism tick) when this is set below 1.
    ///     A continuous bloodstream presence is collapsed into a single dose.
    /// </summary>
    [DataField]
    public float DoseChance = 1.0f;

    /// <summary>
    ///     If true, the resulting addiction is permanent (never expires on its own),
    ///     used e.g. for Jet.
    /// </summary>
    [DataField]
    public bool Permanent;

    /// <summary>
    ///     Maximum time between drug observations for them to be counted as the SAME dose.
    ///     Mirrors SharedAddictionSystem.ExposureGap.
    /// </summary>
    private const float DoseGap = 15f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var addictionSys = args.EntityManager.EntitySysManager.GetEntitySystem<SharedAddictionSystem>();

        var time = Time;
        ProtoId<ReagentPrototype>? drugId = null;
        FixedPoint2? currentQuantity = null;

        // #Misfits Change /Tweak:/ Pass the reagent's localized name so chat messages can name the specific drug
        var drugName = string.Empty;
        if (args is EntityEffectReagentArgs reagentArgs)
        {
            time *= reagentArgs.Scale.Float();
            drugId = reagentArgs.Reagent?.ID;
            drugName = reagentArgs.Reagent?.LocalizedName ?? string.Empty;

            if (reagentArgs.Reagent != null
                && reagentArgs.Source != null
                && reagentArgs.Source.TryGetReagent(new ReagentId(reagentArgs.Reagent.ID, null), out var reagentQuantity))
            {
                currentQuantity = reagentQuantity.Quantity;
            }
        }

        // #Misfits Change /Add:/ Per-dose chance gate — roll DoseChance once per NEW dose,
        // not on every metabolism tick.
        bool? isNewExposure = null;
        if (DoseChance < 1.0f)
        {
            if (!addictionSys.MarkExposureAndIsNewDose(args.TargetEntity, drugId, currentQuantity, TimeSpan.FromSeconds(DoseGap)))
                return;

            // #Misfits Fix - relay the "new dose" verdict to TryApplyAddiction. Without it
            // the addiction tracker re-reads LastSeenTimes (just written by the gate) and
            // concludes the tick is NOT a fresh exposure, so the count never advances and
            // an addiction with threshold > 1 and DoseChance < 1 can never be applied.
            isNewExposure = true;

            if (!IoCManager.Resolve<IRobustRandom>().Prob(DoseChance))
                return;
        }

        if (!addictionSys.TryApplyAddiction(
                args.TargetEntity,
                time,
                drugId,
                drugName,
                AddictionThreshold,
                currentQuantity,
                permanent: Permanent,
                isNewExposure: isNewExposure))
            return;

        // #Misfits Change /Add:/ Register withdrawal parameters (strongest values win on multi-drug)
        addictionSys.SetWithdrawalEffects(
            args.TargetEntity,
            WithdrawalMoodEffect,
            WithdrawalDamage,
            WithdrawalSpeedPenalty,
            WithdrawalStaminaDrain);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-addicted", ("chance", Probability));
    }
}
