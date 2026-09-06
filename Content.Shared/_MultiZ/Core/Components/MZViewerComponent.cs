// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared.GameStates;

namespace Content.Shared._MultiZ.Core.Components;

/// <summary>
/// Marks an entity as able to see through Z-levels via openings (empty/transparent tiles).
/// Tracks look-up state and stair-preview positions for the client renderer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent]
public sealed partial class MZViewerComponent : Component
{
    public const int MaxStairPreviewPositions = 4;

    /// <summary>
    /// Full look-up is active — the level above is rendered and aim is shifted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LookUp;

    /// <summary>
    /// Faint upper-level ghost — rooftop awareness mode.
    /// The level above is drawn at low alpha without shifting aim.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool FaintUp;

    /// <summary>
    /// Temporarily draws the level above when a visible stair is close enough.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StairPreviewUp;

    /// <summary>
    /// Number of active stair preview origin positions.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int StairPreviewPositionCount;

    /// <summary>
    /// Primary world position on the viewer's map for stair preview FOV/PVS origin.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition2;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition3;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition4;

    [DataField]
    public EntProtoId ActionProto = "ActionToggleMultiZLookUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelActionEntity;

    public Vector2 GetStairPreviewPosition(int index)
    {
        return index switch
        {
            0 => StairPreviewPosition,
            1 => StairPreviewPosition2,
            2 => StairPreviewPosition3,
            3 => StairPreviewPosition4,
            _ => default,
        };
    }

    public void SetStairPreviewPosition(int index, Vector2 value)
    {
        switch (index)
        {
            case 0:
                StairPreviewPosition = value;
                break;
            case 1:
                StairPreviewPosition2 = value;
                break;
            case 2:
                StairPreviewPosition3 = value;
                break;
            case 3:
                StairPreviewPosition4 = value;
                break;
        }
    }
}
