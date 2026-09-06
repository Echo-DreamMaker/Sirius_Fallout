using Robust.Shared.GameStates;

namespace Content.Shared._RMC.Vendors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMVendorUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Points;

    [DataField, AutoNetworkedField]
    public Dictionary<string, int> SectionPurchases = new();
}

