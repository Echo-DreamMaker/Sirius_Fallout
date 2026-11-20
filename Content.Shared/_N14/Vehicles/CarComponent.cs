using Robust.Shared.GameStates;

namespace Content.Shared._N14.Vehicles;

/// <summary>
/// Component for ground vehicles (cars) that use car-specific behavior
/// (ignition key, passengers, engine running) handled by CarSystem, split from MechSystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CarComponent : Component
{
    /// <summary>
    /// #Sirius Add - max turning speed in radians per second, used by A/D steering.
    /// </summary>
    [DataField]
    public float TurnRate = 1.75f;

    /// <summary>
    /// #Sirius Add - how many crates the cargo bay holds.
    /// </summary>
    [DataField]
    public int CargoCapacity = 2;

    /// <summary>
    /// #Sirius Add - locale key for the car console window title.
    /// </summary>
    [DataField]
    public string WindowTitleLocId = "car-window-title";

    /// <summary>
    /// #Sirius Add - true while the car is rolling with the throttle released.
    /// </summary>
    [ViewVariables]
    public bool Coasting;

    /// <summary>
    /// #Sirius Add - speed at the moment the throttle was released. The car loses this
    /// speed linearly over CarCoastDuration seconds, instead of the engine's exponential
    /// friction (which dumped half the speed in the first two seconds).
    /// </summary>
    [ViewVariables]
    public float CoastStartSpeed;

    /// <summary>
    /// #Sirius Add - braking run: time to go from CoastStartSpeed to zero.
    /// </summary>
    public const float CarCoastDuration = 10f;
}
