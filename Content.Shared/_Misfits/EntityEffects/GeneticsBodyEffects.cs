// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Shared._Misfits.EntityEffects;

public sealed partial class RelayBodyParts : EntityEffect
{
    [DataField] public BodyPartType? PartType;
    [DataField] public BodyPartSymmetry? Symmetry;
    [DataField] public LocId? GuidebookText;
    [DataField(required: true)] public EntityEffect[] Effects = [];
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.HasComponent<BodyComponent>(args.TargetEntity))
            return;
        var body = args.EntityManager.System<SharedBodySystem>();
        var parts = PartType is {} type
            ? body.GetBodyChildrenOfType(args.TargetEntity, type, symmetry: Symmetry)
            : body.GetBodyChildren(args.TargetEntity);
        foreach (var part in parts.ToArray())
            GeneticsEffects.Apply(Effects, new EntityEffectBaseArgs(part.Id, args.EntityManager));
    }
}

public sealed partial class RelayRandomPart : EntityEffect
{
    [DataField(required: true)] public BodyPartType[] Types = [];
    [DataField] public BodyPartSymmetry? PartSymmetry;
    [DataField("effect", required: true)] public EntityEffect ChildEffect = default!;
    [DataField] public EntityEffect? FailEffect;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var body = args.EntityManager.System<SharedBodySystem>();
        var parts = Types.SelectMany(type => body.GetBodyChildrenOfType(args.TargetEntity, type, symmetry: PartSymmetry))
            .Select(part => part.Id).ToArray();
        if (parts.Length == 0)
        {
            if (FailEffect is {} fail)
                GeneticsEffects.Apply(fail, args);
            return;
        }
        GeneticsEffects.Apply(ChildEffect, new EntityEffectBaseArgs(IoCManager.Resolve<IRobustRandom>().Pick(parts), args.EntityManager));
    }
}

public sealed partial class DetachOrgan : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var containers = args.EntityManager.System<SharedContainerSystem>();
        if (containers.TryGetContainingContainer(args.TargetEntity, out var container))
            containers.Remove(args.TargetEntity, container);
    }
}

public sealed partial class AddOrganSlot : EntityEffect
{
    [DataField(required: true)] public string Category = string.Empty;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var body = args.EntityManager.System<SharedBodySystem>();
        if (Enum.TryParse<BodyPartType>(Category, true, out var partType))
            body.TryCreatePartSlot(args.TargetEntity, Category.ToLowerInvariant(), partType, out _);
        else
            body.TryCreateOrganSlot(args.TargetEntity, Category.ToLowerInvariant(), out _);
    }
}

public sealed partial class RemoveOrganSlot : EntityEffect
{
    [DataField(required: true)] public string Slot = string.Empty;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent<BodyPartComponent>(args.TargetEntity, out var part))
            return;
        var key = Slot.ToLowerInvariant();
        part.Organs.Remove(key);
        part.Children.Remove(key);
        args.EntityManager.Dirty(args.TargetEntity, part);
    }
}

public sealed partial class MoveOrgan : EntityEffect
{
    [DataField(required: true)] public string Organ = string.Empty;
    [DataField(required: true)] public string Dest = string.Empty;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var em = args.EntityManager;
        var body = em.System<SharedBodySystem>();
        if (!Enum.TryParse<BodyPartType>(Dest, true, out var type))
            return;
        var destination = body.GetBodyChildrenOfType(args.TargetEntity, type).FirstOrDefault();
        if (destination.Id == EntityUid.Invalid)
            return;
        (EntityUid Id, OrganComponent Component) organ = default;
        foreach (var found in body.GetBodyOrgans(args.TargetEntity))
        {
            var foundSlot = found.Component.SlotId;
            if (!string.Equals(foundSlot, Organ, StringComparison.OrdinalIgnoreCase))
                continue;
            organ = found;
            break;
        }
        if (organ.Id == EntityUid.Invalid)
            return;
        var slot = Organ.ToLowerInvariant();
        body.TryCreateOrganSlot(destination.Id, slot, out _);
        if (body.RemoveOrgan(organ.Id, organ.Component))
            body.InsertOrgan(destination.Id, organ.Id, slot, destination.Component, organ.Component);
    }
}

public sealed partial class RegenerateOrgan : EntityEffect
{
    [DataField(required: true)] public string Slot = string.Empty;
    [DataField] public bool Recursive = true;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
    public override void Effect(EntityEffectBaseArgs args)
    {
        var em = args.EntityManager;
        if (!em.TryGetComponent<BodyPartComponent>(args.TargetEntity, out var parent) ||
            parent.Body is not {} bodyUid ||
            !em.TryGetComponent<BodyComponent>(bodyUid, out var bodyComp) ||
            bodyComp.Prototype is not {} bodyId)
            return;
        var prototype = IoCManager.Resolve<IPrototypeManager>().Index<BodyPrototype>(bodyId);
        var pair = prototype.Slots.FirstOrDefault(entry => entry.Key.Equals(Slot, StringComparison.OrdinalIgnoreCase));
        if (pair.Value?.Part is not {} partProto)
            return;
        var spawned = em.SpawnEntity(partProto, em.GetComponent<TransformComponent>(args.TargetEntity).Coordinates);
        if (!em.TryGetComponent<BodyPartComponent>(spawned, out var spawnedPart))
        {
            em.QueueDeleteEntity(spawned);
            return;
        }
        var system = em.System<SharedBodySystem>();
        var slot = pair.Key;
        system.TryCreatePartSlot(args.TargetEntity, slot, spawnedPart.PartType, out _, parent);
        if (!system.AttachPart(args.TargetEntity, slot, spawned, parent, spawnedPart))
            em.QueueDeleteEntity(spawned);
    }
}

public sealed partial class HasOrganSlot : EntityEffectCondition
{
    [DataField(required: true)] public string Organ = string.Empty;
    [DataField(required: true)] public BodyPartType PartType;
    [DataField] public BodyPartSymmetry? Symmetry;
    [DataField] public bool Inverted;
    public override bool Condition(EntityEffectBaseArgs args)
    {
        var key = Organ.ToLowerInvariant();
        var result = args.EntityManager.System<SharedBodySystem>()
            .GetBodyChildrenOfType(args.TargetEntity, PartType, symmetry: Symmetry)
            .Any(part => part.Component.Organs.ContainsKey(key) || part.Component.Children.ContainsKey(key));
        return Inverted ? !result : result;
    }
    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}
