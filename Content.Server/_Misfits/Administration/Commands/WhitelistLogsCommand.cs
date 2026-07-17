using Content.Server.Administration;
using Content.Server._Misfits.Administration.WhitelistLogs;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>
/// Opens the dedicated whitelist log panel.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class WhitelistLogsCommand : LocalizedCommands
{
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "whitelistlogs";

    public override string Description => "Open the dedicated whitelist log viewer.";

    public override string Help => "Usage: whitelistlogs";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } session)
        {
            shell.WriteError("This command can only be used in-game.");
            return;
        }

        var ui = new WhitelistLogsEui();
        _eui.OpenEui(ui, session);
    }
}
