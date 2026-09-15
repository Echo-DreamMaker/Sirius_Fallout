// #Misfits Add - EntityEffect that installs DrugSpecialTimelineComponent on a player
// each metabolism tick while a phased drug is being metabolised.
// The effect is idempotent per reagent: if a timeline for the same reagent already
// exists it is left unchanged; a different reagent replaces the active timeline.
// Referenced from chems.yml as !type:SpecialStatTimelineEffect.

using Content.Shared._Misfits.SpecialStats;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.EntityEffects.Effects;

/// <summary>
///     Installs a multi-phase drug timeline on the target entity.
///     Phases are authored inline in YAML and describe absolute stat values
///     (not deltas) that replace the previous phase on each transition.
/// </summary>
[UsedImplicitly]
public sealed partial class SpecialStatTimelineEffect : EntityEffect
{
    /// <summary>
    ///     Ordered list of phases. Each phase specifies absolute stat values
    ///     and an optional damage resistance set id.
    /// </summary>
    [DataField]
    public List<DrugTimelinePhase> Phases = new();

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var parts = new List<string>();
        foreach (var phase in Phases)
        {
            var p = $"@{phase.Time:F0}s: ";
            var fields = new List<string>();
            if (phase.Strength     != 0) fields.Add($"STR {phase.Strength:+#;-#;0}");
            if (phase.Perception   != 0) fields.Add($"PER {phase.Perception:+#;-#;0}");
            if (phase.Endurance    != 0) fields.Add($"END {phase.Endurance:+#;-#;0}");
            if (phase.Charisma     != 0) fields.Add($"CHA {phase.Charisma:+#;-#;0}");
            if (phase.Agility      != 0) fields.Add($"AGI {phase.Agility:+#;-#;0}");
            if (phase.Intelligence != 0) fields.Add($"INT {phase.Intelligence:+#;-#;0}");
            if (phase.Luck         != 0) fields.Add($"LCK {phase.Luck:+#;-#;0}");
            if (!string.IsNullOrEmpty(phase.DamageResistSet)) fields.Add($"DR:{phase.DamageResistSet}");
            if (fields.Count > 0)
                p += string.Join(", ", fields);
            else
                p += "baseline";
            parts.Add(p);
        }
        return parts.Count > 0 ? string.Join(" → ", parts) : null;
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var uid  = args.TargetEntity;
        var entMan = args.EntityManager;
        var comp = entMan.EnsureComponent<DrugSpecialTimelineComponent>(uid);

        // Determine the reagent id + current blood quantity.
        string? reagentId = null;
        FixedPoint2 currentQuantity = FixedPoint2.Zero;
        if (args is EntityEffectReagentArgs reagentArgs)
        {
            reagentId = reagentArgs.Reagent?.ID;

            if (reagentArgs.Reagent != null
                && reagentArgs.Source != null
                && reagentArgs.Source.TryGetReagent(new ReagentId(reagentArgs.Reagent.ID, null), out var reagentQuantity))
            {
                currentQuantity = reagentQuantity.Quantity;
            }
        }

        var now = IoCManager.Resolve<IGameTiming>().CurTime;

        var isNewDose = comp.LastSeen == default
            || now - comp.LastSeen > NewDoseGap
            || currentQuantity > comp.LastQuantity;

        // Record this observation before deciding, so a continuous bloodstream
        // presence collapses into a single dose.
        comp.LastSeen = now;
        if (currentQuantity > FixedPoint2.Zero)
            comp.LastQuantity = currentQuantity;
        entMan.Dirty(uid, comp);

        // Install or reset the timeline only for a genuinely new dose.
        if (string.IsNullOrEmpty(comp.ReagentId) || comp.ReagentId != reagentId || isNewDose)
        {
            comp.ReagentId  = reagentId ?? string.Empty;
            comp.Anchor     = now;
            comp.PhaseIndex = 0;
            comp.Phases     = new List<DrugTimelinePhase>(Phases);
            comp.ActiveResistSet = null;

            entMan.Dirty(uid, comp);
        }

        // The system's Update() handles phase advancement, writing stat values,
        // damage resistance management, and component removal.
    }

    /// <summary>
    ///     Gap between the drug clearing the system and a later dose being treated as a NEW dose.
    /// </summary>
    private static readonly TimeSpan NewDoseGap = TimeSpan.FromSeconds(10);
}
