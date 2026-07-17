// #Misfits Add - Shared network messages for the player-based war system.
// Syncs active player-vs-player war declarations between server and all connected clients,
// and carries GUI request/response traffic between client and server.
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.FactionWar;

// ── War history event tracking ─────────────────────────────────────────────

/// <summary>Type of war history event for logging and admin audit trail.</summary>
[Serializable, NetSerializable]
public enum WarHistoryEventType : byte
{
    Declared,      // War declaration (declarer, target, sides)
    Accepted,      // Target accepted war and named their side
    PlayerJoined,  // Player joined a side
    CeasefireProposed,  // One player proposed ceasefire
    CeasefireAccepted,  // Other player accepted ceasefire
    CeasefireRejected,  // Other player rejected ceasefire
    RaidCascaded,  // Raid auto-ended due to war ending
    Concluded,     // War ended (via ceasefire or manual admin)
}

/// <summary>Single war history event with timestamp and actor information.</summary>
[Serializable, NetSerializable]
public sealed class WarHistoryEvent
{
    public WarHistoryEventType EventType;
    public DateTime OccurredAtUtc;
    public NetUserId? ActorUserId;
    public string ActorUserName = string.Empty;
    public string ActorCharacterName = string.Empty;
    public string Details = string.Empty;  // Additional context (e.g., "Joined Side1")
}

// ── Network message types ──────────────────────────────────────────────────

/// <summary>War lifecycle phase.</summary>
[Serializable, NetSerializable]
public enum WarPhase : byte
{
    /// <summary>War declared but not yet active. /warjoin is open.</summary>
    Pending,
    /// <summary>War is active. /warjoin is closed.</summary>
    Active,
}

/// <summary>
/// A single active player-vs-player war, transmitted as part of <see cref="FactionWarStateUpdatedEvent"/>.
/// Wars are character-bound: participants are tracked by their character entity, not account.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarEntry
{
    /// <summary>NetUserId of the account that declared the war (kept for session lookup).</summary>
    public NetUserId DeclaredByPlayer;

    /// <summary>Character entity of the declarer at time of declaration (character-bound).</summary>
    public NetEntity DeclaredByEntity;

    /// <summary>Character name of declarer (at time of declaration).</summary>
    public string DeclaredByCharacterName = string.Empty;

    /// <summary>Job name of declarer (at time of declaration).</summary>
    public string DeclaredByJobName = string.Empty;

    /// <summary>NetUserId of the account war was declared against (kept for session lookup).</summary>
    public NetUserId DeclaredAgainstPlayer;

    /// <summary>Character entity of the target at time of declaration (character-bound).</summary>
    public NetEntity DeclaredAgainstEntity;

    /// <summary>Character name of target player (at time of declaration).</summary>
    public string DeclaredAgainstCharacterName = string.Empty;

    /// <summary>Custom name for the declarer's side.</summary>
    public string SideName1 = string.Empty;

    /// <summary>Custom name for the target player's side.</summary>
    public string SideName2 = string.Empty;

    /// <summary>Auto-enlist faction ID for side 1 (null if declarer has no auto-enlist faction).</summary>
    public string? Side1FactionId;

    /// <summary>Auto-enlist faction ID for side 2 (null if target has no auto-enlist faction).</summary>
    public string? Side2FactionId;

    /// <summary>Player-supplied reason for the war declaration.</summary>
    public string Reason = string.Empty;

    /// <summary>Current phase of this war (Pending during 5-min prep, Active after).</summary>
    public WarPhase Phase = WarPhase.Pending;

    /// <summary>All participants in this war with their chosen sides (1 or 2), keyed by character entity.</summary>
    public Dictionary<NetEntity, byte> Participants = new();

    /// <summary>War history events for admin audit trail.</summary>
    public List<WarHistoryEvent> History = new();

    /// <summary>Unique identifier for this war, derived from the two character entities.</summary>
    public string WarKey => GetWarKey(DeclaredByEntity, DeclaredAgainstEntity);

    /// <summary>Generate canonical war key from two character entities (order-independent).</summary>
    public static string GetWarKey(NetEntity e1, NetEntity e2)
    {
        var id1 = e1.Id < e2.Id ? e1.Id : e2.Id;
        var id2 = e1.Id < e2.Id ? e2.Id : e1.Id;
        return $"e{id1}_e{id2}";
    }
}

