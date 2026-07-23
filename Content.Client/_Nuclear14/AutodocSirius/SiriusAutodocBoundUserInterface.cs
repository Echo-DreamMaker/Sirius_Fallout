using Content.Client.UserInterface.Controls;
using Content.Shared._Nuclear14.AutodocSirius;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Nuclear14.AutodocSirius;

public sealed class SiriusAutodocBoundUserInterface : BoundUserInterface
{
    private SiriusAutodocWindow? _window;

    public SiriusAutodocBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _window = this.CreateWindow<SiriusAutodocWindow>();
        if (_window != null)
        {
            _window.OnAutodocButton += OnButtonPressed;
            _window.OnClose += () =>
            {
                Close();
            };
        }
        base.Open();
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (_window == null)
            return;

        if (state is AutodocBoundUserInterfaceState castState)
        {
            _window.UpdateState(castState);
        }
    }

    private void OnButtonPressed(AutodocUiButton button)
    {
        if (button == AutodocUiButton.Close)
        {
            Close();
            return;
        }

        SendMessage(new AutodocUiButtonPressedMessage(button));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.OnAutodocButton -= OnButtonPressed;
            _window.Close();
        }
        _window = null;
        base.Dispose(disposing);
    }
}
