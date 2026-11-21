using Content.Server._Sirius.Soap;
using Content.Shared._Sirius.Dirtiness;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server._Sirius.Dirtiness;

// #Sirius Add: reagent effects used by the SiriusDirty reactions and mob reactive entries.

[DataDefinition]
public sealed partial class SiriusMakeWetEffect : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("sirius-reagent-effect-wet");

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs)
            return;

        // #Sirius Add: 2 seconds per unit of water, capped at one minute by the system.
        var duration = TimeSpan.FromSeconds(2 * reagentArgs.Quantity.Float());
        args.EntityManager.System<SiriusDirtySystem>().MakeWet(args.TargetEntity, duration, accumulate: true);
    }
}

[DataDefinition]
public sealed partial class SiriusWashEffect : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("sirius-reagent-effect-wash");

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent<SiriusDirtyComponent>(args.TargetEntity, out var dirt))
            return;

        args.EntityManager.System<SiriusDirtySystem>().ApplyWashed(
            args.TargetEntity, dirt.PlainSoapPerSecond, dirt.PlainSoapCap, TimeSpan.FromSeconds(30));
    }
}

/// <summary>
/// #Sirius Add: marks a solution container as holding soap that still has to solidify.
/// </summary>
[DataDefinition]
public sealed partial class SiriusSolidifySoap : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("sirius-reagent-effect-solidify-soap");

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args.EntityManager.HasComponent<SiriusSoapSolidifyingComponent>(args.TargetEntity))
            return;

        args.EntityManager.AddComponent<SiriusSoapSolidifyingComponent>(args.TargetEntity);
    }
}
