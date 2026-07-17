using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.NightVision;

[RegisterComponent, NetworkedComponent]
public sealed partial class NVComponent : Component
{
    [DataField]
    public EntProtoId ActionProto = "NVToggleAction";

    [DataField]
    public EntityUid? ActionContainer;
}
