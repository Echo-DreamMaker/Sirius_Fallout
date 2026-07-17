// #Misfits Add - Shared state for the Door Logs EUI
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration.DoorLogs;

/// <summary>
/// State sent from server to client for the door logs EUI.
/// Contains the full list of logged door destructions.
/// </summary>
[Serializable, NetSerializable]
public sealed class DoorLogsEuiState : EuiStateBase
{
    /// <summary>
    /// All door destruction log entries, newest first.
    /// </summary>
    public List<DoorLogEntry> Entries;

    public DoorLogsEuiState(List<DoorLogEntry> entries)
    {
        Entries = entries;
    }
}
