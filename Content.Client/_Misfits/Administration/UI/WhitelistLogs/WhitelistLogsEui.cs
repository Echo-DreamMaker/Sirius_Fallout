using Content.Client.Eui;
using Content.Shared._Misfits.Administration.WhitelistLogs;
using Content.Shared.Eui;

namespace Content.Client._Misfits.Administration.UI.WhitelistLogs;

public sealed class WhitelistLogsEui : BaseEui
{
    private readonly WhitelistLogsWindow _window;

    public WhitelistLogsEui()
    {
        _window = new WhitelistLogsWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not WhitelistLogsEuiState cast)
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
