using Robust.Shared.Map;

namespace Content.Client._Misfits.Weapons.Ranged.Prediction;


/// <summary>
/// Based from <see cref="PredictedProjectileClientComponent"> in the same file which was ripped from RCM.
/// Comp that listens for the client physics events to extend prediction/prevent resetting
/// calls <see cref="PhysicsSystem.UpdateIsPredicted"/> on CompInit
/// Relavent system in <see cref="ClientPredictPhysSystem">
/// </summary>

[RegisterComponent]
public sealed partial class VisualPhysComponent : Component
{
    [DataField]
    public EntityCoordinates Coords = EntityCoordinates.Invalid;
}
