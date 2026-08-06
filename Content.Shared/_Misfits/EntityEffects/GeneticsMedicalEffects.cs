// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Gibbing.Events;
using Content.Shared.Gibbing.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Map;

namespace Content.Shared._Misfits.EntityEffects;

public sealed partial class ModifyStatusEffect : EntityEffect
{
    [DataField(required: true)] public string EffectProto = string.Empty;
    [DataField] public float Time;
    [DataField] public bool Refresh = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var (key, component) = EffectProto switch
        {
            "StatusEffectForcedSleeping" => ("ForcedSleep", "ForcedSleeping"),
            "StatusEffectBlurryVision" => ("TemporaryBlindness", "TemporaryBlindness"),
            "StatusEffectBlindness" => ("TemporaryBlindness", "TemporaryBlindness"),
            _ => (EffectProto, EffectProto),
        };
        args.EntityManager.System<StatusEffectsSystem>()
            .TryAddStatusEffect(args.TargetEntity, key, TimeSpan.FromSeconds(Time), Refresh, component);
    }
}

public sealed partial class ModifyKnockdown : EntityEffect
{
    [DataField] public float Time;
    [DataField] public bool Refresh = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args) =>
        args.EntityManager.System<SharedStunSystem>().TryKnockdown(args.TargetEntity, TimeSpan.FromSeconds(Time), Refresh);
}

public sealed partial class Knockdown : EntityEffect
{
    [DataField] public float Time = 2f;
    [DataField] public bool Refresh = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args) =>
        args.EntityManager.System<SharedStunSystem>().TryKnockdown(args.TargetEntity, TimeSpan.FromSeconds(Time), Refresh);
}

public sealed partial class ModifyParalysis : EntityEffect
{
    [DataField] public float Time;
    [DataField] public bool Refresh = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args) =>
        args.EntityManager.System<SharedStunSystem>().TryParalyze(args.TargetEntity, TimeSpan.FromSeconds(Time), Refresh);
}

public sealed partial class Flammable : EntityEffect
{
    [DataField] public float Multiplier = 1f;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args) =>
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, new GeneticsFlammableEffectEvent(Multiplier));
}

public sealed class GeneticsFlammableEffectEvent(float multiplier) : EntityEventArgs
{
    public readonly float Multiplier = multiplier;
}

public sealed partial class EyeDamage : EntityEffect
{
    [DataField] public int Amount = -1;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args.EntityManager.TryGetComponent<BlindableComponent>(args.TargetEntity, out var blindable))
            args.EntityManager.System<BlindableSystem>().AdjustEyeDamage((args.TargetEntity, blindable), Amount);
    }
}

public sealed partial class SpawnEntity : EntityEffect
{
    [DataField(required: true)] public EntProtoId Entity;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var coordinates = args.EntityManager.GetComponent<TransformComponent>(args.TargetEntity).Coordinates;
        args.EntityManager.SpawnEntity(Entity, coordinates);
    }
}

public sealed partial class Gib : EntityEffect
{
    [DataField] public GibType GibType = GibType.Gib;
    [DataField] public GibContentsOption GibContents = GibContentsOption.Drop;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args) =>
        args.EntityManager.System<GibbingSystem>().TryGibEntity(args.TargetEntity, args.TargetEntity, GibType, GibContents, out _);
}