/// <summary>
/// Server → all clients. Sent whenever the war state changes: declaration, ceasefire, or admin override.
/// Clients replace their entire local war list with <see cref="ActiveWars"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarStateUpdatedEvent : EntityEventArgs
{
    public List<PlayerWarEntry> ActiveWars = new();
}

// ── GUI request/response messages ─────────────────────────────────────────

/// <summary>
/// Client → server. Player opened the war panel and needs list of all players to target.
/// Server responds with <see cref="PlayerWarPanelDataEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarOpenPanelRequestEvent : EntityEventArgs { }

/// <summary>
/// Server → requesting client. Pre-computed panel state including list of online players
/// and active wars with their sides.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarPanelDataEvent : EntityEventArgs
{
    public string? MyCharacterName;
    public List<PlayerWarEntry> ActiveWars = new();
    public List<OnlinePlayerInfo> OnlinePlayers = new();
    public string? StatusMessage;

    /// <summary>Wars where this player is a participant.</summary>
    public List<PlayerWarEntry> MyWars = new();
}

/// <summary>
/// An online player that can be targeted for war declaration.
/// </summary>
[Serializable, NetSerializable]
public sealed class OnlinePlayerInfo
{
    public NetUserId UserId;
    public string UserName = string.Empty;
    public string CharacterName = string.Empty;
}

/// <summary>
/// Client → server. Player submits the Declare War form.
/// Server validates and responds with <see cref="FactionWarCommandResultEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarDeclareRequestEvent : EntityEventArgs
{
    public NetUserId TargetPlayer;
    public string Reason = string.Empty;
    public string SideName1 = string.Empty;  // Declarer's side name
}

/// <summary>
/// BUI/Popup sent to target player: accept war and name their side?
/// </summary>
[Serializable, NetSerializable]
public sealed class WarAcceptancePromptEvent : EntityEventArgs
{
    public NetUserId DeclaredByPlayer;
    public string DeclaredByCharacterName = string.Empty;
    public string SideName1 = string.Empty;  // Declarer's side name
    public string Reason = string.Empty;
}

/// <summary>
/// Client → server. Target player accepts war and provides their side name.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarAcceptEvent : EntityEventArgs
{
    public NetUserId DeclaredByPlayer;
    public string SideName2 = string.Empty;  // Target's side name
}

/// <summary>
/// Client → server. Target player rejects war (BUI dismissal/timeout).
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarRejectEvent : EntityEventArgs
{
    public NetUserId DeclaredByPlayer;
}

/// <summary>
/// Client → server. Player requests a ceasefire (must be original war participant).
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarCeasefireRequestEvent : EntityEventArgs
{
    public NetUserId OtherPlayer;
}

/// <summary>
/// Server → the requesting client only. Delivers success/failure feedback.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarCommandResultEvent : EntityEventArgs
{
    public bool   Success = false;
    public string Message = string.Empty;
}

/// <summary>
/// Server → target player. Notification that the other war participant proposed ceasefire.
/// </summary>
[Serializable, NetSerializable]
public sealed class CeasefireProposalEvent : EntityEventArgs
{
    public NetUserId ProposingPlayer;
    public string ProposingPlayerName = string.Empty;
}

/// <summary>
/// Client → server. Target player accepts ceasefire proposal.
/// </summary>
[Serializable, NetSerializable]
public sealed class CeasefireAcceptedEvent : EntityEventArgs
{
    public NetUserId OtherPlayer;
}

