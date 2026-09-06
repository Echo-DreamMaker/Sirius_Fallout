// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
// Ported to misfits-14 _MultiZ/ — renamed & adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared.GameStates;

namespace Content.Shared._MultiZ.Core.Components;

/// <summary>
/// Allows entities to walk on top of this entity at a higher Z-level.
/// Think of it as the ability to walk on top of walls, for example.
/// Supports height curves for ramps and staircases.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MZHighGroundComponent : Component
{
    /// <summary>
    /// Height profile points, forming a simple curve (0..1 by X, height by Y).
    /// Two points of 1.05 means a flat surface at height 1.05 (just above the floor above).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<float> HeightCurve = new()
    {
        1.05f,
        1.05f,
    };

    /// <summary>
    /// Forcibly attaches the entity to itself along the Z-axis if the character descends smoothly.
    /// Needed to prevent falling from staircases.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Stick;

    /// <summary>
    /// If true, this high ground only supports entities checking from a higher Z-level.
    /// Useful for ladders: the base can hold someone at the opening above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SupportOnlyFromAbove;

    /// <summary>
    /// Allows this highground to automatically reveal a nearby preview of the level above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PreviewUpLevel = true;

    /// <summary>
    /// Maximum distance in tiles at which this highground can reveal the level above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PreviewRange = 5f;

    /// <summary>
    /// Workaround for the inability to place map entities rotated by 45 degrees.
    /// When fixed, this flag should be removed in favor of proper rotation support.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Corner;
}
