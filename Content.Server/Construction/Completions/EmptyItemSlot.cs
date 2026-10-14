// #Misfits Add - Removes the item held by an item slot, e.g. a magazine.
// EmptyContainer cannot be used for this: item slots are not registered in
// ContainerManagerComponent, so looking them up there always fails and the
// step silently does nothing - leaving the magazine to be deleted together
// with the weapon when the construction graph replaces the entity.
using Content.Shared.Construction;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;

namespace Content.Server.Construction.Completions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class EmptyItemSlot : IGraphAction
    {
        [DataField("slot")] public string Slot { get; private set; } = string.Empty;

        /// <summary>
        ///     Whether the removed item should be put into the user's hands (dropped on the floor if both hands are
        ///     full), like <see cref="EmptyContainer"/> does for regular containers.
        /// </summary>
        [DataField("pickup")]
        public bool Pickup = false;

        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (string.IsNullOrEmpty(Slot))
                return;

            var slotSys = entityManager.EntitySysManager.GetEntitySystem<ItemSlotsSystem>();

            if (!Pickup)
            {
                slotSys.TryEject(uid, Slot, userUid, out _);
                return;
            }

            if (!slotSys.TryGetSlot(uid, Slot, out var slot))
                return;

            slotSys.TryEjectToHands(uid, slot, userUid);
        }
    }
}