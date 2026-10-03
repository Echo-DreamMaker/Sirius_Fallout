using Robust.Shared.GameStates;

namespace Content.Shared._White.AdminObserver;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class AdminObserverHandsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HandsEnabled = true;
}
