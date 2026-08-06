// #Misfits Add - Applies penalties from prosthetic hands and feet only while installed.

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;

namespace Content.Shared._Misfits.Prosthetics;

public sealed class ProstheticLimbSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<ProstheticLimbComponent, EntInsertedIntoContainerMessage>(
            OnProstheticInserted,
            after: [typeof(SharedBodySystem)]);
        SubscribeLocalEvent<ProstheticLimbComponent, EntRemovedFromContainerMessage>(
            OnProstheticRemoved,
            after: [typeof(SharedBodySystem)]);
        SubscribeLocalEvent<GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnRefreshMovementSpeed(
        Entity<BodyComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        foreach (var (partUid, part) in _body.GetBodyChildren(ent, ent.Comp))
        {
            if (part.PartType != BodyPartType.Foot ||
                !TryComp<ProstheticLimbComponent>(partUid, out var prosthetic))
                continue;

            args.ModifySpeed(prosthetic.MovementSpeedModifier);
        }
    }

    private void OnGetMeleeDamage(ref GetMeleeDamageEvent args)
    {
        if (!TryComp<BodyComponent>(args.User, out var body))
            return;

        foreach (var (partUid, part) in _body.GetBodyChildren(args.User, body))
        {
            if (part.PartType != BodyPartType.Hand ||
                !TryComp<ProstheticLimbComponent>(partUid, out var prosthetic))
                continue;

            args.Damage *= prosthetic.MeleeDamageModifier;
        }
    }

    private void OnProstheticInserted(
        Entity<ProstheticLimbComponent> ent,
        ref EntInsertedIntoContainerMessage args)
    {
        if (TryComp<BodyPartComponent>(ent, out var part) && part.Body is { } body)
            _movement.RefreshMovementSpeedModifiers(body);
    }

    private void OnProstheticRemoved(
        Entity<ProstheticLimbComponent> ent,
        ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<BodyPartComponent>(args.Container.Owner, out var parent) && parent.Body is { } body)
            _movement.RefreshMovementSpeedModifiers(body);
    }
}
