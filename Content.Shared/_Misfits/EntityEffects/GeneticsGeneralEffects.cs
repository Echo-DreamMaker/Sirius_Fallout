// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Linq;
using Content.Shared._Misfits.Genetics.Mutations;
using Content.Shared.CombatMode;
using Content.Shared.Dataset;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HeightAdjust;
using Content.Shared.Humanoid;
using Content.Shared.Random.Helpers;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared._Misfits.EntityEffects;

internal static class GeneticsEffects
{
    public static void Apply(EntityEffect effect, EntityEffectBaseArgs args)
    {
        if (effect.ShouldApply(args))
            effect.Effect(args);
    }

    public static void Apply(IEnumerable<EntityEffect> effects, EntityEffectBaseArgs args)
    {
        foreach (var effect in effects)
            Apply(effect, args);
    }
}

public sealed partial class RelayMutated : EntityEffect
{
    [DataField("effect", required: true)] public EntityEffect ChildEffect = default!;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args.EntityManager.TryGetComponent<MutationComponent>(args.TargetEntity, out var mutation) && mutation.Target is {} target)
            GeneticsEffects.Apply(ChildEffect, new EntityEffectBaseArgs(target, args.EntityManager));
    }
}

[DataDefinition]
public partial record struct WeightedEffect
{
    [DataField("effect", required: true)] public EntityEffect ChildEffect = default!;
    [DataField] public float Weight = 1f;
}

public sealed partial class WeightedRandomEffect : EntityEffect
{
    [DataField(required: true)] public List<WeightedEffect> Children = [];
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var total = Children.Sum(child => Math.Max(0f, child.Weight));
        if (total <= 0f)
            return;

        var roll = IoCManager.Resolve<IRobustRandom>().NextFloat() * total;
        foreach (var child in Children)
        {
            roll -= Math.Max(0f, child.Weight);
            if (roll <= 0f)
            {
                GeneticsEffects.Apply(child.ChildEffect, args);
                return;
            }
        }
    }
}

public sealed partial class PlaySoundEffect : EntityEffect
{
    [DataField(required: true)] public SoundSpecifier Sound = default!;
    [DataField] public bool Positional = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var audio = args.EntityManager.System<SharedAudioSystem>();
        if (Positional)
            audio.PlayPvs(Sound, args.EntityManager.GetComponent<TransformComponent>(args.TargetEntity).Coordinates);
        else
            audio.PlayPvs(Sound, args.TargetEntity);
    }
}

public sealed partial class StartUseDelay : EntityEffect
{
    [DataField] public string DelayId = "default";
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args.EntityManager.TryGetComponent<UseDelayComponent>(args.TargetEntity, out var delay))
            args.EntityManager.System<UseDelaySystem>().TryResetDelay((args.TargetEntity, delay), id: DelayId);
    }
}

public sealed partial class DropItems : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args) =>
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, new DropHandItemsEvent(), false);
}

public sealed partial class DropRandomItem : EntityEffect
{
    [DataField] public EntityEffect[] Effects = [];
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent<HandsComponent>(args.TargetEntity, out var hands))
            return;
        var handSystem = args.EntityManager.System<SharedHandsSystem>();
        var items = handSystem.EnumerateHeld(args.TargetEntity, hands).ToArray();
        if (items.Length == 0)
            return;
        var item = IoCManager.Resolve<IRobustRandom>().Pick(items);
        if (handSystem.TryDrop(args.TargetEntity, item, handsComp: hands))
            GeneticsEffects.Apply(Effects, new EntityEffectBaseArgs(item, args.EntityManager));
    }
}

public sealed partial class AttackSelf : EntityEffect
{
    [DataField] public bool UseHeld = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var em = args.EntityManager;
        if (!em.TryGetComponent<CombatModeComponent>(args.TargetEntity, out var combat))
            return;
        var hands = em.System<SharedHandsSystem>();
        var weapon = UseHeld ? hands.GetActiveItemOrSelf(args.TargetEntity) : args.TargetEntity;
        if (!em.TryGetComponent<MeleeWeaponComponent>(weapon, out var melee))
            return;
        var combatSystem = em.System<SharedCombatModeSystem>();
        var wasOn = combat.IsInCombatMode;
        combatSystem.SetInCombatMode(args.TargetEntity, true, combat);
        em.System<SharedMeleeWeaponSystem>().AttemptLightAttack(args.TargetEntity, weapon, melee, args.TargetEntity);
        combatSystem.SetInCombatMode(args.TargetEntity, wasOn, combat);
    }
}

// Trauma's implementation is intentionally a stub pending random directional gun/melee attacks.
public sealed partial class AttackOthers : EntityEffect
{
    [DataField] public bool UseHeld = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args) { }
}

