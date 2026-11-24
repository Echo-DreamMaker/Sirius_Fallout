using System;
using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Lathe;

// #Sirius Add: whole-item recipe costs. Lathe recipes can now require concrete entity prototypes
// (combat armor, dynamite, microchips, NV wires, batteries, ...) in addition to material units.
// Matching items are taken from the workbench storage container first, then from the acting
// player's hands and inventory (including items inside equipped bags and other storage).
public sealed partial class LatheSystem
{
    [Dependency] private readonly SharedHandsSystem _siriusHands = default!;
    [Dependency] private readonly InventorySystem _siriusInventory = default!;

    protected override int GetAvailableItemAmount(EntityUid uid, EntProtoId prototype, LatheComponent component)
    {
        var count = 0;

        if (TryComp<StorageComponent>(uid, out var storage))
        {
            var visited = new HashSet<EntityUid>();
            foreach (var entity in storage.Container.ContainedEntities.ToArray())
            {
                VisitHeldOrStored(entity, visited, contained =>
                {
                    if (IsPrototype(contained, prototype))
                        count++;
                });
            }
        }

        if (component.LastUser is { } user)
            count += CountInUser(user, prototype);

        return count;
    }

    /// <summary>
    /// #Sirius Add: counts of every whole-item prototype used by the recipes available at this lathe.
    /// Sent to the client so recipe buttons and tooltips can reflect item costs.
    /// </summary>
    public Dictionary<EntProtoId, int> GetAvailableItemAmounts(EntityUid uid, LatheComponent component, EntityUid? actor = null)
    {
        var wanted = new HashSet<EntProtoId>();
        foreach (var recipeId in GetAvailableRecipes(uid, component, true))
        {
            if (!_proto.TryIndex(recipeId, out var recipe))
                continue;

            foreach (var item in recipe.ItemCosts.Keys)
                wanted.Add(item);

            // #Sirius Add: alternative item costs must also be counted for UI and consumption checks.
            foreach (var option in recipe.AnyOfItemCosts)
            {
                foreach (var item in option.Keys)
                    wanted.Add(item);
            }
        }

        var result = new Dictionary<EntProtoId, int>();
        if (wanted.Count == 0)
            return result;

        foreach (var item in wanted)
            result[item] = 0;

        void Count(EntityUid entity)
        {
            var id = MetaData(entity).EntityPrototype?.ID;
            if (id is null)
                return;

            var item = (EntProtoId) id;
            if (wanted.Contains(item))
                result[item]++;
        }

        if (TryComp<StorageComponent>(uid, out var storage))
        {
            var visited = new HashSet<EntityUid>();
            foreach (var entity in storage.Container.ContainedEntities.ToArray())
                VisitHeldOrStored(entity, visited, Count);
        }

        if (actor is not null)
            CountUser(actor.Value, Count);

        return result;
    }

    /// <summary>
    /// #Sirius Add: checks and then consumes the whole-item costs of a recipe.
    /// Called together with material consumption when a recipe enters the queue.
    /// </summary>
    public bool TryConsumeItemCosts(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component, EntityUid? actor)
    {
        // #Sirius Fix: recipes with only alternative costs must still consume one option.
        if (recipe.ItemCosts.Count == 0 && recipe.AnyOfItemCosts.Count == 0)
            return true;

        // #Sirius Add: UI interactions pass the actor; fallback to the last user so machine-driven
        // queues resolve costs against the same player CanProduce() checked.
        var user = actor ?? component.LastUser;
        var available = GetAvailableItemAmounts(uid, component, user);
        foreach (var (item, needed) in recipe.ItemCosts)
        {
            if (available.GetValueOrDefault(item, 0) < needed)
                return false;
        }

        foreach (var (item, needed) in recipe.ItemCosts)
        {
            for (var i = 0; i < needed; i++)
            {
                if (!TryConsumeOneItem(uid, item, user))
                {
                    Log.Warning($"Sirius item cost consumption failed for {item} in recipe {recipe.ID}");
                    return false;
                }
            }
        }

        // #Sirius Add: consume one full alternative option after mandatory item costs.
        if (!TryConsumeAnyOfItemCosts(uid, recipe, component, user))
            return false;

        return true;
    }

    /// <summary>
    /// #Sirius Add: picks and consumes one affordable alternative item-cost option.
    /// Availability is recalculated after mandatory costs so overlapping items are not double-spent.
    /// </summary>
    private bool TryConsumeAnyOfItemCosts(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component, EntityUid? user)
    {
        if (recipe.AnyOfItemCosts.Count == 0)
            return true;

        var available = GetAvailableItemAmounts(uid, component, user);
        foreach (var option in recipe.AnyOfItemCosts)
        {
            if (option.Any(pair => available.GetValueOrDefault(pair.Key, 0) < pair.Value))
                continue;

            foreach (var (item, needed) in option)
            {
                for (var i = 0; i < needed; i++)
                {
                    if (!TryConsumeOneItem(uid, item, user))
                    {
                        Log.Warning($"Sirius alternative item cost consumption failed for {item} in recipe {recipe.ID}");
                        return false;
                    }
                }
            }

            return true;
        }

        Log.Warning($"Sirius no affordable alternative item cost for recipe {recipe.ID}");
        return false;
    }

