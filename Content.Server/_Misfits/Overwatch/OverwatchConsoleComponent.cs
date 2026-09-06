using System.Collections.Generic;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Overwatch;

[RegisterComponent, Access(typeof(OverwatchConsoleSystem))]
public sealed partial class OverwatchConsoleComponent : Component
{
    [DataField]
    public List<ProtoId<TagPrototype>> TrackedTags = new();

    [DataField]
    public string MonitorTitle = "overwatch-monitor-title";

    /// <summary>
    /// Per-operator watch sessions keyed by the watching actor. Each operator holds their
    /// own target, enabling multiple simultaneous watchers on the same console.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, OverwatchWatchSession> WatchSessions = new();
}

/// <summary>Server-side watch session for a single operator on this console.</summary>
public sealed class OverwatchWatchSession
{
    /// <summary>
    /// The player session that owns the PVS subscription. Keep this independently of the actor entity so the
    /// subscription can still be removed after detaching for ghosting, aghosting, or disconnecting.
    /// </summary>
    public ICommonSession Subscriber = default!;

    public uint WatchedNumber;

    /// <summary>Last resolved watch target. Null while suspended or never resolved.</summary>
    public EntityUid? WatchedEntity;

    /// <summary>True while the target cannot be resolved (link suspended, not stopped).</summary>
    public bool Suspended;
}
