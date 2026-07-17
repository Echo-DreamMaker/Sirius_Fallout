using System.Linq;
using Content.Shared._Misfits.Administration.WhitelistLogs;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Administration.WhitelistLogs;

public sealed class WhitelistLogSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<WhitelistLogEntry> _entries = new();
    private readonly HashSet<WhitelistLogsEui> _openUis = new();

    public void AddEntry(
        string action,
        string adminName,
        string targetName,
        string roles,
        string reason = "",
        string discordUsername = "",
        string applicationText = "")
    {
        var entry = new WhitelistLogEntry(
            action,
            adminName,
            targetName,
            roles,
            reason,
            discordUsername,
            applicationText,
            _timing.CurTime);

        _entries.Add(entry);

        foreach (var ui in _openUis)
        {
            if (!ui.IsShutDown)
                ui.StateDirty();
        }
    }

    public List<WhitelistLogEntry> GetEntries()
    {
        return _entries.OrderByDescending(e => e.Time).ToList();
    }

    public void RegisterUi(WhitelistLogsEui ui)
    {
        _openUis.Add(ui);
    }

    public void UnregisterUi(WhitelistLogsEui ui)
    {
        _openUis.Remove(ui);
    }
}
