// #Misfits Add - PipBoy Hub Cartridge System.
// Handles all UI messages for the PipBoy Hub cartridge (contacts, groups, directory, password,
// SOS beacon, waypoints, dead drops, encrypted channels, status, radio relay).
using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.Chat.Managers; // #Misfits Fix - direct chat notification
using Content.Server.PDA.Ringer; // #Misfits Fix - direct ringer playback
using Content.Shared._Misfits.PipBoy;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader; // #Misfits Change - also provides CartridgeLoaderComponent for notifications
using Content.Shared.Chat; // #Misfits Fix - ChatChannel for direct notification
using Content.Shared.Database;
using Content.Shared.DeltaV.CartridgeLoader.Cartridges;
using Content.Shared.DeltaV.NanoChat;
using Content.Shared.PDA;
using Robust.Server.GameObjects; // #Misfits Fix - ActorComponent lookup
using Robust.Shared.Containers; // #Misfits Fix - container traversal
using Robust.Shared.Player; // #Misfits Fix - player session
using Robust.Shared.Timing;
using Robust.Shared.Utility; // #Misfits Fix - FormattedMessage

namespace Content.Server._Misfits.PipBoy;

public sealed class PipBoyHubCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridge = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // #Misfits Fix - direct chat notifications
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PipBoyNetworkSystem _network = default!;
    [Dependency] private readonly RingerSystem _ringer = default!; // #Misfits Fix - direct ringer playback
    [Dependency] private readonly SharedContainerSystem _container = default!; // #Misfits Fix - container lookup
    [Dependency] private readonly SharedNanoChatSystem _nanoChat = default!;

    private const int MaxNameLength = 42;
    private const int MaxPasswordLength = 32;
    private const int MaxNoteLength = 256;
    private const int MaxStatusLength = 64;
    private const int MaxWaypointLabelLength = 32;
    private const int NotificationMaxLength = 64; // #Misfits Add - max length for notification body text

    // #Misfits Add - Shared empty sentinels for UpdateUI's locked/no-card placeholder paths.
    // These replace ten per-call `new List/Dictionary` allocations that were executed on every
    // PipBoy UI refresh (roughly once per second per active user). They are never mutated —
    // the unlocked path reassigns the local variables to live collections returned from the
    // network system. With ~150 concurrent users this eliminates ~1500 allocations/sec.
    private static readonly Dictionary<uint, PipBoyContact> EmptyContacts = new();
    private static readonly List<PipBoyTrackedLocation> EmptyTrackedLocations = new();
    private static readonly List<PipBoyGroupInfo> EmptyGroups = new();
    private static readonly List<PipBoyGroupMessage> EmptyGroupMessages = new();
    private static readonly List<PipBoyWaypoint> EmptyWaypoints = new();
    private static readonly List<PipBoyDirectoryEntry> EmptyDirectory = new();
    private static readonly List<PipBoySosAlert> EmptySosAlerts = new();
    private static readonly List<PipBoyDeadDrop> EmptyDeadDrops = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PipBoyHubCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<PipBoyHubCartridgeComponent, CartridgeMessageEvent>(OnMessage);
    }

    // Misfits Fix: card-reference sync only needs to run at low frequency;
    // contained-ID changes are user actions, not per-tick events.
    private float _cardSyncAccum;
    private const float CardSyncInterval = 0.5f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Misfits Fix: gate to 2 Hz (every 0.5 s) — card swaps are rare user actions.
        _cardSyncAccum += frameTime;
        if (_cardSyncAccum < CardSyncInterval)
            return;
        _cardSyncAccum -= CardSyncInterval;

        // Keep card references in sync (same pattern as NanoChatCartridgeSystem)
        var query = EntityQueryEnumerator<PipBoyHubCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var hub, out var cartridge))
        {
            if (cartridge.LoaderUid == null)
                continue;

            if (!TryComp<PdaComponent>(cartridge.LoaderUid, out var pda))
                continue;

            var newCard = pda.ContainedId;
            if (newCard == hub.Card)
                continue;

            hub.Card = newCard;
            UpdateUI((uid, hub), cartridge.LoaderUid.Value);
        }
    }

    private void OnUiReady(Entity<PipBoyHubCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUI(ent, args.Loader);
    }

    private void OnMessage(Entity<PipBoyHubCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not PipBoyHubUiMessageEvent msg)
            return;

        if (!GetCard(GetEntity(args.LoaderUid), out var cardUid, out var cardComp, out var netComp))
            return;

        // #Misfits Fix - Enforce the lock server-side. When the Pip-Boy is locked, the ONLY
        // permitted action is attempting to unlock with the password. All other messages
        // (removing/changing the password, contacts, groups, directory, SOS, dead drops,
        // status, etc.) are rejected while locked. Previously any client could send
        // PasswordSet("") to clear the password and unlock a Pip-Boy without knowing it
        // (e.g. by opening the Settings program on a handed-over Pip-Boy and using the
        // Remove Password action), so the lock was purely cosmetic and trivially bypassed.
        if (netComp.IsLocked && msg.Type != PipBoyHubMessageType.PasswordUnlock)
            return;

        var ownNumber = cardComp.Number ?? 0;

        switch (msg.Type)
        {
            // ── Contacts ──
            case PipBoyHubMessageType.ContactAdd:
                HandleContactAdd(ownNumber, msg);
                break;
            case PipBoyHubMessageType.ContactAccept:
                HandleContactAccept(ownNumber, msg);
                break;
            case PipBoyHubMessageType.ContactReject:
                HandleContactReject(ownNumber, msg);
                break;
            case PipBoyHubMessageType.ContactToggleLocation:
                HandleContactToggleLocation(ownNumber, msg);
                break;
            case PipBoyHubMessageType.ContactSendMessage:
                HandleContactSendMessage(cardUid, cardComp, msg);
                break;

            // ── Groups ──
            case PipBoyHubMessageType.GroupCreate:
                HandleGroupCreate(cardUid, netComp, ownNumber, msg);
                break;
            case PipBoyHubMessageType.GroupJoin:
                HandleGroupJoin(cardUid, netComp, ownNumber, msg);
                break;
            case PipBoyHubMessageType.GroupLeave:
                HandleGroupLeave(ent, cardUid, netComp, ownNumber, msg);
                break;
            case PipBoyHubMessageType.GroupToggleMapTracking:
                HandleGroupToggleMapTracking(cardUid, netComp, msg);
                break;
            case PipBoyHubMessageType.GroupSendMessage:
                HandleGroupSendMessage(ownNumber, cardUid, msg, GetEntity(args.LoaderUid));
                break;
            case PipBoyHubMessageType.GroupSelect:
                HandleGroupSelect(ent, msg);
                break;
            case PipBoyHubMessageType.GroupAddWaypoint:
                HandleGroupAddWaypoint(cardUid, ownNumber, msg);
                break;
            case PipBoyHubMessageType.GroupRemoveWaypoint:
                HandleGroupRemoveWaypoint(msg);
                break;
            case PipBoyHubMessageType.GroupSetPassword:
                HandleGroupSetPassword(ownNumber, msg);
                break;

            // ── Directory ──
            case PipBoyHubMessageType.DirectoryToggleVisibility:
                HandleToggleVisibility(cardUid, netComp);
                break;
            case PipBoyHubMessageType.DirectorySendMessage:
                HandleDirectorySendMessage(cardUid, cardComp, msg);
                break;

            // ── Password / Lock ──
            case PipBoyHubMessageType.PasswordSet:
                HandlePasswordSet(cardUid, netComp, msg);
                break;
            case PipBoyHubMessageType.PasswordUnlock:
                HandlePasswordUnlock(cardUid, netComp, msg);
                break;
            case PipBoyHubMessageType.Lock:
                _network.Lock((cardUid, netComp));
                break;

            // ── SOS ──
            case PipBoyHubMessageType.SosBeacon:
                HandleSosBeacon(cardUid, ownNumber);
                break;

            // ── Status ──
            case PipBoyHubMessageType.SetStatus:
                HandleSetStatus(ownNumber, msg);
                break;

            // ── Dead Drops ──
            case PipBoyHubMessageType.DeadDropCreate:
                HandleDeadDropCreate(cardUid, ownNumber, msg);
                break;
            case PipBoyHubMessageType.DeadDropCollect:
                HandleDeadDropCollect(ownNumber, msg);
                break;

            // ── Contact Notes ──
            case PipBoyHubMessageType.ContactSetNote:
                HandleContactSetNote(ownNumber, msg);
                break;
        }

        UpdateUI(ent, GetEntity(args.LoaderUid));
    }

    #region Handlers

    private void HandleContactAdd(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.TargetNumber == null)
            return;

        if (!_network.SendContactRequest(ownNumber, msg.TargetNumber.Value))
            return;

        // #Misfits Add - Notify the target that they have an incoming contact request
        var targetCard = _network.FindCardByNumber(msg.TargetNumber.Value);
        var senderCard = _network.FindCardByNumber(ownNumber);
        if (targetCard != null && senderCard != null)
        {
            var senderInfo = _network.GetCardDisplayInfo(senderCard.Value);
            NotifyCardHolder(targetCard.Value,
                Loc.GetString("pipboy-notif-contact-request-title"),
                Loc.GetString("pipboy-notif-contact-request-body", ("sender", senderInfo.Name)));
        }
    }

    private void HandleContactAccept(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.TargetNumber == null)
            return;

        _network.AcceptContactRequest(ownNumber, msg.TargetNumber.Value);
    }

    private void HandleContactReject(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.TargetNumber == null)
            return;

        _network.RemoveContact(ownNumber, msg.TargetNumber.Value);
    }

    private void HandleContactToggleLocation(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.TargetNumber == null)
            return;

        _network.ToggleLocationSharing(ownNumber, msg.TargetNumber.Value);
    }

    /// <summary>Send a quick message to a contact via NanoChat.</summary>
    private void HandleContactSendMessage(EntityUid cardUid, NanoChatCardComponent cardComp, PipBoyHubUiMessageEvent msg)
    {
        if (msg.TargetNumber == null || string.IsNullOrWhiteSpace(msg.Content) || cardComp.Number == null)
            return;

        var content = msg.Content.Trim();
        if (content.Length > NanoChatMessage.MaxContentLength)
            content = content[..NanoChatMessage.MaxContentLength];

        // Create the NanoChat message and store on sender
        var nanoChatMsg = new NanoChatMessage(_timing.CurTime, content, cardComp.Number.Value);

        // Ensure recipient exists in NanoChat
        _nanoChat.EnsureRecipientExists((cardUid, cardComp), msg.TargetNumber.Value, GetNanoChatRecipientInfo(msg.TargetNumber.Value));

        _nanoChat.AddMessage((cardUid, cardComp), msg.TargetNumber.Value, nanoChatMsg);

        // Deliver to recipient
        var targetCard = _network.FindCardByNumber(msg.TargetNumber.Value);
        if (targetCard != null && TryComp<NanoChatCardComponent>(targetCard.Value, out var targetChatComp))
        {
            _nanoChat.EnsureRecipientExists((targetCard.Value, targetChatComp), cardComp.Number.Value, GetNanoChatRecipientInfo(cardComp.Number.Value));
            _nanoChat.AddMessage((targetCard.Value, targetChatComp), cardComp.Number.Value, nanoChatMsg with { DeliveryFailed = false });

            // #Misfits Add - Notify recipient of incoming message
            var senderInfo = _network.GetCardDisplayInfo(cardUid);
            NotifyCardHolder(targetCard.Value,
                Loc.GetString("pipboy-notif-message-title", ("sender", senderInfo.Name)),
                Truncate(content, NotificationMaxLength));
        }
    }

    private void HandleGroupCreate(EntityUid cardUid, PipBoyNetworkComponent netComp, uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Content))
            return;

        // #Misfits Change - Support custom group ID: content format is "name\0customId"
        var content = msg.Content;
        uint customId = 0;
        var separatorIdx = content.IndexOf('\0');
        if (separatorIdx >= 0)
        {
            var idPart = content[(separatorIdx + 1)..];
            uint.TryParse(idPart, out customId);
            content = content[..separatorIdx];
        }

        var name = content.Trim();
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength];

        uint groupId;
        if (customId > 0)
        {
            groupId = _network.CreateGroupWithId(customId, name, ownNumber);
            if (groupId == 0)
                return; // ID already taken, silently fail
        }
        else
        {
            groupId = _network.CreateGroup(name, ownNumber);
        }

        netComp.Groups[groupId] = new PipBoyGroupMembership(groupId, name);
        Dirty(cardUid, netComp);
    }

    private void HandleGroupJoin(EntityUid cardUid, PipBoyNetworkComponent netComp, uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.GroupId == null)
            return;

        if (!_network.JoinGroup(msg.GroupId.Value, ownNumber, msg.Content))
            return;

        var group = _network.GetGroup(msg.GroupId.Value);
        if (group == null)
            return;

        netComp.Groups[msg.GroupId.Value] = new PipBoyGroupMembership(msg.GroupId.Value, group.GroupName);
        Dirty(cardUid, netComp);
    }

    private void HandleGroupLeave(Entity<PipBoyHubCartridgeComponent> ent, EntityUid cardUid, PipBoyNetworkComponent netComp, uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.GroupId == null)
            return;

        _network.LeaveGroup(msg.GroupId.Value, ownNumber);
        netComp.Groups.Remove(msg.GroupId.Value);
        Dirty(cardUid, netComp);

        // #Misfits Add - Clear selection if we left the currently selected group
        if (ent.Comp.SelectedGroupId == msg.GroupId)
            ent.Comp.SelectedGroupId = null;
    }

    private void HandleGroupToggleMapTracking(EntityUid cardUid, PipBoyNetworkComponent netComp, PipBoyHubUiMessageEvent msg)
    {
        if (msg.GroupId == null || !netComp.Groups.TryGetValue(msg.GroupId.Value, out var membership))
            return;

        netComp.Groups[msg.GroupId.Value] = membership with { MapTrackingEnabled = !membership.MapTrackingEnabled };
        Dirty(cardUid, netComp);
    }

    private void HandleGroupSendMessage(uint ownNumber, EntityUid cardUid, PipBoyHubUiMessageEvent msg, EntityUid loaderUid)
    {
        if (msg.GroupId == null || string.IsNullOrWhiteSpace(msg.Content))
            return;

        // Radio relay: log whether telecomm is available (informational, does not block messaging)
        // In the wasteland, PipBoy-to-PipBoy group chat works peer-to-peer without infrastructure.

        var content = msg.Content.Trim();
        if (content.Length > PipBoyGroupMessage.MaxContentLength)
            content = content[..PipBoyGroupMessage.MaxContentLength];

        var info = _network.GetCardDisplayInfo(cardUid);
        var groupMsg = new PipBoyGroupMessage(_timing.CurTime, content, ownNumber, info.Name);
        _network.AddGroupMessage(msg.GroupId.Value, groupMsg);

        // #Misfits Add - Notify all other group members of the new message
        var group = _network.GetGroup(msg.GroupId.Value);
        if (group != null)
        {
            foreach (var memberNum in group.Members)
            {
                if (memberNum == ownNumber)
                    continue; // don't notify the sender

                var memberCard = _network.FindCardByNumber(memberNum);
                if (memberCard != null)
                {
                    NotifyCardHolder(memberCard.Value,
                        Loc.GetString("pipboy-notif-group-message-title", ("group", group.GroupName)),
                        Loc.GetString("pipboy-notif-group-message-body", ("sender", info.Name), ("message", Truncate(content, NotificationMaxLength))));
                }
            }
        }

        // Trigger UI update for all group members who have the hub open
        UpdateUIForGroupMembers(msg.GroupId.Value);
    }

    private void HandleGroupSelect(Entity<PipBoyHubCartridgeComponent> ent, PipBoyHubUiMessageEvent msg)
    {
        ent.Comp.SelectedGroupId = msg.GroupId;
    }

    private void HandleToggleVisibility(EntityUid cardUid, PipBoyNetworkComponent netComp)
    {
        netComp.IsVisible = !netComp.IsVisible;
        Dirty(cardUid, netComp);
    }

    /// <summary>Send a message to someone from the directory (via NanoChat).</summary>
    private void HandleDirectorySendMessage(EntityUid cardUid, NanoChatCardComponent cardComp, PipBoyHubUiMessageEvent msg)
    {
        // Reuse the contact send message logic
        HandleContactSendMessage(cardUid, cardComp, msg);
    }

    private void HandlePasswordSet(EntityUid cardUid, PipBoyNetworkComponent netComp, PipBoyHubUiMessageEvent msg)
    {
        var password = msg.Content?.Trim();
        if (password != null && password.Length > MaxPasswordLength)
            password = password[..MaxPasswordLength];

        _network.SetPassword((cardUid, netComp), string.IsNullOrEmpty(password) ? null : password);
    }

    private void HandlePasswordUnlock(EntityUid cardUid, PipBoyNetworkComponent netComp, PipBoyHubUiMessageEvent msg)
    {
        if (string.IsNullOrEmpty(msg.Content))
            return;

        _network.TryUnlock((cardUid, netComp), msg.Content);
    }

    private void HandleGroupAddWaypoint(EntityUid cardUid, uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.GroupId == null || string.IsNullOrWhiteSpace(msg.Content))
            return;

        var label = msg.Content.Trim();
        if (label.Length > MaxWaypointLabelLength)
            label = label[..MaxWaypointLabelLength];

        // Use the PDA's world position as the waypoint coordinates
        if (!TryComp<NanoChatCardComponent>(cardUid, out var card) || card.PdaUid == null)
            return;

        var xform = Transform(card.PdaUid.Value);
        var pos = xform.WorldPosition;
        var info = _network.GetCardDisplayInfo(cardUid);
        _network.AddGroupWaypoint(msg.GroupId.Value, label, pos.X, pos.Y, info.Name);
    }

    private void HandleGroupRemoveWaypoint(PipBoyHubUiMessageEvent msg)
    {
        if (msg.GroupId == null || string.IsNullOrEmpty(msg.Content))
            return;

        if (int.TryParse(msg.Content, out var index))
            _network.RemoveGroupWaypoint(msg.GroupId.Value, index);
    }

    private void HandleGroupSetPassword(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.GroupId == null)
            return;

        var password = msg.Content?.Trim();
        if (password != null && password.Length > MaxPasswordLength)
            password = password[..MaxPasswordLength];

        _network.SetGroupPassword(msg.GroupId.Value, ownNumber, string.IsNullOrEmpty(password) ? null : password);
    }

    private void HandleSosBeacon(EntityUid cardUid, uint ownNumber)
    {
        if (!TryComp<NanoChatCardComponent>(cardUid, out var card) || card.PdaUid == null)
            return;

        var info = _network.GetCardDisplayInfo(cardUid);
        var xform = Transform(card.PdaUid.Value);
        var pos = xform.WorldPosition;
        _network.BroadcastSos(ownNumber, info.Name, pos.X, pos.Y);

        // #Misfits Add - Notify all contacts that an SOS was sent
        if (TryComp<PipBoyNetworkComponent>(cardUid, out var net))
        {
            foreach (var (contactNum, contact) in net.Contacts)
            {
                if (contact.Status != PipBoyContactStatus.Accepted)
                    continue;

                var contactCard = _network.FindCardByNumber(contactNum);
                if (contactCard != null)
                {
                    NotifyCardHolder(contactCard.Value,
                        Loc.GetString("pipboy-notif-sos-title"),
                        Loc.GetString("pipboy-notif-sos-body", ("sender", info.Name), ("x", $"{pos.X:F0}"), ("y", $"{pos.Y:F0}")));
                }
            }
        }
    }

    private void HandleSetStatus(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        var status = PipBoyPresenceStatus.Available;
        string? statusMessage = null;

        if (!string.IsNullOrEmpty(msg.Content))
        {
            var parts = msg.Content.Split('|', 2);
            if (parts.Length >= 1 && byte.TryParse(parts[0], out var statusByte) &&
                Enum.IsDefined(typeof(PipBoyPresenceStatus), statusByte))
            {
                status = (PipBoyPresenceStatus) statusByte;
            }

            if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                statusMessage = parts[1].Trim();
                if (statusMessage.Length > MaxStatusLength)
                    statusMessage = statusMessage[..MaxStatusLength];
            }
        }

        _network.SetStatus(ownNumber, status, statusMessage);
    }

    private void HandleDeadDropCreate(EntityUid cardUid, uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Content))
            return;

        if (!TryComp<NanoChatCardComponent>(cardUid, out var card) || card.PdaUid == null)
            return;

        var content = msg.Content.Trim();
        if (content.Length > PipBoyDeadDrop.MaxContentLength)
            content = content[..PipBoyDeadDrop.MaxContentLength];

        var info = _network.GetCardDisplayInfo(cardUid);
        var xform = Transform(card.PdaUid.Value);
        var pos = xform.WorldPosition;
        _network.CreateDeadDrop(ownNumber, info.Name, content, pos.X, pos.Y, msg.TargetNumber);
    }

    private void HandleDeadDropCollect(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (string.IsNullOrEmpty(msg.Content))
            return;

        if (uint.TryParse(msg.Content, out var dropId))
            _network.CollectDeadDrop(dropId, ownNumber);
    }

    private void HandleContactSetNote(uint ownNumber, PipBoyHubUiMessageEvent msg)
    {
        if (msg.TargetNumber == null)
            return;

        var notes = msg.Content?.Trim();
        if (notes != null && notes.Length > MaxNoteLength)
            notes = notes[..MaxNoteLength];

        _network.SetContactNote(ownNumber, msg.TargetNumber.Value,
            string.IsNullOrWhiteSpace(notes) ? null : notes);
    }

    #endregion

    #region UI

    /// <summary>Update UI for all members of a group who have the hub cartridge active.</summary>
    private void UpdateUIForGroupMembers(uint groupId)
    {
        var group = _network.GetGroup(groupId);
        if (group == null)
            return;

        var query = EntityQueryEnumerator<PipBoyHubCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var hub, out var cartridge))
        {
            if (cartridge.LoaderUid == null || hub.Card == null)
                continue;

            if (!TryComp<NanoChatCardComponent>(hub.Card, out var card) || card.Number == null)
                continue;

            if (!group.Members.Contains(card.Number.Value))
                continue;

            UpdateUI((uid, hub), cartridge.LoaderUid.Value);
        }
    }

    private bool GetCard(EntityUid loaderUid,
        out EntityUid cardUid,
        out NanoChatCardComponent cardComp,
        out PipBoyNetworkComponent netComp)
    {
        cardUid = default;
        cardComp = default!;
        netComp = default!;

        if (!TryComp<PdaComponent>(loaderUid, out var pda) ||
            pda.ContainedId == null ||
            !TryComp<NanoChatCardComponent>(pda.ContainedId, out var cc) ||
            !TryComp<PipBoyNetworkComponent>(pda.ContainedId, out var nc))
            return false;

        cardUid = pda.ContainedId.Value;
        cardComp = cc;
        netComp = nc;
        return true;
    }

    private void UpdateUI(Entity<PipBoyHubCartridgeComponent> ent, EntityUid loader)
    {
        uint ownNumber = 0;
        var hasPassword = false;
        var isLocked = false;
        var isVisible = true;
        // #Misfits Tweak - Start with shared empty sentinels; each field is reassigned to a
        // live collection in the unlocked-card path. This avoids ten `new` allocations per
        // UI refresh (see Empty* sentinel block above).
        Dictionary<uint, PipBoyContact> contacts = EmptyContacts;
        List<PipBoyTrackedLocation> contactLocs = EmptyTrackedLocations;
        List<PipBoyGroupInfo> groups = EmptyGroups;
        List<PipBoyGroupMessage> groupMessages = EmptyGroupMessages;
        List<PipBoyTrackedLocation> groupLocs = EmptyTrackedLocations;
        List<PipBoyWaypoint> groupWaypoints = EmptyWaypoints;
        List<PipBoyDirectoryEntry> directory = EmptyDirectory;
        List<PipBoySosAlert> sosAlerts = EmptySosAlerts;
        var presenceStatus = PipBoyPresenceStatus.Available;
        string? statusMessage = null;
        List<PipBoyDeadDrop> nearbyDeadDrops = EmptyDeadDrops;
        var hasRadioConnection = false;

        // #Misfits Fix - Resolve the card directly from the loader's PDA first so the
        // lock/password state is correct even before the periodic card-sync tick has run
        // (e.g. a friend opening the Settings program on a freshly handed-over locked
        // Pip-Boy). Falls back to the cached Card reference kept in sync by Update(), and
        // opportunistically refreshes that cache. Without this, a stale/null cache could
        // push an IsLocked=false state and let the client render the unlocked Settings
        // panel (with the Remove Password button) on a locked Pip-Boy.
        var cardEntity = ent.Comp.Card;
        if (TryComp<PdaComponent>(loader, out var pda) && pda.ContainedId is { } containedId)
            cardEntity = containedId;

        if (cardEntity is { } resolvedCard &&
            TryComp<NanoChatCardComponent>(resolvedCard, out var card) &&
            TryComp<PipBoyNetworkComponent>(resolvedCard, out var net))
        {
            // #Misfits Fix - Opportunistically refresh the cached card reference.
            ent.Comp.Card = resolvedCard;

            ownNumber = card.Number ?? 0;
            hasPassword = net.Password != null;
            isLocked = net.IsLocked;
            isVisible = net.IsVisible;
            contacts = net.Contacts;
            presenceStatus = net.PresenceStatus;
            statusMessage = net.StatusMessage;
            sosAlerts = net.SosAlerts;

            if (!isLocked)
            {
                contactLocs = _network.GetContactLocations((ent.Comp.Card.Value, net));
                groups = _network.GetGroupsForMember(ownNumber, net.Groups);
                directory = _network.GetDirectory(ownNumber);
                hasRadioConnection = _network.HasTelecommAccess(loader);

                // Group messages, tracking, and waypoints for the selected group
                if (ent.Comp.SelectedGroupId != null)
                {
                    var groupData = _network.GetGroup(ent.Comp.SelectedGroupId.Value);
                    if (groupData != null)
                    {
                        groupMessages = groupData.Messages;
                        groupLocs = _network.GetGroupLocations(ent.Comp.SelectedGroupId.Value, ownNumber);
                        groupWaypoints = _network.GetGroupWaypoints(ent.Comp.SelectedGroupId.Value);
                    }
                }

                // Dead drops near the PDA's position
                if (card.PdaUid != null)
                {
                    var xform = Transform(card.PdaUid.Value);
                    var pos = xform.WorldPosition;
                    nearbyDeadDrops = _network.GetNearbyDeadDrops(pos.X, pos.Y, ownNumber);

                    // #Misfits Add - Notify player about newly detected dead drops
                    foreach (var drop in nearbyDeadDrops)
                    {
                        if (ent.Comp.NotifiedDeadDropIds.Add(drop.Id))
                        {
                            NotifyCardHolder(ent.Comp.Card.Value,
                                Loc.GetString("pipboy-notif-dead-drop-title"),
                                Loc.GetString("pipboy-notif-dead-drop-body", ("sender", drop.SenderName)));
                        }
                    }
                }
            }
        }

        var state = new PipBoyHubUiState(
            ownNumber,
            hasPassword,
            isLocked,
            isVisible,
            contacts,
            contactLocs,
            groups,
            groupMessages,
            ent.Comp.SelectedGroupId,
            groupLocs,
            directory,
            groupWaypoints,
            sosAlerts,
            presenceStatus,
            statusMessage,
            nearbyDeadDrops,
            hasRadioConnection);

        _cartridge.UpdateCartridgeUiState(loader, state);
    }

    /// <summary>Get NanoChatRecipient info for use with the NanoChat messaging system.</summary>
    private NanoChatRecipient? GetNanoChatRecipientInfo(uint number)
    {
        var cardUid = _network.FindCardByNumber(number);
        if (cardUid == null)
            return null;

        var info = _network.GetCardDisplayInfo(cardUid.Value);
        return new NanoChatRecipient(number, info.Name, info.JobTitle);
    }

    // #Misfits Fix - Send a PDA notification directly to the player holding the PDA.
    // Bypasses the CartridgeLoader → PdaSystem event chain and sends the chat message
    // + ringer vibration directly, ensuring notifications are never silently dropped.
    private void NotifyCardHolder(EntityUid cardUid, string header, string body)
    {
        if (!TryComp<NanoChatCardComponent>(cardUid, out var card) || card.PdaUid is not {} pdaUid)
            return;

        // Play the ringer vibration popup ("Your Pip-Boy vibrates")
        _ringer.RingerPlayRingtone(pdaUid);

        // Find the player holding this PDA and send them the chat notification directly
        if (!_container.TryGetContainingContainer((pdaUid, null, null), out var container)
            || !TryComp<ActorComponent>(container.Owner, out var actor))
            return;

        var escapedBody = FormattedMessage.EscapeText(body);
        var wrappedMessage = Loc.GetString("pda-notification-message",
            ("header", header),
            ("message", escapedBody));

        _chatManager.ChatMessageToOne(
            ChatChannel.Notifications,
            escapedBody,
            wrappedMessage,
            EntityUid.Invalid,
            false,
            actor.PlayerSession.Channel);
    }

    // #Misfits Add - Truncate a string for notification display.
    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength] + " [...]";
    }

    #endregion
}
