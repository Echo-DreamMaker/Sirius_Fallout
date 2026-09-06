using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Overwatch;

/// <summary>
/// Placed on an operator while they are watching a target through an Overwatch console.
/// This is the per-actor, auto-networked source of truth for the operator's current watch
/// session, so any number of operators can each hold their own session on the same console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OverwatchWatchingComponent : Component
{
    /// <summary>Entity currently being watched. Null while idle or while the link is suspended.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Watching;

    /// <summary>Personnel number of the current watch target. Retained while suspended.</summary>
    [DataField, AutoNetworkedField]
    public uint? WatchedNumber;

    /// <summary>Display name of the current watch target.</summary>
    [DataField, AutoNetworkedField]
    public string? WatchedName;

    /// <summary>Last known entity name while the link is degraded.</summary>
    [DataField, AutoNetworkedField]
    public string? LastKnownName;

    [DataField, AutoNetworkedField]
    public float? LastKnownX;

    [DataField, AutoNetworkedField]
    public float? LastKnownY;

    [DataField, AutoNetworkedField]
    public string? LastKnownTimestamp;
}
