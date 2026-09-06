// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Genetics.Mutations;

public sealed class ComponentMutationSystem : EntitySystem
{
    [Dependency] private EntityQuery<ComponentMutationComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComponentMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<ComponentMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<ComponentMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (ent.Comp.Added is {} added)
            EntityManager.AddComponents(args.Target, added);
        if (ent.Comp.Removed is {} removed)
            EntityManager.RemoveComponents(args.Target, removed);
    }

    private void OnRemoved(Entity<ComponentMutationComponent> ent, ref MutationRemovedEvent args)
    {
        // removed components get readded first incase that mattered
        if (ent.Comp.Removed is {} removed)
            EntityManager.AddComponents(args.Target, removed);
        if (ent.Comp.Added is {} added)
            EntityManager.RemoveComponents(args.Target, StillProvided(ent.Owner, args.Target, added));
    }

    /// <summary>
    /// Drops any component another active mutation on the mob also adds, so removing one of
    /// two mutations that share a component doesn't strip it out from under the other.
    /// </summary>
    // ponytail: rescans the mob's mutations per removal, fine for the handful a mob carries
    private ComponentRegistry StillProvided(EntityUid mutation, Entity<MutatableComponent> mob, ComponentRegistry registry)
    {
        ComponentRegistry? filtered = null;
        foreach (var other in mob.Comp.Mutations.Values)
        {
            if (other == mutation || !_query.TryComp(other, out var comp) || comp.Added is not {} added)
                continue;

            foreach (var name in registry.Keys)
            {
                if (added.ContainsKey(name))
                    (filtered ??= new(registry)).Remove(name);
            }
        }

        return filtered ?? registry;
    }
}
