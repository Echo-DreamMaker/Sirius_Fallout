using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server._Misfits.Administration.Commands;

[AdminCommand(AdminFlags.Server)]
public sealed class WatchdogDeployCommand : IConsoleCommand
{
    [Dependency] private readonly WatchdogDeployManager _deploy = default!;

    public string Command => "misfitsdeploy";
    public string Description => "Ask SS14.Watchdog to check for and stage the latest published build.";
    public string Help => "misfitsdeploy";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(await _deploy.RequestDeployAsync()
            ? "Watchdog deploy check requested."
            : "Watchdog deploy request failed or is not configured.");
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class WatchdogSuperDeployCommand : IConsoleCommand
{
    [Dependency] private readonly WatchdogDeployManager _deploy = default!;

    public string Command => "misfitssuperdeploy";
    public string Description => "Host-only watchdog deploy command.";
    public string Help => "misfitssuperdeploy";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(await _deploy.RequestDeployAsync()
            ? "Host-level watchdog deploy check requested."
            : "Watchdog deploy request failed or is not configured.");
    }
}
