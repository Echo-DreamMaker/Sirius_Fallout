using Content.Shared._Misfits.Enclave;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.Enclave;

[UsedImplicitly]
public sealed class EnclaveDetonatorBoundUserInterface : BoundUserInterface
{
    private EnclaveDetonatorWindow? _window;

    public EnclaveDetonatorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<EnclaveDetonatorWindow>();
        _window.OnRefresh += () => SendMessage(new EnclaveDetonatorRefreshMessage());
        _window.OnActivate += target => SendMessage(new EnclaveDetonatorActivateMessage(target));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is EnclaveDetonatorBoundUserInterfaceState detonatorState)
            _window?.SetPersonnel(detonatorState.Personnel);
    }
}
