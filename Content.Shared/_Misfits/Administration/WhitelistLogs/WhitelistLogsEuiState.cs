using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration.WhitelistLogs;

[Serializable, NetSerializable]
public sealed class WhitelistLogsEuiState : EuiStateBase
{
    public List<WhitelistLogEntry> Entries;

    public WhitelistLogsEuiState(List<WhitelistLogEntry> entries)
    {
        Entries = entries;
    }
}
