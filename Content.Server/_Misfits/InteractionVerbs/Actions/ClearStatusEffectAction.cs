using Content.Shared.InteractionVerbs;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server._Misfits.InteractionVerbs.Actions;

/// <summary>
///     Fully removes a status effect from the target, unlike ModifyStatusEffectAction
///     which only adds/subtracts time.
/// </summary>
[Serializable]
public sealed partial class ClearStatusEffectAction : InteractionAction
{
    [DataField(required: true)]
    public ProtoId<StatusEffectPrototype> Effect;

    public override bool IsAllowed(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {
        var statusEffects = deps.EntMan.System<StatusEffectsSystem>();
        return statusEffects.HasStatusEffect(args.Target, Effect);
    }

    public override bool CanPerform(InteractionArgs args, InteractionVerbPrototype proto, bool isBefore, VerbDependencies deps)
    {
        var statusEffects = deps.EntMan.System<StatusEffectsSystem>();
        return statusEffects.HasStatusEffect(args.Target, Effect);
    }

    public override bool Perform(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {
        var statusEffects = deps.EntMan.System<StatusEffectsSystem>();
        return statusEffects.TryRemoveStatusEffect(args.Target, Effect);
    }
}
