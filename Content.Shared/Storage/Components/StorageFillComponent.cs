using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Storage.Components;

// TODO:
// REPLACE THIS WITH CONTAINERFILL
[RegisterComponent, NetworkedComponent, Access(typeof(SharedStorageSystem))]
public sealed partial class StorageFillComponent : Component
{
    [DataField("contents")] public List<EntitySpawnEntry> Contents = new();

    /// <summary>
    /// Whether the contents should be generated when the entity is map-initialized.
    /// Systems that provide their own interaction, such as searchable junk piles,
    /// can reuse this loot table without exposing a storage inventory.
    /// </summary>
    [DataField("fillOnMapInit")]
    public bool FillOnMapInit = true;
}
