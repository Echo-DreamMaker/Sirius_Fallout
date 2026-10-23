using Content.Shared._Sirius.WeaponAssemblyStation;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client._Sirius.WeaponAssemblyStation;

public sealed class WeaponAssemblyStationBoundUserInterface : BoundUserInterface
{
    private WeaponAssemblyStationWindow? _window;

    public WeaponAssemblyStationBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _window = this.CreateWindow<WeaponAssemblyStationWindow>();
        if (_window != null)
        {
            _window.OnButtonPressed += OnButtonPressed;
            _window.OnSlotPressed += OnSlotPressed;
        }
        base.Open();
    }

    private void OnSlotPressed(string slotId)
    {
        SendMessage(new ItemSlotButtonPressedEvent(slotId));
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (_window == null)
            return;
        if (state is WeaponAssemblyBoundUserInterfaceState castState)
        {
            _window.UpdateState(castState);
        }
    }

    private void OnButtonPressed(WeaponAssemblyUiButton button)
    {
        SendMessage(new WeaponAssemblyUiButtonPressedMessage(button));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.OnButtonPressed -= OnButtonPressed;
            _window.OnSlotPressed -= OnSlotPressed;
            _window.Close();
        }
        _window = null;
        base.Dispose(disposing);
    }
}