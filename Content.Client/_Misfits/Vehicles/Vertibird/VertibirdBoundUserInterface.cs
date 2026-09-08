using Content.Shared._Misfits.Vehicles.Vertibird;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.Vehicles.Vertibird;

[UsedImplicitly]
public sealed class VertibirdBoundUserInterface : BoundUserInterface
{
    private VertibirdWindow? _window;

    public VertibirdBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<VertibirdWindow>();
        _window.OnSeatSelected += seatIndex => SendMessage(new VertibirdSelectSeatMessage(seatIndex));
        _window.OnLoadCargo += () => SendMessage(new VertibirdLoadCargoMessage());
        _window.OnUnloadCargo += crate => SendMessage(new VertibirdUnloadCargoMessage(crate));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not VertibirdSeatBoundUserInterfaceState vertibirdState)
            return;

        _window?.SetState(vertibirdState);
    }
}
