// #Misfits Add - Door log entry data model
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration.DoorLogs;

/// <summary>
/// A single entry in the door destruction log.
/// Records which door was destroyed and by whom.
/// </summary>
[Serializable, NetSerializable]
public sealed class DoorLogEntry
{
    /// <summary>
    /// The door entity prototype ID (e.g. N14MetalDoor).
    /// </summary>
    public string DoorPrototype;

    /// <summary>
    /// Name of the player/mob who destroyed the door.
    /// </summary>
    public string DestroyedBy;

    /// <summary>
    /// When the destruction happened (server time).
    /// </summary>
    public TimeSpan Time;

    public DoorLogEntry(string doorPrototype, string destroyedBy, TimeSpan time)
    {
        DoorPrototype = doorPrototype;
        DestroyedBy = destroyedBy;
        Time = time;
    }
}
