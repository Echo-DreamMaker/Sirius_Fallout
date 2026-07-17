// #Misfits Add - Client-side EUI for the Door Logs admin panel
using Content.Client.Eui;
using Content.Shared._Misfits.Administration.DoorLogs;
using Content.Shared.Eui;

namespace Content.Client._Misfits.Administration.UI.DoorLogs;

public sealed class DoorLogsEui : BaseEui
{
    private DoorLogsWindow _window;

    public DoorLogsEui()
    {
        _window = new DoorLogsWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not DoorLogsEuiState cast)
            return;

        _window.Populate(cast.Entries);
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }
}
