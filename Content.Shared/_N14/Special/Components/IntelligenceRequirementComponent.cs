using Robust.Shared.GameStates;

namespace Content.Shared._N14.Special.Components;

/// <summary>
/// Applied to energy weapons.  Firing can misfire or fall from the wielder's hands
/// when the shooter's Intelligence is below <see cref="RequiredIntelligence"/>.
/// Checked via <see cref="IntelligenceRequirementSystem"/> on the ShotAttemptedEvent.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IntelligenceRequirementComponent : Component
{
    [DataField, AutoNetworkedField]
    public int RequiredIntelligence = 4;
}
