// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared._Misfits.Actions;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed partial class ChemSpikeMutationSystem : EntitySystem
{
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private BloodstreamSystem _blood = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedProjectileSystem _projectile = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ChemSpikeMutationComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ChemTransferActionComponent, ChemTransferActionEvent>(OnTransfer);
        SubscribeLocalEvent<ChemTransferProjectileComponent, EmbedEvent>(OnProjectileEmbed);
        SubscribeLocalEvent<ChemTransferProjectileComponent, RemoveEmbedEvent>(OnProjectileDetach);
        SubscribeLocalEvent<ChemTransferProjectileComponent, ComponentShutdown>(OnProjectileShutdown);
    }

    private void OnShutdown(Entity<ChemSpikeMutationComponent> ent, ref ComponentShutdown args)
        => QueueDel(ent.Comp.ActionEntity);

    private void OnTransfer(Entity<ChemTransferActionComponent> ent, ref ChemTransferActionEvent args)
    {
        if (CompOrNull<BaseActionComponent>(ent)?.Container is not {} mutation ||
            !TryComp<ChemSpikeMutationComponent>(mutation, out var comp) ||
            comp.Target is not {} target)
            return;

        if (TryComp<BloodstreamComponent>(args.Performer, out var bloodstream) &&
            _solution.ResolveSolution(args.Performer, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution))
        {
            var removed = _solution.SplitSolution(bloodstream.ChemicalSolution.Value, comp.MaxQuantity);
            if (removed.Volume == 0)
            {
                // No chems in your blood: warn and keep the transfer available instead of
                // wasting it. Deliberately leaving Handled false - that's what consumes
                // charges and starts the cooldown in PerformAction, so setting it here would
                // quietly burn the use the day this action gains a useDelay.
                _popup.PopupEntity(Loc.GetString("MutationChemSpike-no-chems"), args.Performer, args.Performer);
                return;
            }
            _blood.TryAddToChemicals(target, removed);
        }

        args.Handled = true;
        if (comp.Projectile is {} projectile && TryComp<EmbeddableProjectileComponent>(projectile, out var embed))
            _projectile.RemoveEmbed(projectile, embed);
        SetMutationTarget((mutation, comp), null, null);
    }

    private void OnProjectileEmbed(Entity<ChemTransferProjectileComponent> ent, ref EmbedEvent args)
    {
        if (HasComp<BloodstreamComponent>(args.Embedded))
            SetProjectileTarget(ent, args.Embedded);
    }

    private void OnProjectileDetach(Entity<ChemTransferProjectileComponent> ent, ref RemoveEmbedEvent args)
        => SetProjectileTarget(ent, null);

    private void OnProjectileShutdown(Entity<ChemTransferProjectileComponent> ent, ref ComponentShutdown args)
        => SetProjectileTarget(ent, null);

    private void SetProjectileTarget(EntityUid uid, EntityUid? target)
    {
        if (CompOrNull<ActionProjectileComponent>(uid)?.Container is not {} mutation)
            return;
        SetMutationTarget(mutation, target, uid);
    }

    private void SetMutationTarget(Entity<ChemSpikeMutationComponent?> ent, EntityUid? target, EntityUid? proj = null)
    {
        if (!Resolve(ent, ref ent.Comp) ||
            (ent.Comp.Target == target && ent.Comp.Projectile == proj) ||
            _mutation.GetMutationTarget(ent.Owner) is not {} user)
            return;

        ent.Comp.Target = target;
        ent.Comp.Projectile = proj;
        if (target != null)
            _actions.AddAction(user, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
        else
            _actions.RemoveAction(user, ent.Comp.ActionEntity);

        var key = target != null ? "set" : "reset";
        _popup.PopupEntity(Loc.GetString("MutationChemSpike-target-" + key), user, user);
    }
}
