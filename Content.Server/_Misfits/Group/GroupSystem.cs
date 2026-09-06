// #Misfits Add - Server-side group (/party) system.
// Players can form small ephemeral groups using /group command.
// Group state is entirely server-side (no DB, cleared on round restart).
// Server broadcasts overlay updates every 2s to members who opt-in.

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Misfits.Group;
using Content.Shared.GameTicking;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Group;

/// <summary>
/// Manages ephemeral player groups. Groups are lost on round restart.
/// Max 20 members, invite expires after 60 seconds.
/// Only the leader can invite/kick; any member can leave.
/// Leadership transfers to the oldest remaining member when the leader leaves.
/// </summary>
public sealed class GroupSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming    _timing        = default!;

    private const int MaxGroupSize = 20;
    private static readonly TimeSpan InviteExpiry       = TimeSpan.FromSeconds(60);
    private const float OverlayBroadcastInterval = 2.0f;

    // ── Inner types ────────────────────────────────────────────────────────

    private sealed class GroupData
    {
        public int   Id;
        public string Name = string.Empty;
        public NetUserId LeaderUserId;
        /// <summary>Members in join order (leader is always first).</summary>
        public List<NetUserId> MemberOrder = new();
        public Dictionary<NetUserId, GroupMemberRole> Roles = new();
        public MapCoordinates? RallyPoint;
    }

    private sealed class PendingInvite
    {
        public NetUserId InviterUserId;
        public NetUserId TargetUserId;
        public int       GroupId;
        public TimeSpan  ExpiresAt;
    }

    // ── State ──────────────────────────────────────────────────────────────

    private readonly Dictionary<int, GroupData>       _groups       = new();
    private readonly Dictionary<NetUserId, int>       _playerToGroup = new();
    private readonly List<PendingInvite>              _pendingInvites = new();
    private readonly HashSet<NetUserId>               _overlayEnabled = new();
    private static readonly Dictionary<NetEntity, string> EmptyOverlay = new();
    private int _nextGroupId = 1;
    private float _overlayAccumulator;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GroupOpenPanelRequestEvent>(OnOpenPanel);
        SubscribeNetworkEvent<GroupCreateRequestEvent>(OnCreate);
        SubscribeNetworkEvent<GroupInviteRequestEvent>(OnInvite);
        SubscribeNetworkEvent<GroupInviteResponseEvent>(OnInviteResponse);
        SubscribeNetworkEvent<GroupLeaveRequestEvent>(OnLeave);
        SubscribeNetworkEvent<GroupKickRequestEvent>(OnKick);
        SubscribeNetworkEvent<GroupRoleChangeRequestEvent>(OnRoleChange);
        SubscribeNetworkEvent<GroupRenameRequestEvent>(OnRename);
        SubscribeNetworkEvent<GroupSetRallyPointRequestEvent>(OnSetRallyPoint);
        SubscribeNetworkEvent<GroupClearRallyPointRequestEvent>(OnClearRallyPoint);
        SubscribeNetworkEvent<GroupToggleOverlayRequestEvent>(OnToggleOverlay);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<ActorComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Expire outdated invites.
        var now = _timing.CurTime;
        _pendingInvites.RemoveAll(inv => inv.ExpiresAt < now);

        // Periodic overlay broadcast.
        _overlayAccumulator += frameTime;
        if (_overlayAccumulator < OverlayBroadcastInterval)
            return;
        _overlayAccumulator = 0f;

        BroadcastOverlays();
    }

    // ── Public API (used by WastelandMapSystem) ────────────────────────────

    /// <summary>
    /// Returns the EntityUids of group members for a given actor (for WastelandMap blip injection).
    /// Returns null if the actor is not in a group or has no AttachedEntity.
    /// </summary>
    public List<EntityUid>? GetGroupMemberEntities(EntityUid actor)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return null;

        if (!_playerToGroup.TryGetValue(actorComp.PlayerSession.UserId, out var groupId))
            return null;

        if (!_groups.TryGetValue(groupId, out var group))
            return null;

        var result = new List<EntityUid>();
        foreach (var memberId in group.MemberOrder)
        {
            if (!TryGetSession(memberId, out var memberSession) || memberSession == null)
                continue;
            if (memberSession.AttachedEntity is { } ent)
                result.Add(ent);
        }
        return result;
    }

    /// <summary>Returns the active rally point for the actor's group, or null if none exists.</summary>
    public MapCoordinates? GetGroupRallyPoint(EntityUid actor)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return null;

        if (!_playerToGroup.TryGetValue(actorComp.PlayerSession.UserId, out var groupId))
            return null;

        if (!_groups.TryGetValue(groupId, out var group))
            return null;

        return group.RallyPoint;
    }

    /// <summary>Returns snapshots for all active groups, used by the raid panel to target formed groups.</summary>
    public List<GroupRaidTargetInfo> GetRaidTargets()
    {
        var targets = new List<GroupRaidTargetInfo>();
        foreach (var (groupId, group) in _groups)
        {
            targets.Add(new GroupRaidTargetInfo(groupId, group.Name, BuildMembers(group)));
        }

        targets.Sort((a, b) => string.Compare(a.GroupName, b.GroupName, StringComparison.Ordinal));
        return targets;
    }

    /// <summary>Looks up a single group snapshot for raid targeting / peer-approval routing.</summary>
    public bool TryGetRaidTargetInfo(int groupId, out GroupRaidTargetInfo info)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            info = new GroupRaidTargetInfo(groupId, group.Name, BuildMembers(group));
            return true;
        }

        info = default;
        return false;
    }

    // ── Overlay broadcast ─────────────────────────────────────────────────

    private void BroadcastOverlays()
    {
        foreach (var (_, group) in _groups)
        {
            var hasViewer = false;
            foreach (var memberId in group.MemberOrder)
            {
                if (_overlayEnabled.Contains(memberId))
                {
                    hasViewer = true;
                    break;
                }
            }

            if (!hasViewer)
                continue;

            // Pre-build the members dict for this group.
            var memberEntities = new Dictionary<NetEntity, string>();
            foreach (var memberId in group.MemberOrder)
            {
                if (!TryGetSession(memberId, out var ms) || ms == null)
                    continue;
                if (ms.AttachedEntity is not { } ent)
                    continue;
                memberEntities[GetNetEntity(ent)] = Name(ent);
            }

            foreach (var memberId in group.MemberOrder)
            {
                if (!_overlayEnabled.Contains(memberId))
                    continue;

                if (!TryGetSession(memberId, out var ms) || ms == null)
                    continue;

                RaiseNetworkEvent(new GroupOverlayUpdateEvent { GroupMembers = memberEntities }, ms);
            }
        }
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnOpenPanel(GroupOpenPanelRequestEvent msg, EntitySessionEventArgs args)
    {
        SendStateTo(args.SenderSession);
    }

    private void OnCreate(GroupCreateRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        if (_playerToGroup.ContainsKey(userId))
        {
            SendResult(session, false, Loc.GetString("group-already-in-group"));
            return;
        }

        var id    = _nextGroupId++;
        var group = new GroupData
        {
            Id           = id,
            Name         = $"Group {id}",
            LeaderUserId = userId,
            MemberOrder  = new List<NetUserId> { userId },
        };
        group.Roles[userId] = GroupMemberRole.Leader;

        _groups[id]          = group;
        _playerToGroup[userId] = id;

        BroadcastStateToGroup(id);
        SendResult(session, true, Loc.GetString("group-created"));
    }

    private void OnInvite(GroupInviteRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!TryGetSessionByCharacterName(msg.TargetCharacterName, out var targetSession) || targetSession == null)
        {
            SendResult(session, false, Loc.GetString("group-player-not-found", ("name", msg.TargetCharacterName)));
            return;
        }

        SendInvite(session, targetSession, msg.TargetCharacterName);
    }

    /// <summary>
    /// Adds the group invite directly to a nearby player's interaction menu.
    /// The normal network invite route ultimately calls the same method, so the
    /// permission, membership, capacity, and invite-expiry rules stay identical.
    /// </summary>
    private void OnGetInteractionVerbs(
        EntityUid target,
        ActorComponent targetActor,
        GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == target)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !_playerToGroup.TryGetValue(actor.PlayerSession.UserId, out var groupId) ||
            !_groups.TryGetValue(groupId, out var group) ||
            !CanInvite(actor.PlayerSession.UserId, group) ||
            group.MemberOrder.Count >= MaxGroupSize)
            return;

        var targetSession = targetActor.PlayerSession;
        if (_playerToGroup.ContainsKey(targetSession.UserId))
            return;

        var targetName = Name(target);
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("group-invite-verb"),
            Category = VerbCategory.Interaction,
            Act = () => SendInvite(actor.PlayerSession, targetSession, targetName),
        });
    }

    private void SendInvite(ICommonSession session, ICommonSession targetSession, string targetName)
    {
        var userId = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];

        if (!CanInvite(userId, group))
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        if (group.MemberOrder.Count >= MaxGroupSize)
        {
            SendResult(session, false, Loc.GetString("group-full"));
            return;
        }

        var targetUserId = targetSession.UserId;

        if (_playerToGroup.ContainsKey(targetUserId))
        {
            SendResult(session, false, Loc.GetString("group-target-already-in-group", ("name", targetName)));
            return;
        }

        // Replace any existing invite to this target from this group.
        _pendingInvites.RemoveAll(i => i.GroupId == groupId && i.TargetUserId == targetUserId);

        _pendingInvites.Add(new PendingInvite
        {
            InviterUserId = userId,
            TargetUserId  = targetUserId,
            GroupId       = groupId,
            ExpiresAt     = _timing.CurTime + InviteExpiry,
        });

        // Send state update to the target with invite info populated.
        var inviterName = session.AttachedEntity is { } ie ? Name(ie) : session.Name;
        var inviteState = new GroupStateUpdateEvent
        {
            PendingInviteFromName   = inviterName,
            PendingInviteFromUserId = userId,
        };
        RaiseNetworkEvent(inviteState, targetSession);

        SendResult(session, true, Loc.GetString("group-invite-sent", ("name", targetName)));
    }

    private void OnInviteResponse(GroupInviteResponseEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        var invite = _pendingInvites.FirstOrDefault(i =>
            i.TargetUserId == userId && i.InviterUserId == msg.InviterUserId);

        if (invite == null)
        {
            SendResult(session, false, Loc.GetString("group-invite-expired"));
            return;
        }

        _pendingInvites.Remove(invite);

        if (!msg.Accept)
        {
            // Clear invite state on the declining player.
            RaiseNetworkEvent(new GroupStateUpdateEvent(), session);
            // Notify the inviter.
            if (TryGetSession(invite.InviterUserId, out var inviterSession) && inviterSession != null)
            {
                var targetName = session.AttachedEntity is { } te ? Name(te) : session.Name;
                SendResult(inviterSession, false, Loc.GetString("group-invite-declined", ("name", targetName)));
            }
            return;
        }

        // Accept path.
        if (!_groups.TryGetValue(invite.GroupId, out var group))
        {
            SendResult(session, false, Loc.GetString("group-not-found"));
            return;
        }

        if (group.MemberOrder.Count >= MaxGroupSize)
        {
            SendResult(session, false, Loc.GetString("group-full"));
            return;
        }

        if (_playerToGroup.ContainsKey(userId))
        {
            SendResult(session, false, Loc.GetString("group-already-in-group"));
            return;
        }

        group.MemberOrder.Add(userId);
        _playerToGroup[userId] = invite.GroupId;
        group.Roles[userId] = GroupMemberRole.Member;

        BroadcastStateToGroup(invite.GroupId);
        SendResult(session, true, Loc.GetString("group-joined"));
    }

    private void OnLeave(GroupLeaveRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        // Clear overlay for this player.
        _overlayEnabled.Remove(userId);
        RaiseNetworkEvent(new GroupOverlayUpdateEvent { GroupMembers = EmptyOverlay }, session);

        RemoveMemberFromGroup(userId, groupId);
        SendResult(session, true, Loc.GetString("group-left"));
    }

    private void OnKick(GroupKickRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];

        if (!CanKick(userId, group))
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        if (!TryGetSessionByCharacterName(msg.TargetCharacterName, out var targetSession) || targetSession == null)
        {
            SendResult(session, false, Loc.GetString("group-player-not-found", ("name", msg.TargetCharacterName)));
            return;
        }

        var targetUserId = targetSession.UserId;

        if (targetUserId == userId)
        {
            SendResult(session, false, Loc.GetString("group-cannot-kick-self"));
            return;
        }

        if (!_playerToGroup.TryGetValue(targetUserId, out var targetGroupId) || targetGroupId != groupId)
        {
            SendResult(session, false, Loc.GetString("group-target-not-in-group", ("name", msg.TargetCharacterName)));
            return;
        }

        // Clear overlay and state for the kicked player.
        _overlayEnabled.Remove(targetUserId);
        RaiseNetworkEvent(new GroupOverlayUpdateEvent { GroupMembers = EmptyOverlay }, targetSession);
        RaiseNetworkEvent(new GroupStateUpdateEvent(), targetSession);
        SendResult(targetSession, false, Loc.GetString("group-you-were-kicked"));

        RemoveMemberFromGroup(targetUserId, groupId);
        SendResult(session, true, Loc.GetString("group-kicked", ("name", msg.TargetCharacterName)));
    }

    private void OnRoleChange(GroupRoleChangeRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];
        if (!CanManageRoles(userId, group))
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        if (!TryGetSessionByCharacterName(msg.TargetCharacterName, out var targetSession) || targetSession == null)
        {
            SendResult(session, false, Loc.GetString("group-player-not-found", ("name", msg.TargetCharacterName)));
            return;
        }

        var targetUserId = targetSession.UserId;
        if (targetUserId == userId)
        {
            SendResult(session, false, Loc.GetString("group-cannot-change-own-role"));
            return;
        }

        if (!_playerToGroup.TryGetValue(targetUserId, out var targetGroupId) || targetGroupId != groupId)
        {
            SendResult(session, false, Loc.GetString("group-target-not-in-group", ("name", msg.TargetCharacterName)));
            return;
        }

        if (msg.TargetRole == GroupMemberRole.Leader)
        {
            SendResult(session, false, Loc.GetString("group-invalid-role-change"));
            return;
        }

        var currentRole = GetMemberRole(group, targetUserId);
        if (currentRole == msg.TargetRole)
        {
            SendResult(session, false, Loc.GetString("group-role-already-set", ("name", msg.TargetCharacterName)));
            return;
        }

        group.Roles[targetUserId] = msg.TargetRole;
        BroadcastStateToGroup(groupId);
        SendResult(session, true, Loc.GetString("group-role-updated", ("name", msg.TargetCharacterName), ("role", GetRoleName(msg.TargetRole))));
    }

    private void OnRename(GroupRenameRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];
        if (!CanManageRoles(userId, group))
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        var name = msg.NewName.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 24)
        {
            SendResult(session, false, Loc.GetString("group-name-invalid"));
            return;
        }

        group.Name = name;
        BroadcastStateToGroup(groupId);
        SendResult(session, true, Loc.GetString("group-name-updated", ("name", name)));
    }

    private void OnSetRallyPoint(GroupSetRallyPointRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];
        if (!CanSetRallyPoint(userId, group))
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        group.RallyPoint = msg.Coordinates;
        BroadcastStateToGroup(groupId);
        SendResult(session, true, Loc.GetString("group-rally-point-set"));
    }

    private void OnClearRallyPoint(GroupClearRallyPointRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];
        if (!CanSetRallyPoint(userId, group))
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        group.RallyPoint = null;
        BroadcastStateToGroup(groupId);
        SendResult(session, true, Loc.GetString("group-rally-point-cleared"));
    }

    private void OnToggleOverlay(GroupToggleOverlayRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        if (msg.Enabled)
        {
            _overlayEnabled.Add(userId);
        }
        else
        {
            _overlayEnabled.Remove(userId);
            RaiseNetworkEvent(new GroupOverlayUpdateEvent { GroupMembers = EmptyOverlay }, args.SenderSession);
        }
    }

    // ── Group state helpers ────────────────────────────────────────────────

    /// <summary>
    /// Removes a member from their group, handles leadership transfer, and disbands if empty.
    /// </summary>
    private void RemoveMemberFromGroup(NetUserId userId, int groupId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return;

        group.MemberOrder.Remove(userId);
        _playerToGroup.Remove(userId);
        group.Roles.Remove(userId);

        if (group.MemberOrder.Count == 0)
        {
            // Group is empty — disband.
            _groups.Remove(groupId);
            return;
        }

        // Transfer leadership to the next oldest member if the leader left.
        if (group.LeaderUserId == userId)
        {
            group.LeaderUserId = group.MemberOrder[0];
            group.Roles[group.LeaderUserId] = GroupMemberRole.Leader;
        }

        BroadcastStateToGroup(groupId);
    }

    /// <summary>Sends current group state to all members of the group.</summary>
    private void BroadcastStateToGroup(int groupId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return;

        // Send individual state to each member.
        foreach (var memberId in group.MemberOrder)
        {
            if (!TryGetSession(memberId, out var ms) || ms == null)
                continue;

            var ev = new GroupStateUpdateEvent
            {
                Members      = BuildMembers(group),
                LeaderUserId = group.LeaderUserId,
                GroupName    = group.Name,
                RallyPoint   = group.RallyPoint,
            };

            RaiseNetworkEvent(ev, ms);
        }
    }

    /// <summary>Sends current state to a single session (includes pending invite info if any).</summary>
    private void SendStateTo(ICommonSession session)
    {
        var userId = session.UserId;
        var ev     = new GroupStateUpdateEvent();

        if (_playerToGroup.TryGetValue(userId, out var groupId) &&
            _groups.TryGetValue(groupId, out var group))
        {
            ev.LeaderUserId = group.LeaderUserId;
            ev.GroupName = group.Name;
            ev.RallyPoint = group.RallyPoint;
            ev.Members = BuildMembers(group);
        }

        // Check for a pending invite addressed to this player.
        var invite = _pendingInvites.FirstOrDefault(i => i.TargetUserId == userId);
        if (invite != null && TryGetSession(invite.InviterUserId, out var inviterSession) && inviterSession != null)
        {
            ev.PendingInviteFromName   = inviterSession.AttachedEntity is { } ie ? Name(ie) : inviterSession.Name;
            ev.PendingInviteFromUserId = invite.InviterUserId;
        }

        RaiseNetworkEvent(ev, session);
    }

    private List<GroupMemberInfo> BuildMembers(GroupData group)
    {
        var members = new List<GroupMemberInfo>();
        foreach (var memberId in group.MemberOrder)
        {
            if (!TryGetSession(memberId, out var ms) || ms == null)
                continue;
            if (ms.AttachedEntity is not { } ent)
                continue;
            members.Add(new GroupMemberInfo(GetNetEntity(ent), Name(ent), GetMemberRole(group, memberId)));
        }

        return members;
    }

    private GroupMemberRole GetMemberRole(GroupData group, NetUserId userId)
    {
        if (group.LeaderUserId == userId)
            return GroupMemberRole.Leader;

        return group.Roles.TryGetValue(userId, out var role) ? role : GroupMemberRole.Member;
    }

    private static bool CanInvite(NetUserId userId, GroupData group)
    {
        return group.LeaderUserId == userId || (group.Roles.TryGetValue(userId, out var role) && role == GroupMemberRole.Officer);
    }

    private static bool CanKick(NetUserId userId, GroupData group)
    {
        return group.LeaderUserId == userId;
    }

    private static bool CanManageRoles(NetUserId userId, GroupData group)
    {
        return group.LeaderUserId == userId;
    }

    private static bool CanSetRallyPoint(NetUserId userId, GroupData group)
    {
        return group.LeaderUserId == userId || (group.Roles.TryGetValue(userId, out var role) && role == GroupMemberRole.Officer);
    }

    private static string GetRoleName(GroupMemberRole role)
    {
        return role switch
        {
            GroupMemberRole.Leader => "leader",
            GroupMemberRole.Officer => "officer",
            _ => "member",
        };
    }

    // ── Utilities ──────────────────────────────────────────────────────────

    private void SendResult(ICommonSession session, bool success, string message)
    {
        RaiseNetworkEvent(new GroupActionResultEvent { Success = success, Message = message }, session);
    }

    private bool TryGetSession(NetUserId userId, out ICommonSession? session)
    {
        return _playerManager.TryGetSessionById(userId, out session);
    }

    /// <summary>
    /// Finds an in-game session whose attached entity name matches (case-insensitive trim).
    /// </summary>
    private bool TryGetSessionByCharacterName(string characterName, out ICommonSession? session)
    {
        var trimmed = characterName.Trim();
        foreach (var s in _playerManager.Sessions)
        {
            if (s.Status != SessionStatus.InGame || s.AttachedEntity == null)
                continue;
            if (string.Equals(Name(s.AttachedEntity.Value).Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                session = s;
                return true;
            }
        }

        session = null;
        return false;
    }

    // ── Event subscriptions ────────────────────────────────────────────────

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _groups.Clear();
        _playerToGroup.Clear();
        _pendingInvites.Clear();
        _overlayEnabled.Clear();
        _nextGroupId = 1;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        var userId = args.Session.UserId;

        // Clean up pending invites involving this player.
        _pendingInvites.RemoveAll(i => i.InviterUserId == userId || i.TargetUserId == userId);
        _overlayEnabled.Remove(userId);

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
            return;

        // Clear stale overlay for the disconnecting player (no-op since they're gone, but safe).
        RemoveMemberFromGroup(userId, groupId);
    }
}
