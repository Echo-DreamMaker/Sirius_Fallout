// #Misfits Refactor - Client-side player war system.
// Receives player-war state syncs and routes panel requests/results.

using System.Linq;
using Content.Client._Misfits.FactionWar.UI;
using Content.Shared._Misfits.FactionWar;
using Content.Shared.Examine;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.FactionWar;

/// <summary>
/// Manages the <see cref="AllyTagOverlay"/> and the <see cref="FactionWarWindow"/>/<see cref="WarJoinWindow"/> GUIs.
/// The /war client command opens the faction war panel; /warjoin opens the enlistment panel.
/// All game-logic validation stays server-side.
/// </summary>
public sealed class FactionWarClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager     _overlayManager = default!;
    [Dependency] private readonly IPlayerManager      _playerManager  = default!;
    [Dependency] private readonly IEyeManager         _eyeManager     = default!;
    [Dependency] private readonly IResourceCache      _resourceCache  = default!;
    [Dependency] private readonly EntityLookupSystem  _entityLookup   = default!;
    [Dependency] private readonly ExamineSystemShared _examine        = default!;
    [Dependency] private readonly SharedTransformSystem _transform    = default!;
    [Dependency] private readonly IClientConsoleHost  _conHost        = default!;
    [Dependency] private readonly IGameTiming         _timing         = default!;

    public IReadOnlyList<PlayerWarEntry> ActiveWars => _activeWars;
    public byte? LocalWarJoinSide { get; private set; }
    public string? LocalWarKey { get; private set; }
    public IReadOnlyDictionary<NetEntity, FactionWarParticipantInfo> WarParticipants => _warParticipants;

    private List<PlayerWarEntry> _activeWars = new();
    private Dictionary<NetEntity, FactionWarParticipantInfo> _warParticipants = new();
    private AllyTagOverlay?    _overlay;
    private FactionWarWindow?  _window;
    private WarJoinWindow?     _warJoinWindow;
    private ForceWarWindow?    _forceWarWindow;
    private CeasefireProposalEvent? _pendingCeasefireProposal;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FactionWarStateUpdatedEvent>(OnWarStateUpdated);
        SubscribeNetworkEvent<PlayerWarPanelDataEvent>(OnPanelData);
        SubscribeNetworkEvent<FactionWarCommandResultEvent>(OnCommandResult);
        SubscribeNetworkEvent<PlayerWarJoinPanelDataEvent>(OnJoinPanelData);
        SubscribeNetworkEvent<FactionWarJoinResultEvent>(OnJoinResult);
        SubscribeNetworkEvent<FactionWarParticipantsUpdatedEvent>(OnParticipantsUpdated);
        SubscribeNetworkEvent<FactionWarForceResultEvent>(OnForceWarResult);
        SubscribeNetworkEvent<CeasefireProposalEvent>(OnCeasefireProposal);
        SubscribeNetworkEvent<FactionWarForceObserveResultEvent>(OnForceObserveResult); // #Misfits Add

        _conHost.RegisterCommand(
            "war",
            Loc.GetString("faction-war-cmd-desc"),
            "war",
            OpenWarPanel);

        _conHost.RegisterCommand(
            "warjoin",
            Loc.GetString("faction-war-join-cmd-desc"),
            "warjoin",
            OpenWarJoinPanel);

        _conHost.RegisterCommand(
            "forcewar",
            "Open the admin Force War panel.",
            "forcewar",
            OpenForceWarPanel);

        _conHost.RegisterCommand(
            "surrender",
            "Surrender in an active war. You will be forced down, incapacitated, and marked as [SURRENDERED].",
            "surrender",
            OpenSurrender);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
        _window = null;
        _warJoinWindow?.Close();
        _warJoinWindow = null;
        _forceWarWindow?.Close();
        _forceWarWindow = null;
        RemoveOverlay();
    }

    // ── Network event handlers ─────────────────────────────────────────────

    private void OnWarStateUpdated(FactionWarStateUpdatedEvent msg)
    {
        _activeWars = msg.ActiveWars;
        UpdateLocalWarContext();
        UpdateOverlayVisibility();

        // Refresh war panel if open so Active Wars list repopulates after respawn/state change.
        if (_window != null)
            RaiseNetworkEvent(new FactionWarOpenPanelRequestEvent());

        // Refresh warjoin panel if open (pending wars may have changed phase).
        if (_warJoinWindow != null)
            RaiseNetworkEvent(new FactionWarJoinPanelRequestEvent());

        // Keep force-war ceasefire dropdown current.
        _forceWarWindow?.UpdateActiveWars(_activeWars);
    }

    private void OnPanelData(PlayerWarPanelDataEvent msg)
    {
        _activeWars = msg.ActiveWars;

        UpdateLocalWarContext();

        UpdateOverlayVisibility();

        _forceWarWindow?.UpdateOnlinePlayers(msg.OnlinePlayers);
        _forceWarWindow?.UpdateActiveWars(_activeWars);

        if (_window == null)
            return;

        _window.UpdateState(msg, _playerManager.LocalSession?.UserId, _pendingCeasefireProposal);

        if (msg.StatusMessage != null)
            _window.ShowResult(false, msg.StatusMessage);
    }

    private void OnCommandResult(FactionWarCommandResultEvent msg)
    {
        _window?.ShowResult(msg.Success, msg.Message);
    }

    private void OnJoinPanelData(PlayerWarJoinPanelDataEvent msg)
    {
        if (_warJoinWindow == null)
            return;

        _warJoinWindow.UpdateState(msg);

        UpdateLocalWarContext();

        UpdateOverlayVisibility();
    }

    private void OnJoinResult(FactionWarJoinResultEvent msg)
    {
        _warJoinWindow?.ShowResult(msg.Success, msg.Message);

        // If join succeeded, refresh panel data to update the UI state.
        if (msg.Success)
            RaiseNetworkEvent(new FactionWarJoinPanelRequestEvent());
    }

    private void OnParticipantsUpdated(FactionWarParticipantsUpdatedEvent msg)
    {
        _warParticipants = msg.Participants;
        UpdateLocalWarContext();
        UpdateOverlayVisibility();
    }

    private void OnCeasefireProposal(CeasefireProposalEvent msg)
    {
        _pendingCeasefireProposal = msg;
        EnsureWarWindow();
        _window!.OpenCentered();
        RaiseNetworkEvent(new FactionWarOpenPanelRequestEvent());
    }

    private void OnForceWarResult(FactionWarForceResultEvent msg)
    {
        // Route to the correct result label based on which action triggered this.
        if (msg.IsCeasefire)
            _forceWarWindow?.ShowCeasefireResult(msg.Success, msg.Message);
        else
            _forceWarWindow?.ShowResult(msg.Success, msg.Message);
    }

    // #Misfits Add - Handle observe result from server.
    private void OnForceObserveResult(FactionWarForceObserveResultEvent msg)
    {
        _forceWarWindow?.ShowObserveResult(msg.Success, msg.Message);
    }

    // ── /war client command ────────────────────────────────────────────────

    private void OpenWarPanel(IConsoleShell shell, string argStr, string[] args)
    {
        EnsureWarWindow();
        _window!.OpenCentered();

        // Ask server for fresh panel data (faction detection must happen server-side).
        RaiseNetworkEvent(new FactionWarOpenPanelRequestEvent());
    }

    // ── /warjoin client command ────────────────────────────────────────────

    private void OpenWarJoinPanel(IConsoleShell shell, string argStr, string[] args)
    {
        EnsureWarJoinWindow();
        _warJoinWindow!.OpenCentered();

        RaiseNetworkEvent(new FactionWarJoinPanelRequestEvent());
    }

    // ── /forcewar client command (admin) ────────────────────────────────────

    private void OpenForceWarPanel(IConsoleShell shell, string argStr, string[] args)
    {
        EnsureForceWarWindow();
        _forceWarWindow!.OpenCentered();

        // Populate the admin panel with the latest online players and active wars.
        RaiseNetworkEvent(new FactionWarOpenPanelRequestEvent());
    }

    // ── /surrender client command ──────────────────────────────────────────

    private void OpenSurrender(IConsoleShell shell, string argStr, string[] args)
    {
        RaiseNetworkEvent(new PlayerWarSurrenderRequestEvent());
    }

    // ── Window lifecycle ───────────────────────────────────────────────────

    private void EnsureWarWindow()
    {
        if (_window != null)
            return;

        _window = new FactionWarWindow();
        _window.OnClose += () => _window = null;

        _window.OnDeclareWar += (targetPlayer, reason, sideName1) =>
        {
            RaiseNetworkEvent(new PlayerWarDeclareRequestEvent
            {
                TargetPlayer = targetPlayer,
                Reason = reason,
                SideName1 = sideName1,
            });
        };

        _window.OnCeasefire += otherPlayer =>
        {
            RaiseNetworkEvent(new PlayerWarCeasefireRequestEvent
            {
                OtherPlayer = otherPlayer,
            });
        };

        _window.OnAcceptCeasefireProposal += otherPlayer =>
        {
            RaiseNetworkEvent(new CeasefireAcceptedEvent
            {
                OtherPlayer = otherPlayer,
            });
        };

        _window.OnRejectCeasefireProposal += otherPlayer =>
        {
            RaiseNetworkEvent(new CeasefireRejectedEvent
            {
                OtherPlayer = otherPlayer,
            });
        };
    }

    private void EnsureWarJoinWindow()
    {
        if (_warJoinWindow != null)
            return;

        _warJoinWindow = new WarJoinWindow();
        _warJoinWindow.OnClose += () => _warJoinWindow = null;

        _warJoinWindow.OnJoinWar += (warKey, chosenSide) =>
        {
            RaiseNetworkEvent(new PlayerWarJoinRequestEvent
            {
                WarKey = warKey,
                ChosenSide = chosenSide,
            });
        };
    }

    private void EnsureForceWarWindow()
    {
        if (_forceWarWindow != null)
            return;

        _forceWarWindow = new ForceWarWindow();
        _forceWarWindow.OnClose += () => _forceWarWindow = null;

        _forceWarWindow.OnForceWar += (player1, side1, player2, side2, reason) =>
        {
            RaiseNetworkEvent(new PlayerWarForceRequestEvent
            {
                Player1 = player1,
                SideName1 = side1,
                Player2 = player2,
                SideName2 = side2,
                Reason = reason,
            });
        };

        _forceWarWindow.OnForceCeasefire += (player1, player2) =>
        {
            RaiseNetworkEvent(new PlayerWarForceCeasefireRequestEvent
            {
                Player1 = player1,
                Player2 = player2,
            });
        };

        // #Misfits Add - Admin observe war. Observer is always the admin using the panel.
        _forceWarWindow.OnForceObserve += participant =>
        {
            RaiseNetworkEvent(new PlayerWarForceObserveRequestEvent
            {
                Participant = participant,
            });
        };

        // Populate the ceasefire dropdown with current wars.
        _forceWarWindow.UpdateActiveWars(_activeWars);
    }

    // ── Overlay lifecycle ──────────────────────────────────────────────────

    private void UpdateOverlayVisibility()
    {
        if (_activeWars.Count == 0 || _warParticipants.Count == 0)
        {
            RemoveOverlay();
            return;
        }

        EnsureOverlay();
    }

    private void EnsureOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new AllyTagOverlay(
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

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay<AllyTagOverlay>();
        _overlay = null;
    }

    /// <summary>
    /// Trigger a refresh of the overlay lifecycle. Public so other client systems
    /// can prompt the war system to re-evaluate whether the overlay should be present.
    /// </summary>
    public void RefreshOverlay()
    {
        UpdateOverlayVisibility();
    }

    private void UpdateLocalWarContext()
    {
        LocalWarKey = null;
        LocalWarJoinSide = null;

        if (_playerManager.LocalSession?.AttachedEntity is not { } localEntity)
            return;

        var localNetEntity = GetNetEntity(localEntity);

        foreach (var war in _activeWars)
        {
            if (!war.Participants.TryGetValue(localNetEntity, out var side))
                continue;

            LocalWarKey = war.WarKey;
            LocalWarJoinSide = side;
            return;
        }
    }
}
