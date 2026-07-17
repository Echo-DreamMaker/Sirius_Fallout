using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Misfits.Administration.WhitelistLogs;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Log;

namespace Content.Server._Misfits.Administration.WhitelistLogs;

public sealed class WhitelistLogsEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private readonly ISawmill _sawmill;
    private readonly WhitelistLogSystem _whitelistLog;

    public WhitelistLogsEui()
    {
        IoCManager.InjectDependencies(this);
        _whitelistLog = _entityManager.System<WhitelistLogSystem>();
        _sawmill = _log.GetSawmill("admin.whitelist_logs_eui");
    }

    public override EuiStateBase GetNewState()
    {
        return new WhitelistLogsEuiState(_whitelistLog.GetEntries());
    }

    public override void Opened()
    {
        base.Opened();
        _whitelistLog.RegisterUi(this);
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _whitelistLog.UnregisterUi(this);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to use whitelist logs without permission");
            return;
        }
    }
}
