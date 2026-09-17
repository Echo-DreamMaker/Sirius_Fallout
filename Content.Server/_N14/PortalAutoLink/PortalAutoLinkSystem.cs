using Content.Shared.Interaction;
using Content.Shared.Teleportation.Systems;

namespace Content.Server._N14.PortalAutoLink
{
    public sealed partial class PortalAutoLinkSystem : EntitySystem
    {
        [Dependency] private readonly LinkedEntitySystem _linkedEntitySystem = default!;
        [Dependency] private readonly IEntityManager _entityMgr = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PortalAutoLinkComponent, MapInitEvent>(HandleMapInitialization);
        }

        private void HandleMapInitialization(Entity<PortalAutoLinkComponent> entity, ref MapInitEvent eventArgs)
        {
            PerformAutoLink(entity, out _);
        }

        public bool PerformAutoLink(Entity<PortalAutoLinkComponent> entity, out EntityUid? linkedEntityId)
        {
            linkedEntityId = null;

            var owner = entity.Owner;

            // Portals that were already paired or left over no longer carry the component.
            if (!_entityMgr.HasComponent<PortalAutoLinkComponent>(owner))
                return false;

            var key = entity.Comp.LinkKey;

            // Gather every portal sharing this key (including this one) so they can be
            // paired up in strict pairs instead of greedily grabbing the first match.
            var candidates = new List<EntityUid>();
            var entityEnumerator = EntityQueryEnumerator<PortalAutoLinkComponent>();
            while (entityEnumerator.MoveNext(out var currentEntityUid, out var currentAutoLinkComponent))
            {
                if (currentAutoLinkComponent.LinkKey == key)
                    candidates.Add(currentEntityUid);
            }

            // Link the 1st portal to the 2nd, the 3rd to the 4th, and so on.
            for (var i = 0; i + 1 < candidates.Count; i += 2)
            {
                var first = candidates[i];
                var second = candidates[i + 1];

                if (!_linkedEntitySystem.TryLink(first, second, false))
                    continue;

                RemComp<PortalAutoLinkComponent>(first);
                RemComp<PortalAutoLinkComponent>(second);

                if (first == owner || second == owner)
                {
                    linkedEntityId = first == owner ? second : first;
                    return true;
                }
            }

            // An odd leftover portal must never be auto-linked to anything;
            // drop it from the auto-link list so it can't pair later either.
            if (_entityMgr.HasComponent<PortalAutoLinkComponent>(owner))
                RemComp<PortalAutoLinkComponent>(owner);

            return false;
        }
    }
}
