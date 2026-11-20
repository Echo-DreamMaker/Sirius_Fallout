namespace Content.Shared._N14.Vehicles;

/// <summary>
/// #Sirius Add - marks a car that spawns and manages dual headlight entities.
/// </summary>
[RegisterComponent]
public sealed partial class CarHeadlightsComponent : Component
{
    [DataField]
    public string LightPrototype = "N14CarHeadlight";

    /// <summary>
    /// Forward offset from car center for each headlight.
    /// </summary>
    [DataField]
    public float ForwardOffset = 1f;

    /// <summary>
    /// Perpendicular offset from the forward point for left/right headlights.
    /// </summary>
    [DataField]
    public float SideOffset = 0.25f;

    [ViewVariables]
    public bool HeadlightsOn;

    [ViewVariables]
    public EntityUid? LeftLight;

    [ViewVariables]
    public EntityUid? RightLight;
}