/// <summary>
/// Client → server. Target player rejects ceasefire proposal.
/// </summary>
[Serializable, NetSerializable]
public sealed class CeasefireRejectedEvent : EntityEventArgs
{
    public NetUserId OtherPlayer;
}

// ── /warjoin network messages ─────────────────────────────────────────────

/// <summary>
/// Client → server. Player opened the warjoin panel and needs pending-war data.
/// Server responds with <see cref="PlayerWarJoinPanelDataEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarJoinPanelRequestEvent : EntityEventArgs { }

/// <summary>
/// Server → the requesting client. Pre-computed data for the warjoin panel.
/// Lists all active/pending wars they can join.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarJoinPanelDataEvent : EntityEventArgs
{
    public List<PlayerWarEntry> AvailableWars = new();
    public List<PlayerWarEntry> ActiveWars = new();
    public bool AlreadyInWar;
    public string? StatusMessage;
}

/// <summary>
/// Client → server. Player requests to join a war on a specific side.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarJoinRequestEvent : EntityEventArgs
{
    public string WarKey = string.Empty;  // War to join, identified by its key
    public byte ChosenSide;               // 1 or 2
}

/// <summary>
/// Server → the requesting client only. Warjoin-specific result feedback.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarJoinResultEvent : EntityEventArgs
{
    public bool   Success = false;
    public string Message = string.Empty;
}

/// <summary>
/// Per-entity participant info for war overlays.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarParticipantInfo
{
    public byte Side;
    public string WarKey = string.Empty;
    public bool Surrendered;
    /// <summary>
    /// #Misfits Add - Observer participants can see overlay tags but don't get one rendered on them.
    /// Used by admins to monitor wars without appearing as [ALLY]/[ENEMY].
    /// </summary>
    public bool IsObserver;
}

/// <summary>
/// Server → all clients. Broadcast whenever the war-participant list changes.
/// Maps each participant entity to their side and owning war.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarParticipantsUpdatedEvent : EntityEventArgs
{
    public Dictionary<NetEntity, FactionWarParticipantInfo> Participants = new();
}

// ── /forcewar admin network messages ──────────────────────────────────────

/// <summary>
/// Client → server. Admin requests to force-declare a war between two players.
/// Bypasses round-start cooldown, post-war cooldown, and all checks.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarForceRequestEvent : EntityEventArgs
{
    public NetUserId Player1;
    public string SideName1 = string.Empty;
    public NetUserId Player2;
    public string SideName2 = string.Empty;
    public string Reason = string.Empty;
}

/// <summary>
/// Server → the requesting admin client. Result feedback for the forcewar GUI.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarForceResultEvent : EntityEventArgs
{
    public bool   Success = false;
    public string Message = string.Empty;
    public bool   IsCeasefire = false;
}

/// <summary>
/// Client → server. Admin requests to forcibly end a war.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarForceCeasefireRequestEvent : EntityEventArgs
{
    public NetUserId Player1;
    public NetUserId Player2;
}

// ── /surrender network messages ──────────────────────────────────────────

/// <summary>
/// Client → server. Player surrenders in an active war.
/// Server will force them down, paralyze them, and mark them as surrendered.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarSurrenderRequestEvent : EntityEventArgs { }

/// <summary>
/// Server → client. Result of a surrender attempt.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarSurrenderResultEvent : EntityEventArgs
{
    public bool   Success = false;
    public string Message = string.Empty;
}

// ── /forceobservewar admin network messages ──────────────────────────────

/// <summary>
/// #Misfits Add - Client → server. Admin requests to observe a war from a participant's perspective.
/// The admin sending the request IS the observer — no need to specify separately.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerWarForceObserveRequestEvent : EntityEventArgs
{
    public NetUserId Participant;
}

/// <summary>
/// #Misfits Add - Server → the requesting admin client. Result feedback for the forceobservewar GUI.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionWarForceObserveResultEvent : EntityEventArgs
{
    public bool   Success = false;
    public string Message = string.Empty;
}
