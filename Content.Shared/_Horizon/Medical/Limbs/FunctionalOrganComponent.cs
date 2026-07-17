using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.Medical.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FunctionalOrganComponent : Component
{
    [DataField]
    public ComponentRegistry? OnAddComponents;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
