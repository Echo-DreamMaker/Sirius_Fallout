// #Misfits Add - Client-side group system.
// Registers the /group command, manages the GroupWindow, and drives the GroupTagOverlay.
// All group logic is server-side; this system only processes state updates and routes
// user input to the server via network events.

using Content.Client._Misfits.Group.UI;
using Content.Shared._Misfits.Group;
using Content.Shared.Examine;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Group;

/// <summary>
/// Manages the <see cref="GroupTagOverlay"/> lifecycle and the <see cref="GroupWindow"/> GUI.
/// The /group client command opens the group panel.
/// </summary>
public sealed class GroupClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager      _overlayManager = default!;
    [Dependency] private readonly IPlayerManager       _playerManager  = default!;
    [Dependency] private readonly IEyeManager          _eyeManager     = default!;
    [Dependency] private readonly IResourceCache       _resourceCache  = default!;
    [Dependency] private readonly EntityLookupSystem   _entityLookup   = default!;
    [Dependency] private readonly ExamineSystemShared  _examine        = default!;
    [Dependency] private readonly SharedTransformSystem _transform     = default!;
    [Dependency] private readonly IClientConsoleHost   _conHost        = default!;
    [Dependency] private readonly IGameTiming          _timing         = default!;

    // ── Public state (read by GroupTagOverlay) ─────────────────────────────

    /// <summary>Map of group member NetEntities to their display names, for overlay rendering.</summary>
    public IReadOnlyDictionary<NetEntity, string> GroupParticipants => _groupParticipants;

    // ── Private state ──────────────────────────────────────────────────────

    private readonly Dictionary<NetEntity, string> _groupParticipants = new();
    private GroupWindow?     _window;
    private GroupTagOverlay? _overlay;

    // Cached invite info — needed to route the response event.
    private string?    _pendingInviteFromName;
    private NetUserId? _pendingInviteFromUserId;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GroupStateUpdateEvent>(OnStateUpdate);
        SubscribeNetworkEvent<GroupOverlayUpdateEvent>(OnOverlayUpdate);
        SubscribeNetworkEvent<GroupActionResultEvent>(OnActionResult);

        _conHost.RegisterCommand(
            "group",
            Loc.GetString("group-cmd-desc"),
            "group",
            OpenGroupPanel);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
        _window = null;
        RemoveOverlay();
    }

    // ── Network event handlers ─────────────────────────────────────────────

    private void OnStateUpdate(GroupStateUpdateEvent msg)
    {
        // Cache pending invite info.
        _pendingInviteFromName   = msg.PendingInviteFromName;
        _pendingInviteFromUserId = msg.PendingInviteFromUserId;

        _window?.UpdateState(
            msg.Members,
            msg.LeaderUserId,
            _playerManager.LocalSession?.UserId,
            _playerManager.LocalSession?.AttachedEntity is { } localEnt ? GetNetEntity(localEnt) : (NetEntity?) null,
            msg.GroupName,
            msg.PendingInviteFromName,
            msg.RallyPoint);
    }

    private void OnOverlayUpdate(GroupOverlayUpdateEvent msg)
    {
        _groupParticipants.Clear();
        foreach (var (entity, name) in msg.GroupMembers)
            _groupParticipants[entity] = name;

        UpdateOverlayVisibility();
    }

    private void OnActionResult(GroupActionResultEvent msg)
    {
        _window?.ShowResult(msg.Success, msg.Message);
    }

    // ── Overlay management ─────────────────────────────────────────────────

    private void UpdateOverlayVisibility()
    {
        var hasMembers = _groupParticipants.Count > 0;

        if (hasMembers && _overlay == null)
        {
            // Exclude the local player's own entity from the overlay.
            var localNet = _playerManager.LocalSession?.AttachedEntity is { } localEnt
                ? GetNetEntity(localEnt)
                : (NetEntity?) null;

            if (localNet.HasValue)
                _groupParticipants.Remove(localNet.Value);

            _overlay = new GroupTagOverlay(
                this,
                EntityManager,
                _playerManager,
                _eyeManager,
                _timing,
                _resourceCache,
                _entityLookup,
                _examine,
                _transform);
            _overlayManager.AddOverlay(_overlay);
        }
        else if (!hasMembers && _overlay != null)
        {
            RemoveOverlay();
        }
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;
        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }

    // ── /group command ─────────────────────────────────────────────────────

    private void OpenGroupPanel(IConsoleShell shell, string argStr, string[] args)
    {
        EnsureGroupWindow();
        _window!.OpenCentered();
        // Ask server for current state.
        RaiseNetworkEvent(new GroupOpenPanelRequestEvent());
    }

    // ── Window lifecycle ───────────────────────────────────────────────────

    private void EnsureGroupWindow()
    {
        if (_window != null)
            return;

        _window = new GroupWindow();
        _window.OnClose += () => _window = null;

        _window.OnCreate += () =>
            RaiseNetworkEvent(new GroupCreateRequestEvent());

        _window.OnInvite += name =>
            RaiseNetworkEvent(new GroupInviteRequestEvent { TargetCharacterName = name });

        _window.OnRename += name =>
            RaiseNetworkEvent(new GroupRenameRequestEvent { NewName = name });

        _window.OnInviteNearby += TryInviteNearbyPlayer;

        _window.OnAcceptInvite += () =>
        {
            if (_pendingInviteFromUserId.HasValue)
                RaiseNetworkEvent(new GroupInviteResponseEvent { Accept = true, InviterUserId = _pendingInviteFromUserId.Value });
        };

        _window.OnDeclineInvite += () =>
        {
            if (_pendingInviteFromUserId.HasValue)
                RaiseNetworkEvent(new GroupInviteResponseEvent { Accept = false, InviterUserId = _pendingInviteFromUserId.Value });
        };

        _window.OnLeave += () =>
            RaiseNetworkEvent(new GroupLeaveRequestEvent());

        _window.OnKick += name =>
            RaiseNetworkEvent(new GroupKickRequestEvent { TargetCharacterName = name });

        _window.OnChangeRole += (name, role) =>
            RaiseNetworkEvent(new GroupRoleChangeRequestEvent { TargetCharacterName = name, TargetRole = role });

        _window.OnSetRallyPoint += () =>
        {
            if (_playerManager.LocalSession?.AttachedEntity is not { } localEnt)
                return;
            var coords = _transform.GetMapCoordinates(localEnt);
            RaiseNetworkEvent(new GroupSetRallyPointRequestEvent { Coordinates = coords });
        };

        _window.OnClearRallyPoint += () =>
            RaiseNetworkEvent(new GroupClearRallyPointRequestEvent());

        _window.OnToggleOverlay += enabled =>
            RaiseNetworkEvent(new GroupToggleOverlayRequestEvent { Enabled = enabled });
    }

    private void TryInviteNearbyPlayer()
    {
        if (_playerManager.LocalSession?.AttachedEntity is not { } localEnt)
            return;

        var localPos = _transform.GetMapCoordinates(localEnt);
        var bestDistance = float.MaxValue;
        string? bestName = null;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var actor, out var xform))
        {
            if (uid == localEnt)
                continue;

            if (xform.MapID != localPos.MapId)
                continue;

            var otherPos = _transform.GetMapCoordinates(uid, xform).Position;
            var distance = (otherPos - localPos.Position).LengthSquared();
            if (distance > 36f * 36f)
                continue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestName = Name(uid);
        }

        if (string.IsNullOrWhiteSpace(bestName))
        {
            _window?.ShowResult(false, Loc.GetString("group-invite-nearby-none"));
            return;
        }

        RaiseNetworkEvent(new GroupInviteRequestEvent { TargetCharacterName = bestName });
        _window?.ShowResult(true, Loc.GetString("group-invite-nearby-sent", ("name", bestName)));
    }
}
