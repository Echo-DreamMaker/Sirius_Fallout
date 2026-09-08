// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared.GameStates;

namespace Content.Shared._MultiZ.Core.Components;

/// <summary>
/// Automatically added to each map entity that is part of a Z-level network.
/// Tracks the map above, map below, depth, and parent network entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class MZMapComponent : Component
{
    /// <summary>
    /// The Z-network entity that owns this map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid NetworkUid = EntityUid.Invalid;

    /// <summary>
    /// The map entity one Z-level above this one (depth + 1), if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? MapAbove;

    /// <summary>
    /// The map entity one Z-level below this one (depth - 1), if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? MapBelow;

    /// <summary>
    /// Depth of this map in the Z-network. Ground level is 0,
    /// upper levels are positive, lower levels are negative.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Depth;
}
