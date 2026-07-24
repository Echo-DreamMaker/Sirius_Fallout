using Content.Client.UserInterface.Controls;
using Content.Shared._Nuclear14.AutodocSirius;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Log;

namespace Content.Client._Nuclear14.AutodocSirius;

public sealed class SiriusAutodocBoundUserInterface : BoundUserInterface
{
    private SiriusAutodocWindow? _window;
    private static readonly ISawmill _sawmill = Logger.GetSawmill("autodoc");

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
        _sawmill.Info($"UpdateState called, state is {(state == null ? "null" : "not null")}");

        if (_window == null)
            return;

        if (state is AutodocBoundUserInterfaceState castState)
        {
            _sawmill.Info($"UpdateState: SelectedPartId={castState.SelectedPartId}, Operations={castState.AvailableOperations?.Count ?? 0}");
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
        _sawmill.Info($"OnPartSelected: {partId}");
        SendMessage(new AutodocSurgeryPartSelectedMessage(partId));
    }

    private void OnOperationSelected(string partId, string operationId)
    {
        _sawmill.Info($"OnOperationSelected: {partId}, {operationId}");
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
