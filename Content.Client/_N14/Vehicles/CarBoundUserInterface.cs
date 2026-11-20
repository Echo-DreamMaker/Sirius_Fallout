// #Sirius Add - car console BUI wiring.
using Content.Shared._N14.Vehicles;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._N14.Vehicles;

[UsedImplicitly]
public sealed class CarBoundUserInterface : BoundUserInterface
{
    private CarWindow? _window;

    public CarBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CarWindow>();
        _window.OnSeatSelected += seatIndex => SendMessage(new CarSelectSeatMessage(seatIndex));
        _window.OnLoadCargo += () => SendMessage(new CarLoadCargoMessage());
        _window.OnUnloadCargo += crate => SendMessage(new CarUnloadCargoMessage(crate));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CarBoundUiState carState)
            return;

        _window?.SetState(carState);
    }
}
