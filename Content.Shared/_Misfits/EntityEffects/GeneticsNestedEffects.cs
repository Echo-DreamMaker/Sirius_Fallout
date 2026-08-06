// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.Whitelist;

namespace Content.Shared._Misfits.EntityEffects;

public sealed partial class NestedEffect : EntityEffect
{
    [DataField(required: true)]
    public ProtoId<GeneticsEntityEffectPrototype> Proto;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>().Index(Proto);
        foreach (var condition in proto.Conditions)
        {
            if (!condition.Condition(args))
                return;
        }

        foreach (var effect in proto.Effects)
        {
            if (effect.ShouldApply(args))
                effect.Effect(args);
        }
    }
}

public sealed partial class NestedCondition : EntityEffectCondition
{
    [DataField(required: true)]
    public ProtoId<GeneticsEntityConditionPrototype> Proto;

    [DataField]
    public bool Inverted;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        var result = IoCManager.Resolve<IPrototypeManager>().Index(Proto).Condition.Condition(args);
        return Inverted ? !result : result;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class WhitelistCondition : EntityEffectCondition
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public bool Inverted;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        var system = args.EntityManager.System<EntityWhitelistSystem>();
        var result = system.IsWhitelistPassOrNull(Whitelist, args.TargetEntity) &&
                     system.IsBlacklistFailOrNull(Blacklist, args.TargetEntity);
        return Inverted ? !result : result;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}
