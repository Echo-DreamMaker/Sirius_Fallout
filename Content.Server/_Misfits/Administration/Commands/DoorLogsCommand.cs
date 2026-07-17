// #Misfits Add - Console command to open the Door Logs admin panel
using Content.Server.Administration;
using Content.Server._Misfits.Administration.DoorLogs;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>
/// Opens the door destruction log panel so admins can see which doors were destroyed and by whom.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class DoorLogsCommand : LocalizedCommands
{
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "doorlogs";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var ui = new DoorLogsEui();
        _eui.OpenEui(ui, player);
    }
}