public sealed partial class ThrowRandomly : EntityEffect
{
    [DataField] public float Speed = 10f;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var direction = IoCManager.Resolve<IRobustRandom>().NextAngle().ToVec();
        args.EntityManager.System<ThrowingSystem>().TryThrow(args.TargetEntity, direction, Speed, args.TargetEntity);
    }
}

public sealed partial class SetStanding : EntityEffect
{
    [DataField] public bool Standing = true;
    [DataField] public bool Force;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var system = args.EntityManager.System<StandingStateSystem>();
        if (Standing)
            system.Stand(args.TargetEntity, force: Force);
        else
            system.Down(args.TargetEntity);
    }
}

public sealed partial class Scale : EntityEffect
{
    [DataField(required: true)] public Vector2 Multiplier;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var current = Vector2.One;
        if (args.EntityManager.TryGetComponent<HumanoidAppearanceComponent>(args.TargetEntity, out var humanoid))
            current = new Vector2(humanoid.Width, humanoid.Height);
        args.EntityManager.System<HeightAdjustSystem>().SetScale(args.TargetEntity, Vector2.Multiply(current, Multiplier));
    }
}

public sealed partial class ScaleFixtures : EntityEffect
{
    [DataField(required: true)] public float Multiplier;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        // HeightAdjustSystem, used by Scale above, already scales Nuclear-14 fixtures.
        // Keeping this serialized compatibility effect prevents gigantism from scaling them twice.
    }
}

public sealed partial class Speak : EntityEffect
{
    [DataField(required: true)] public ProtoId<LocalizedDatasetPrototype> Id;
    [DataField] public LocId? Prefix;
    [DataField] public bool HideChat;
    [DataField] public LocId GuidebookText = "entity-effect-guidebook-speak";
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>().Index(Id);
        var text = IoCManager.Resolve<IRobustRandom>().Pick(proto);
        if (Prefix is {} prefix)
            text = Loc.GetString(prefix) + text;
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, new GeneticsSpeakEffectEvent(text, HideChat));
    }
}

public sealed class GeneticsSpeakEffectEvent(string message, bool hideChat) : EntityEventArgs
{
    public readonly string Message = message;
    public readonly bool HideChat = hideChat;
}

public sealed partial class RelayNearby : EntityEffect
{
    [DataField("effect", required: true)] public EntityEffect ChildEffect = default!;
    [DataField(required: true)] public string CompName = string.Empty;
    [DataField] public float Range = 5f;
    [DataField] public EntityWhitelist? Whitelist;
    [DataField] public EntityWhitelist? Blacklist;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var em = args.EntityManager;
        var whitelist = em.System<EntityWhitelistSystem>();
        foreach (var found in em.System<EntityLookupSystem>().GetEntitiesInRange(args.TargetEntity, Range))
        {
            if (found == args.TargetEntity || !whitelist.IsWhitelistPassOrNull(Whitelist, found) || !whitelist.IsBlacklistFailOrNull(Blacklist, found))
                continue;
            var registration = em.ComponentFactory.GetRegistration(CompName);
            if (!em.HasComponent(found, registration.Type))
                continue;
            GeneticsEffects.Apply(ChildEffect, new EntityEffectBaseArgs(found, em));
        }
    }
}

public sealed partial class HoldingItemCondition : EntityEffectCondition
{
    public override bool Condition(EntityEffectBaseArgs args) =>
        args.EntityManager.TryGetComponent<HandsComponent>(args.TargetEntity, out var hands) &&
        args.EntityManager.System<SharedHandsSystem>().EnumerateHeld(args.TargetEntity, hands).Any();
    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class InContainerCondition : EntityEffectCondition
{
    [DataField] public bool Inverted;
    public override bool Condition(EntityEffectBaseArgs args)
    {
        var result = args.EntityManager.System<SharedContainerSystem>().TryGetContainingContainer(args.TargetEntity, out _);
        return Inverted ? !result : result;
    }
    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class StandingCondition : EntityEffectCondition
{
    [DataField] public bool Inverted;
    public override bool Condition(EntityEffectBaseArgs args)
    {
        var result = args.EntityManager.TryGetComponent<StandingStateComponent>(args.TargetEntity, out var standing) && standing.Standing;
        return Inverted ? !result : result;
    }
    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class UseDelayCondition : EntityEffectCondition
{
    [DataField] public string DelayId = "default";
    [DataField] public bool Inverted;
    public override bool Condition(EntityEffectBaseArgs args)
    {
        var result = args.EntityManager.TryGetComponent<UseDelayComponent>(args.TargetEntity, out var delay) &&
                     !args.EntityManager.System<UseDelaySystem>().IsDelayed((args.TargetEntity, delay), DelayId);
        return Inverted ? !result : result;
    }
    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}
