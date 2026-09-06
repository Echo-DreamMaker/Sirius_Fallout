using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC.Vendors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMVendorBundleComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Bundle = new();
}

