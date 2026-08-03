using Content.Shared._Nuclear14.AutodocSirius;
using Robust.Client.UserInterface;

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
            _window.OnPartSelected += OnPartSelected;
            _window.OnOperationSelected += OnOperationSelected;
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
    private void OnPartSelected(string partId)
    {
        SendMessage(new AutodocSurgeryPartSelectedMessage(partId));
    }

    private void OnOperationSelected(string partId, string operationId)
    {
        SendMessage(new AutodocSurgeryOperationMessage(partId, operationId));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.OnAutodocButton -= OnButtonPressed;
            _window.OnPartSelected -= OnPartSelected;
            _window.OnOperationSelected -= OnOperationSelected;
            _window.Close();
        }
        _window = null;
        base.Dispose(disposing);
    }
}