    /// <summary>
    /// #Sirius Add: true when this entity prototype is required as a whole item by any recipe
    /// available at the lathe. Used to keep such items out of the material pool and to refresh
    /// the UI when they are inserted into or removed from bench storage.
    /// </summary>
    public bool IsItemCostPrototype(EntityUid uid, LatheComponent component, EntityUid entity)
    {
        var id = MetaData(entity).EntityPrototype?.ID;
        if (id is null)
            return false;

        var item = (EntProtoId) id;
        foreach (var recipeId in GetAvailableRecipes(uid, component, true))
        {
            if (!_proto.TryIndex(recipeId, out var recipe))
                continue;

            if (recipe.ItemCosts.ContainsKey(item))
                return true;

            // #Sirius Add: alternative item costs are real whole-item costs too.
            foreach (var option in recipe.AnyOfItemCosts)
            {
                if (option.ContainsKey(item))
                    return true;
            }
        }

        return false;
    }

    private bool IsPrototype(EntityUid entity, EntProtoId prototype)
        => MetaData(entity).EntityPrototype?.ID == prototype.Id;

    private int CountInUser(EntityUid user, EntProtoId prototype)
    {
        var count = 0;
        CountUser(user, entity =>
        {
            if (IsPrototype(entity, prototype))
                count++;
        });
        return count;
    }

    /// <summary>
    /// #Sirius Add: walk hands and equipped slots, including items stored inside bags/containers.
    /// The visited set keeps nested containers from counting an entity twice.
    /// </summary>
    private void CountUser(EntityUid user, Action<EntityUid> count)
    {
        var visited = new HashSet<EntityUid>();

        foreach (var held in _siriusHands.EnumerateHeld(user).ToArray())
            VisitHeldOrStored(held, visited, count);

        if (!TryComp<InventoryComponent>(user, out var inventory) ||
            !_siriusInventory.TryGetSlots(user, out var slots))
            return;

        foreach (var slot in slots)
        {
            if (_siriusInventory.TryGetSlotEntity(user, slot.Name, out var item, inventory))
                VisitHeldOrStored(item.Value, visited, count);
        }
    }

    private void VisitHeldOrStored(EntityUid entity, HashSet<EntityUid> visited, Action<EntityUid> visit)
    {
        if (!visited.Add(entity))
            return;

        visit(entity);

        if (!TryComp<StorageComponent>(entity, out var storage))
            return;

        foreach (var child in storage.Container.ContainedEntities.ToArray())
            VisitHeldOrStored(child, visited, visit);
    }

    private bool TryConsumeOneItem(EntityUid uid, EntProtoId prototype, EntityUid? actor)
    {
        // Bench storage is checked first so physical items placed into the station are consumed
        // before anything the player is carrying.
        if (TryComp<StorageComponent>(uid, out _) &&
            TryFindStored(uid, prototype, new HashSet<EntityUid>(), out var stored, out var benchParent) &&
            stored is not null && benchParent is not null)
        {
            _container.Remove(stored.Value, benchParent, force: true);
            QueueDel(stored.Value);
            return true;
        }

        if (actor is not { } user)
            return false;

        foreach (var held in _siriusHands.EnumerateHeld(user).ToArray())
        {
            if (IsPrototype(held, prototype) &&
                _siriusHands.TryDrop(user, held, checkActionBlocker: false, doDropInteraction: false))
            {
                QueueDel(held);
                return true;
            }

            if (TryFindStored(held, prototype, new HashSet<EntityUid>(), out var heldNested, out var heldParent) &&
                heldNested is not null && heldParent is not null)
            {
                _container.Remove(heldNested.Value, heldParent, force: true);
                QueueDel(heldNested.Value);
                return true;
            }
        }

        if (TryComp<InventoryComponent>(user, out var inventory) &&
            _siriusInventory.TryGetSlots(user, out var slots))
        {
            foreach (var slot in slots)
            {
                if (!_siriusInventory.TryGetSlotEntity(user, slot.Name, out var item, inventory))
                    continue;

                if (IsPrototype(item.Value, prototype) &&
                    _siriusInventory.TryUnequip(user, slot.Name, silent: true, force: true, inventory: inventory))
                {
                    QueueDel(item.Value);
                    return true;
                }

                if (TryFindStored(item.Value, prototype, new HashSet<EntityUid>(), out var slotNested, out var slotParent) &&
                    slotNested is not null && slotParent is not null)
                {
                    _container.Remove(slotNested.Value, slotParent, force: true);
                    QueueDel(slotNested.Value);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// #Sirius Add: find a whole-item cost inside any storage container under <paramref name="root"/>.
    /// Returns the matching entity and the container it lives in so it can be removed in place.
    /// </summary>
    private bool TryFindStored(
        EntityUid root,
        EntProtoId prototype,
        HashSet<EntityUid> visited,
        out EntityUid? found,
        out Container? parentContainer)
    {
        found = null;
        parentContainer = null;

        if (!visited.Add(root) || !TryComp<StorageComponent>(root, out var storage))
            return false;

        foreach (var child in storage.Container.ContainedEntities.ToArray())
        {
            if (IsPrototype(child, prototype))
            {
                found = child;
                parentContainer = storage.Container;
                return true;
            }

            if (TryFindStored(child, prototype, visited, out found, out parentContainer))
                return true;
        }

        return false;
    }
}
