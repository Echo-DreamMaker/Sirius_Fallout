using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// API token used by the server to ask SS14.Watchdog to check for and stage a new build.
    /// This is intentionally server-only and confidential; it must never reach clients.
    /// </summary>
    public static readonly CVarDef<string> WatchdogDeployApiToken =
        CVarDef.Create("misfits.watchdog.deploy_api_token", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
