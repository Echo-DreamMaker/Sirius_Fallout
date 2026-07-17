// #Misfits Add - Server-side EUI for the Door Logs admin panel
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Misfits.Administration.DoorLogs;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Log;

namespace Content.Server._Misfits.Administration.DoorLogs;

public sealed class DoorLogsEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private readonly ISawmill _sawmill;
    private readonly DoorLogSystem _doorLog;

    public DoorLogsEui()
    {
        IoCManager.InjectDependencies(this);
        _doorLog = _entityManager.System<DoorLogSystem>();
        _sawmill = _log.GetSawmill("admin.door_logs_eui");
    }

    public override EuiStateBase GetNewState()
    {
        return new DoorLogsEuiState(_doorLog.GetEntries());
    }

    public override void Opened()
    {
        base.Opened();
        _doorLog.RegisterUi(this);
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _doorLog.UnregisterUi(this);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to use door logs without permission");
            return;
        }

        // Currently no client-to-server messages needed - the state is refreshed on open.
    }
}
