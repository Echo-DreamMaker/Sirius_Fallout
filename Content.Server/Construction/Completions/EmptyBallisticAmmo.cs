// #Misfits Add - Removes the loose ammunition held by a BallisticAmmoProvider,
// e.g. the internal magazine of the tribal sniper rifles.
// EmptyContainer cannot be used for this: the container is a DataField of the
// component ("ballistic-ammo"), not an entry of ContainerManagerComponent, so
// looking it up there always fails and the step silently does nothing - the
// cartridges are then deleted together with the weapon when the construction
// graph replaces the entity.
using Content.Server.Hands.Systems;
using Content.Shared.Construction;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Ranged.Components;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server.Construction.Completions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class EmptyBallisticAmmo : IGraphAction
    {
        /// <summary>
        ///     Whether or not the user should attempt to pick up the removed cartridges.
        /// </summary>
        [DataField("pickup")]
        public bool Pickup = false;

        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (!entityManager.TryGetComponent(uid, out BallisticAmmoProviderComponent? provider))
                return;

            var containerSys = entityManager.EntitySysManager.GetEntitySystem<ContainerSystem>();
            var handSys = entityManager.EntitySysManager.GetEntitySystem<HandsSystem>();

            HandsComponent? hands = null;
            var pickup = Pickup && entityManager.TryGetComponent(userUid, out hands);

            foreach (var ent in containerSys.EmptyContainer(provider.Container, true, reparent: !pickup))
            {
                if (pickup)
                    handSys.PickupOrDrop(userUid, ent, handsComp: hands);
            }
        }
    }
}