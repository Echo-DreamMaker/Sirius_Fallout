// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared.GameStates;

namespace Content.Shared._MultiZ.Core.Components;

/// <summary>
/// Central registry for a multi-Z station network.
/// Maps depth (int) to map entity, and vice versa.
/// Lives as an entity in nullspace, referenced by all MZMapComponents in the network.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MZNetworkComponent : Component
{
    /// <summary>
    /// Depth → Map entity lookup.
    /// Depth 0 is the ground-level map from the gameMap prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<int, EntityUid?> ZLevels = new();

    /// <summary>
    /// Map entity → Depth lookup. Inverse of ZLevels.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> ZLevelByEntity = new();
}
