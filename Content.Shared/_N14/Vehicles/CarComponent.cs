using Robust.Shared.GameStates;

namespace Content.Shared._N14.Vehicles;

/// <summary>
/// Marker component for ground vehicles (cars) that use car-specific behavior
/// (ignition key, passengers, engine running) handled by CarSystem, split from MechSystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CarComponent : Component
{
}
