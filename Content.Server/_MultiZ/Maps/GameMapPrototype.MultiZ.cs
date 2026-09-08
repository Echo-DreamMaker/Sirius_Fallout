// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Added — Multi-Z level support for misfits-14

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Server.Maps;

/// <summary>
/// Multi-Z extension for GameMapPrototype.
/// </summary>
public sealed partial class GameMapPrototype
{
    /// <summary>
    /// Additional maps loaded above the main map (at positive depth levels).
    /// Each map in the list is loaded at depth 1, 2, ..., N. MapPath works as depth 0.
    /// </summary>
    [DataField]
    public List<ResPath> MapsAbove = new();

    /// <summary>
    /// Additional maps loaded below the main map (at negative depth levels).
    /// Each map in the list is loaded from top to bottom at depth -1, -2, ..., -N,
    /// with MapPath at depth 0.
    /// </summary>
    [DataField]
    public List<ResPath> MapsBelow = new();

    /// <summary>
    /// Component overrides applied to ALL Z-level maps in the network.
    /// Useful for shared lighting, atmosphere, or other per-map settings.
    /// </summary>
    [DataField]
    public ComponentRegistry ZLevelsComponentOverrides = new();
}
