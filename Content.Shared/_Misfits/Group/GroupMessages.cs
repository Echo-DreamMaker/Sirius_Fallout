// #Misfits Add - Shared network messages for the player group (/party) system.
// Players use /group to form a small group, invite others, and toggle an [ALLY] overlay.
// All game logic is server-side; clients receive state updates via these events.

using System.Collections.Generic;
using Robust.Shared.Network;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Group;

/// <summary>Role within a group. Leader has full control, officers can coordinate, members are regular participants.</summary>
[Serializable, NetSerializable]
public enum GroupMemberRole : byte
{
    Member,
    Officer,
    Leader,
}

// ── Client → Server requests ──────────────────────────────────────────────

/// <summary>Client requests the current group state; server responds with GroupStateUpdateEvent.</summary>
[Serializable, NetSerializable]
public sealed class GroupOpenPanelRequestEvent : EntityEventArgs { }

/// <summary>Client requests creation of a new group (caller becomes leader).</summary>
[Serializable, NetSerializable]
public sealed class GroupCreateRequestEvent : EntityEventArgs { }

/// <summary>Leader invites another player by their in-world character name.</summary>
[Serializable, NetSerializable]
public sealed class GroupInviteRequestEvent : EntityEventArgs
{
    public string TargetCharacterName { get; set; } = string.Empty;
}

/// <summary>Target accepts or declines a pending invite from a specific inviter.</summary>
[Serializable, NetSerializable]
public sealed class GroupInviteResponseEvent : EntityEventArgs
{
    public bool Accept { get; set; }
    /// <summary>UserId of the player who sent the invite — used to match the pending invite record.</summary>
    public NetUserId InviterUserId { get; set; }
}

/// <summary>Player leaves (or disbands, if leader and only member) their group.</summary>
[Serializable, NetSerializable]
public sealed class GroupLeaveRequestEvent : EntityEventArgs { }

/// <summary>Leader kicks a member by character name.</summary>
[Serializable, NetSerializable]
public sealed class GroupKickRequestEvent : EntityEventArgs
{
    public string TargetCharacterName { get; set; } = string.Empty;
}

/// <summary>Leader toggles another member between officer and regular member.</summary>
[Serializable, NetSerializable]
public sealed class GroupRoleChangeRequestEvent : EntityEventArgs
{
    public string TargetCharacterName { get; set; } = string.Empty;
    public GroupMemberRole TargetRole { get; set; }
}

/// <summary>Leader renames the group.</summary>
[Serializable, NetSerializable]
public sealed class GroupRenameRequestEvent : EntityEventArgs
{
    public string NewName { get; set; } = string.Empty;
}

/// <summary>Leader or officer sets the group's rally point at their current location.</summary>
[Serializable, NetSerializable]
public sealed class GroupSetRallyPointRequestEvent : EntityEventArgs
{
    public MapCoordinates Coordinates { get; set; }
}

/// <summary>Leader or officer clears the group's rally point.</summary>
[Serializable, NetSerializable]
public sealed class GroupClearRallyPointRequestEvent : EntityEventArgs { }

/// <summary>Player toggles the [ALLY] screen overlay on or off.</summary>
[Serializable, NetSerializable]
public sealed class GroupToggleOverlayRequestEvent : EntityEventArgs
{
    public bool Enabled { get; set; }
}

// ── Server → Client responses ─────────────────────────────────────────────

/// <summary>
/// Full group state sent to a player whenever membership changes, or on panel open.
/// Empty Members list means the player is not in a group.
/// PendingInviteFromName/UserId are non-null when the player has an outstanding invite.
/// </summary>
[Serializable, NetSerializable]
public sealed class GroupStateUpdateEvent : EntityEventArgs
{
    /// <summary>All group members with display names and roles. Empty when not in a group.</summary>
    public List<GroupMemberInfo> Members { get; set; } = new();
    /// <summary>UserId of the current group leader. Null when not in a group.</summary>
    public NetUserId? LeaderUserId { get; set; }
    /// <summary>Display name for the group, if any.</summary>
    public string? GroupName { get; set; }
    /// <summary>Optional rally point shown on the tactical map.</summary>
    public MapCoordinates? RallyPoint { get; set; }
    /// <summary>Character name of the player who sent the pending invite, if any.</summary>
    public string? PendingInviteFromName { get; set; }
    /// <summary>UserId of the player who sent the invite (needed for response routing).</summary>
    public NetUserId? PendingInviteFromUserId { get; set; }
}

/// <summary>One member entry in a group snapshot.</summary>
[Serializable, NetSerializable]
public readonly record struct GroupMemberInfo(NetEntity Entity, string Name, GroupMemberRole Role);

/// <summary>Server-side snapshot of a group for raid targeting / decision routing.</summary>
public readonly record struct GroupRaidTargetInfo(int GroupId, string GroupName, List<GroupMemberInfo> Members);

/// <summary>
/// Periodic overlay update: maps group member NetEntities to their display names.
/// Sent every 2 seconds to members who have overlay enabled.
/// Empty dict = no group members to tag (overlay should clear).
/// </summary>
[Serializable, NetSerializable]
public sealed class GroupOverlayUpdateEvent : EntityEventArgs
{
    public Dictionary<NetEntity, string> GroupMembers { get; set; } = new();
}

/// <summary>Simple success/failure feedback for any group action.</summary>
[Serializable, NetSerializable]
public sealed class GroupActionResultEvent : EntityEventArgs
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
