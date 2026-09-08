using Content.Shared._N14.Special.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared._N14.Special.Components;

/// <summary>
/// Placed on weapons that require a minimum Strength to be used. Enforced by
/// <see cref="StrengthRequirementSystem"/>: a wield gate for two-handed weapons, an
/// automatic unwield when Strength drops below the requirement, and an attack/fire
/// cancellation when Strength is too low to apply the weapon.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(StrengthRequirementSystem))]
public sealed partial class StrengthRequirementComponent : Component
{
    /// <summary>
    /// Minimum effective Strength required to wield (if wieldable) and to apply/use this
    /// weapon. If the wielder's effective Strength is below this value the weapon cannot
    /// be swung or fired. Defaults low (2) so that everything is usable except by the
    /// weakest characters; individual prototypes raise it as needed.
    /// </summary>
    [DataField("requiredStrength"), AutoNetworkedField]
    public int RequiredStrength = 2;
}
