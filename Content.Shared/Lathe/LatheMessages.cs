using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Lathe;

[Serializable, NetSerializable]
public sealed class LatheUpdateState : BoundUserInterfaceState
{
    public List<ProtoId<LatheRecipePrototype>> Recipes;

    public List<LatheRecipePrototype> Queue;

    public LatheRecipePrototype? CurrentlyProducing;

    // #Misfits Change Add: Server-computed available material amounts (pool + physical storage)
    // sent to the client so PopulateRecipes can accurately enable/disable recipe buttons even
    // when PhysicalCompositionComponent is not yet synced to the client.
    public Dictionary<ProtoId<MaterialPrototype>, int> AvailableMaterials;

    // #Sirius Add: Server-computed counts of whole item prototypes available to item costs.
    public Dictionary<EntProtoId, int> AvailableItems;

    public LatheUpdateState(List<ProtoId<LatheRecipePrototype>> recipes, List<LatheRecipePrototype> queue, LatheRecipePrototype? currentlyProducing = null, Dictionary<ProtoId<MaterialPrototype>, int>? availableMaterials = null, Dictionary<EntProtoId, int>? availableItems = null)
    {
        Recipes = recipes;
        Queue = queue;
        CurrentlyProducing = currentlyProducing;
        AvailableMaterials = availableMaterials ?? new Dictionary<ProtoId<MaterialPrototype>, int>();
        AvailableItems = availableItems ?? new Dictionary<EntProtoId, int>();
    }
}

/// <summary>
///     Sent to the server to sync material storage and the recipe queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheSyncRequestMessage : BoundUserInterfaceMessage
{

}

/// <summary>
///     Sent to the server when a client queues a new recipe.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheQueueRecipeMessage : BoundUserInterfaceMessage
{
    public readonly string ID;
    public readonly int Quantity;
    public LatheQueueRecipeMessage(string id, int quantity)
    {
        ID = id;
        Quantity = quantity;
    }
}

[NetSerializable, Serializable]
public enum LatheUiKey
{
    Key,
}
