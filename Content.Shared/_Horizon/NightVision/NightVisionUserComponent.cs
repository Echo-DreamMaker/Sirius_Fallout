using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.NightVision;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NightVisionUserComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public bool IsNightVision;

    [DataField]
    public Color NightVisionColor = Color.Green;

    [DataField]
    public bool IsToggle = false;

    [DataField]
    public EntityUid? ActionContainer;

    [DataField]
    public float TransitionDuration = 0.3f;
}

public sealed partial class NVInstantActionEvent : InstantActionEvent { }
