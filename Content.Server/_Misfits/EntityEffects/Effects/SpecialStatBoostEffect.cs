// #Misfits Add - EntityEffect that installs DrugSpecialBoostComponent on a player each
// metabolism tick while a SPECIAL-boosting drug is being metabolised.
// Follows the same pattern as MovespeedModifier — each tick refreshes a timer so the
// effect persists for as long as the reagent remains in the bloodstream.
// Referenced from chems.yml as !type:SpecialStatBoostEffect.

using Content.Shared._Misfits.SpecialStats;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.EntityEffects.Effects;

/// <summary>
/// Applies a temporary SPECIAL stat boost to the target entity for each tick of reagent
/// metabolism. Non-zero boost fields produce in-game effects via <see cref="DrugSpecialBoostSystem"/>:
/// <list type="bullet">
///   <item><see cref="StrengthBoost"/>   — melee damage bonus (+4 % per point)</item>
///   <item><see cref="PerceptionBoost"/> — gun spread / recoil reduction (+0.5 % per point)</item>
///   <item><see cref="AgilityBoost"/>    — walk/sprint speed bonus (+1.5 % per point)</item>
///   <item><see cref="IntelligenceBoost"/>, <see cref="EnduranceBoost"/>, etc. — stored, reserved for future systems</item>
/// </list>
/// <para>
/// Set <see cref="StatusLifetime"/> to control how long the boost lingers after the last
/// metabolism tick (i.e. the "grace window" once the reagent amount hits zero).
/// </para>
/// </summary>
[UsedImplicitly]
public sealed partial class SpecialStatBoostEffect : EntityEffect
{
    [DataField] public int StrengthBoost;
    [DataField] public int PerceptionBoost;
    [DataField] public int EnduranceBoost;
    [DataField] public int CharismaBoost;
    [DataField] public int AgilityBoost;
    [DataField] public int IntelligenceBoost;
    [DataField] public int LuckBoost;

/// <summary>
    ///     Seconds the boost persists after the last metabolism tick.
    ///     Combined with the per-source push-forward expiry (<see cref="DrugSpecialBoostSystem.SetSource"/>)
    ///     this keeps the effect active for the full duration the reagent is in the bloodstream,
    ///     expiring shortly after.
    ///     Default: 4 s gives enough headroom for all standard metabolism rates.
    /// </summary>
    [DataField]
    public float StatusLifetime = 4f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var parts = new List<string>();
        if (StrengthBoost     != 0) parts.Add($"STR +{StrengthBoost}");
        if (PerceptionBoost   != 0) parts.Add($"PER +{PerceptionBoost}");
        if (EnduranceBoost    != 0) parts.Add($"END +{EnduranceBoost}");
        if (CharismaBoost     != 0) parts.Add($"CHA +{CharismaBoost}");
        if (AgilityBoost      != 0) parts.Add($"AGI +{AgilityBoost}");
        if (IntelligenceBoost != 0) parts.Add($"INT +{IntelligenceBoost}");
        if (LuckBoost         != 0) parts.Add($"LCK +{LuckBoost}");
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var uid  = args.TargetEntity;
        var comp = args.EntityManager.EnsureComponent<DrugSpecialBoostComponent>(uid);

        // Key the contribution by the reagent so simultaneous drugs each keep their own
        // window: when one is metabolised away it stops contributing without cancelling
        // the other drug's ongoing boost (a plain shared-field write left the flushed
        // drug's stats stuck on forever).
        var sourceKey = (args as EntityEffectReagentArgs)?.Reagent?.ID ?? "metabolism-boost";

        var signature = new StatSignature(
            StrengthBoost,
            PerceptionBoost,
            EnduranceBoost,
            CharismaBoost,
            IntelligenceBoost,
            AgilityBoost,
            LuckBoost);

        // Upsert + resolve through the boost system. Effective fields only dirty when they
        // actually change, so steady metabolism does not churn replicated state every tick.
        // Same for the movement-speed refresh: fired only when the resolved agility changes.
        args.EntityManager.System<DrugSpecialBoostSystem>()
            .SetSource(uid, comp, sourceKey, signature, StatusLifetime);
    }
}
