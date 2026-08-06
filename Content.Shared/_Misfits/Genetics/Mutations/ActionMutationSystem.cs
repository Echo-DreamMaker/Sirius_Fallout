// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Misfits.Genetics.Mutations;

public sealed partial class ActionMutationSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    private static readonly TimeSpan DefaultMutationUseDelay = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<ActionMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<ActionMutationComponent> ent, ref MutationAddedEvent args)
    {
        // action shitcode spawns a clientside action
        if (_net.IsClient)
            return;

        if (_actions.AddAction(args.Target.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action, container: ent.Owner))
        {
            BaseActionComponent? action = null;
            if (_actions.ResolveActionData(ent.Comp.ActionEntity, ref action) && action.UseDelay == null)
                _actions.SetUseDelay(ent.Comp.ActionEntity, DefaultMutationUseDelay);

            _actions.SetCheckCanInteract(ent.Comp.ActionEntity, false);
        }

        Dirty(ent);
    }

    private void OnRemoved(Entity<ActionMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (ent.Comp.ActionEntity is {} action)
            _actions.RemoveProvidedAction(args.Target.Owner, ent.Owner, action);
    }

    public Entity<BaseActionComponent>? GetAction(Entity<ActionMutationComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return null;

        BaseActionComponent? component = null;
        if (ent.Comp.ActionEntity is not {} action ||
            !_actions.ResolveActionData(action, ref component))
            return null;

        return (action, component);
    }
}
