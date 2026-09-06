// #Cythisiax Add - Wendover Free Market Exchange server system
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Stack;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared._Misfits.Trade.Market;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Trade.Market;

public sealed class MarketSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActorSystem _actor = default!;

    private ISawmill _log = default!;
    private readonly Dictionary<string, MarketOrder> _activeOrders = new();
    private readonly Dictionary<Guid, EntityUid> _playerStorage = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _openMarketUis = new();
    private EntityUid? _escrowHost;
    private const string ListingSlotPrefix = "market_slot_";
    private const string ProceedsContainerPrefix = "market_proceeds_";
    private float _purgeTimer;
    private readonly List<MarketFeedEntry> _activityFeed = new();
    private const int MaxFeedEntries = 50;
    private readonly Dictionary<Guid, string> _selectedProtoByUser = new();
    // #Cythisiax Add - Search results are tracked per player so one buyer's
    // search does not overwrite another player's UI state.
    private readonly Dictionary<Guid, (string Query, List<(string Id, string Name)> Results)> _searchResultsByUser = new();

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("market");
        SubscribeLocalEvent<MarketTerminalComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<MarketTerminalComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MarketTerminalComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<GetVerbsEvent<UtilityVerb>>(OnItemVerb);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        // Refresh market UIs when deposit storage contents change (grid drag-drop)
        SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnDepositContainerChanged);
        SubscribeLocalEvent<EntRemovedFromContainerMessage>(OnDepositContainerChanged);
        // #Cythisiax Fixed - Removed SubscribeLocalEvent<StackComponent, StackCountChangedEvent>:
        // it duplicated the pre-existing _NC StoreStructuredSystem directed subscription, and the
        // event bus allows only one directed subscription per component+event pair (server crashed
        // with "Duplicate Subscriptions" on startup). Stack split/merge inside market storage is
        // still covered by the container insert/remove subscriptions above.
        Subs.BuiEvents<MarketTerminalComponent>(MarketUiKey.Key, subs =>
        {
            subs.Event<CreateOrderMessage>(OnCreateOrder);
            subs.Event<PurchaseListingMessage>(OnPurchaseListing);
            subs.Event<CancelOrderMessage>(OnCancelOrder);
            subs.Event<ClaimEscrowMessage>(OnClaimEscrow);
            subs.Event<MarketWithdrawItemMessage>(OnWithdrawItem);
            subs.Event<ProtoSearchMessage>(OnProtoSearch);
            subs.Event<SelectOrderBookMessage>(OnSelectOrderBook);
        });
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnGetVerbs(Entity<MarketTerminalComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess) return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("market-verb-open"), Priority = 10, Act = () => OpenMarketForPlayer(user, ent) });
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("market-verb-storage"), Priority = 9, Act = () => OpenDepositStorage(user, ent) });
    }

    private void OnItemVerb(GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Target == args.User) return;
        var user = args.User;
        var query = EntityQueryEnumerator<MarketTerminalComponent, TransformComponent>();
        while (query.MoveNext(out var terminalUid, out _, out var terminalXform))
        {
            if (!terminalXform.Coordinates.InRange(EntityManager, Transform(user).Coordinates, 2f)) continue;
            args.Verbs.Add(new UtilityVerb { Text = Loc.GetString("market-verb-deposit"), Act = () => DepositItemIntoMarket(args.Target, terminalUid, user) });
            break;
        }
    }

    private void OnActivate(Entity<MarketTerminalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex) return;
        OpenMarketForPlayer(args.User, ent);
        args.Handled = true;
    }

    private void OpenMarketForPlayer(EntityUid user, Entity<MarketTerminalComponent> terminal)
    {
        if (!TryComp<ActorComponent>(user, out _)) return;
        if (!_ui.IsUiOpen(terminal.Owner, MarketUiKey.Key, user))
            _ui.OpenUi(terminal.Owner, MarketUiKey.Key, user);
        if (!_openMarketUis.TryGetValue(terminal.Owner, out var users))
        {
            users = new HashSet<EntityUid>();
            _openMarketUis[terminal.Owner] = users;
        }
        users.Add(user);
        RefreshMarketState(terminal);
    }

    private void OnUiClosed(Entity<MarketTerminalComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_openMarketUis.TryGetValue(ent.Owner, out var users))
            users.Remove(args.Actor);
    }

    /// <summary>
    /// When a deposit storage container changes (grid drag-drop), refresh that player's market UI.
    /// </summary>
    private void OnDepositContainerChanged(EntInsertedIntoContainerMessage ev)
    {
        var containerEntity = ev.Container.Owner;
        foreach (var storage in _playerStorage.Values)
        {
            if (storage != containerEntity) continue;
            RefreshAllMarketStates();
            return;
        }
    }

    private void OnDepositContainerChanged(EntRemovedFromContainerMessage ev)
    {
        var containerEntity = ev.Container.Owner;
        foreach (var storage in _playerStorage.Values)
        {
            if (storage != containerEntity) continue;
            RefreshAllMarketStates();
            return;
        }
    }

    // #Cythisiax Fixed - Removed the OnStackCountChanged handler: its
    // StackComponent/StackCountChangedEvent subscription duplicated the pre-existing
    // _NC StoreStructuredSystem subscription (one directed subscription per
    // component+event pair). Market UI refresh on stack changes is handled by
    // OnDepositContainerChanged (container insert/remove) above.

    // ── Deposit Storage ───────────────────────────────────────────────────────

    private EntityUid GetOrCreateDepositStorage(EntityUid terminal, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor)) return EntityUid.Invalid;
        var userId = actor.PlayerSession.UserId.UserId;
        if (_playerStorage.TryGetValue(userId, out var existing) && Exists(existing)) return existing;
        var storage = Spawn("MarketDepositStorage", Transform(terminal).Coordinates);
        _playerStorage[userId] = storage;
        return storage;
    }

    private void OpenDepositStorage(EntityUid user, Entity<MarketTerminalComponent> terminal)
    {
        if (!TryComp<ActorComponent>(user, out var actor)) return;
        var storage = GetOrCreateDepositStorage(terminal.Owner, user);
        if (storage == EntityUid.Invalid) return;
        _ui.OpenUi(storage, StorageComponent.StorageUiKey.Key, actor.PlayerSession);
    }

    private void DepositItemIntoMarket(EntityUid item, EntityUid terminal, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out _)) return;
        var storage = GetOrCreateDepositStorage(terminal, user);
        if (storage == EntityUid.Invalid || !TryComp<StorageComponent>(storage, out var sc)) return;
        if (!_container.Insert(item, sc.Container)) return;
        RefreshAllMarketStates();
    }

    private void OnWithdrawItem(Entity<MarketTerminalComponent> terminal, ref MarketWithdrawItemMessage msg)
    {
        if (!TryComp<ActorComponent>(msg.Actor, out var actor))
            return;

        var user = msg.Actor;
        if (!_playerStorage.TryGetValue(actor.PlayerSession.UserId.UserId, out var storage) || !Exists(storage)
            || !TryComp<StorageComponent>(storage, out var sc)) return;
        EntityUid? toRemove = null;
        foreach (var c in sc.Container.ContainedEntities)
        {
            if (c.ToString() != msg.SlotKey)
                continue;

            toRemove = c;
            break;
        }

        if (toRemove == null)
            return;

        _container.Remove(toRemove.Value, sc.Container);
        if (!_hands.TryPickupAnyHand(user, toRemove.Value))
            _xform.DropNextTo(toRemove.Value, user);
        RefreshAllMarketStates();
    }

    private void OnProtoSearch(Entity<MarketTerminalComponent> terminal, ref ProtoSearchMessage msg)
    {
        if (!TryComp<ActorComponent>(msg.Actor, out var actor))
            return;

        if (string.IsNullOrWhiteSpace(msg.Query)) return;

        var query = msg.Query.ToLowerInvariant();
        var matches = new List<(string Id, string Name)>();

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            var id = proto.ID.ToLowerInvariant();
            if (!id.StartsWith("n14") && !id.StartsWith("misfits"))
                continue;

            var hasItem = proto.Components.ContainsKey("Item");
            var hasClothing = proto.Components.ContainsKey("Clothing");
            if (!hasItem && !hasClothing)
                continue;

            var rawName = proto.Name ?? string.Empty;
            var displayName = rawName;
            if (!string.IsNullOrWhiteSpace(rawName) && Loc.TryGetString(rawName, out var localized))
                displayName = localized;

            if (!id.Contains(query) && !rawName.ToLowerInvariant().Contains(query) && !displayName.ToLowerInvariant().Contains(query))
                continue;

            matches.Add((proto.ID, displayName));
            if (matches.Count >= 20)
                break;
        }

        // #Cythisiax Add - Store search results per player instead of globally.
        _searchResultsByUser[actor.PlayerSession.UserId.UserId] = (msg.Query, matches);
        _ui.ServerSendUiMessage(terminal.Owner, MarketUiKey.Key, new ProtoSearchResults(matches), msg.Actor);
        RefreshMarketState(terminal);
    }

    private void OnSelectOrderBook(Entity<MarketTerminalComponent> terminal, ref SelectOrderBookMessage msg)
    {
        if (!TryComp<ActorComponent>(msg.Actor, out var actor))
            return;

        var uid = actor.PlayerSession.UserId.UserId;
        if (string.IsNullOrWhiteSpace(msg.PrototypeId))
            return;

        _selectedProtoByUser[uid] = msg.PrototypeId;
        RefreshMarketState(terminal);
    }

    // ── Listings and direct purchases ─────────────────────────────────────────

    private void OnCreateOrder(Entity<MarketTerminalComponent> terminal, ref CreateOrderMessage msg)
    {
        if (!TryComp<ActorComponent>(msg.Actor, out var actor))
            return;

        var session = actor.PlayerSession;
        var user = session.AttachedEntity;
        if (user == null || user == EntityUid.Invalid) return;
        var userId = session.UserId;
        var charName = MetaData(user.Value).EntityName;
        var orderId = Guid.NewGuid().ToString();

        if (msg.IsBuyOrder)
        {
            SendResult(terminal, msg.Actor, "Buy orders are no longer used. Purchase an active listing from the Market Exchange tab.", false);
            return;
        }

        if (msg.Quantity <= 0 || msg.Price <= 0 || msg.Price > int.MaxValue / msg.Quantity
            || msg.Currency is not ("Bottlecaps" or "Barter"))
        {
            SendResult(terminal, msg.Actor, "Choose a valid quantity, price, and payment type.", false);
            return;
        }

        if (_activeOrders.Values.Count(o => o.OwnerId == userId.UserId && o.Status == "Active") >= 3)
        {
            SendResult(terminal, msg.Actor, "You already have the maximum of 3 active listings.", false);
            return;
        }

        if (msg.Currency == "Barter" && (string.IsNullOrWhiteSpace(msg.RequestedItemId)
            || !_proto.TryIndex<EntityPrototype>(msg.RequestedItemId, out _)))
        {
            SendResult(terminal, msg.Actor, "Select a valid barter item before listing.", false);
            return;
        }

        if (!_playerStorage.TryGetValue(userId.UserId, out var storageUid) || !Exists(storageUid)
            || !TryComp<StorageComponent>(storageUid, out var storage))
        {
            SendResult(terminal, msg.Actor, "Deposit the item into market storage first.", false);
            return;
        }

        var prototypeId = msg.PrototypeId;
        var item = storage.Container.ContainedEntities.FirstOrDefault(c =>
            MetaData(c).EntityPrototype?.ID == prototypeId);
        if (item == EntityUid.Invalid)
        {
            SendResult(terminal, msg.Actor, "That item is no longer in market storage.", false);
            return;
        }

        EntityUid escrowItem;
        if (TryComp(item, out StackComponent? stack) && stack != null)
        {
            if (stack.Count < msg.Quantity)
            {
                SendResult(terminal, msg.Actor, "The stored stack does not contain that quantity.", false);
                return;
            }

            if (stack.Count > msg.Quantity)
            {
                var split = _stack.Split(item, msg.Quantity, Transform(storageUid).Coordinates, stack);
                if (split == null)
                {
                    SendResult(terminal, msg.Actor, "The item stack could not be split.", false);
                    return;
                }

                escrowItem = split.Value;
            }
            else
            {
                _container.Remove(item, storage.Container);
                escrowItem = item;
            }
        }
        else
        {
            if (msg.Quantity != 1)
            {
                SendResult(terminal, msg.Actor, "Non-stackable items must be listed one at a time.", false);
                return;
            }

            _container.Remove(item, storage.Container);
            escrowItem = item;
        }

        var escrowHost = GetOrCreateEscrowHost(terminal.Owner);
        var escrowContainer = _container.EnsureContainer<ContainerSlot>(escrowHost, $"{ListingSlotPrefix}{orderId}");
        if (!_container.Insert(escrowItem, escrowContainer))
        {
            _container.Insert(escrowItem, storage.Container);
            SendResult(terminal, msg.Actor, "The market could not escrow that item.", false);
            return;
        }

        var protoName = _proto.TryIndex<EntityPrototype>(msg.PrototypeId, out var p) ? p.Name : msg.PrototypeId;
        var requestedName = msg.RequestedItemId != null && _proto.TryIndex<EntityPrototype>(msg.RequestedItemId, out var requested)
            ? requested.Name
            : msg.RequestedItemId;
        var order = new MarketOrder
        {
            OrderId = orderId, PrototypeId = msg.PrototypeId, PrototypeName = protoName,
            Quantity = msg.Quantity, Price = msg.Price, Currency = msg.Currency,
            IsBuyOrder = msg.IsBuyOrder, OwnerName = charName, OwnerId = userId.UserId,
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(3),
            RequestedItemId = msg.Currency == "Barter" ? msg.RequestedItemId : null,
            RequestedItemName = msg.Currency == "Barter" ? requestedName : null,
        };

        _activeOrders[orderId] = order;
        _selectedProtoByUser[userId.UserId] = msg.PrototypeId;
        PushFeed($"{charName} listed {order.Quantity}x {order.PrototypeName}");
        SendResult(terminal, msg.Actor, "Listing placed on the main market.", true);
        RefreshAllMarketStates();
    }

    private void OnPurchaseListing(Entity<MarketTerminalComponent> terminal, ref PurchaseListingMessage msg)
    {
        if (!TryComp<ActorComponent>(msg.Actor, out var actor) || actor.PlayerSession.AttachedEntity is not { } buyer)
            return;
        if (!_activeOrders.TryGetValue(msg.OrderId, out var order) || order.Status != "Active")
        {
            SendResult(terminal, msg.Actor, "That listing is no longer available.", false);
            return;
        }
        var buyerId = actor.PlayerSession.UserId.UserId;
        var remaining = order.Quantity - order.FulfilledQty;
        if (order.OwnerId == buyerId || msg.Quantity <= 0 || msg.Quantity > remaining)
        {
            SendResult(terminal, msg.Actor, order.OwnerId == buyerId
                ? "You cannot purchase your own listing."
                : $"Choose a quantity between 1 and {remaining}.", false);
            return;
        }

        if (_escrowHost is not { } escrowHost || !Exists(escrowHost)
            || !_container.TryGetContainer(escrowHost, $"{ListingSlotPrefix}{order.OrderId}", out var listingContainer)
            || listingContainer is not ContainerSlot slot || slot.ContainedEntity is not { } listedItem)
        {
            SendResult(terminal, msg.Actor, "The listing escrow is unavailable; no payment was taken.", false);
            return;
        }

        var total = checked(order.Price * msg.Quantity);
        if (order.Currency == "Bottlecaps")
        {
            if (!TryDeductCurrency(buyer, order.Currency, total))
            {
                SendResult(terminal, msg.Actor, "You do not have enough caps for that purchase.", false);
                return;
            }
        }
        else if (!TryTakeBarterPayment(buyerId, order, msg.Quantity, escrowHost))
        {
            SendResult(terminal, msg.Actor,
                $"Deposit {total}x {order.RequestedItemName ?? order.RequestedItemId} in market storage first.", false);
            return;
        }

        EntityUid purchased;
        if (TryComp(listedItem, out StackComponent? stack) && stack.Count > msg.Quantity)
        {
            var split = _stack.Split(listedItem, msg.Quantity, Transform(escrowHost).Coordinates, stack);
            if (split == null)
            {
                if (order.Currency == "Bottlecaps") RefundCurrency(buyer, order.Currency, total);
                SendResult(terminal, msg.Actor, "The listed stack could not be split; payment was refunded.", false);
                return;
            }
            purchased = split.Value;
        }
        else
        {
            _container.Remove(listedItem, slot);
            purchased = listedItem;
        }

        if (!_hands.TryPickupAnyHand(buyer, purchased))
            _xform.DropNextTo(purchased, buyer);

        order.FulfilledQty += msg.Quantity;
        if (order.FulfilledQty >= order.Quantity)
            order.Status = "Fulfilled";

        if (order.Currency == "Bottlecaps")
            CreditSeller(order.OwnerId, order.OwnerName, order.Currency, total - total / 10);

        PushFeed($"{MetaData(buyer).EntityName} bought {msg.Quantity}x {order.PrototypeName}");
        SendResult(terminal, msg.Actor, $"Purchased {msg.Quantity}x {order.PrototypeName}.", true);
        RefreshAllMarketStates();
    }

    private void OnCancelOrder(Entity<MarketTerminalComponent> terminal, ref CancelOrderMessage msg)
    {
        if (!TryComp<ActorComponent>(msg.Actor, out var actor))
            return;

        var user = actor.PlayerSession.AttachedEntity;
        if (user == null || user == EntityUid.Invalid) return;
        if (!_activeOrders.TryGetValue(msg.OrderId, out var order) || order.Status != "Active") return;
        if (order.OwnerId != actor.PlayerSession.UserId.UserId) return;
        order.Status = "Cancelled";
        if (_escrowHost is { } escrowHost && Exists(escrowHost))
        {
            var sn = $"{ListingSlotPrefix}{msg.OrderId}";
            if (_container.TryGetContainer(escrowHost, sn, out var c) && c is ContainerSlot slot && slot.ContainedEntity is { } item)
            { _container.Remove(item, slot); if (!_hands.TryPickupAnyHand(user.Value, item)) _xform.DropNextTo(item, user.Value); }
        }
        if (order.Currency == "Barter" && order.FulfilledQty > 0)
            MoveBarterProceedsToPlayer(order, terminal.Owner, user.Value);
        SendResult(terminal, msg.Actor, "Listing cancelled; the unsold item was returned.", true);
        RefreshAllMarketStates();
    }

    private void OnClaimEscrow(Entity<MarketTerminalComponent> terminal, ref ClaimEscrowMessage msg)
    {
        if (!TryComp<ActorComponent>(msg.Actor, out var actor))
            return;

        var user = actor.PlayerSession.AttachedEntity;
        if (user == null || user == EntityUid.Invalid) return;
        if (!_activeOrders.TryGetValue(msg.OrderId, out var order)) return;
        if (order.OwnerId != actor.PlayerSession.UserId.UserId || order.Status is not ("Active" or "Fulfilled")
            || order.Currency != "Barter" || !MoveBarterProceedsToPlayer(order, terminal.Owner, user.Value)) return;
        if (order.Status == "Fulfilled")
            order.Status = "Claimed";
        SendResult(terminal, msg.Actor, "Barter payment moved to your market storage.", true);
        RefreshAllMarketStates();
    }

    private bool MoveBarterProceedsToPlayer(MarketOrder order, EntityUid terminal, EntityUid user)
    {
        if (_escrowHost is not { } escrowHost || !Exists(escrowHost)
            || !_container.TryGetContainer(escrowHost, $"{ProceedsContainerPrefix}{order.OrderId}", out var proceeds)
            || proceeds.ContainedEntities.Count == 0)
            return false;

        var storageUid = GetOrCreateDepositStorage(terminal, user);
        if (!TryComp<StorageComponent>(storageUid, out var storage))
            return false;
        foreach (var item in proceeds.ContainedEntities.ToList())
        {
            _container.Remove(item, proceeds);
            if (!_container.Insert(item, storage.Container) && !_hands.TryPickupAnyHand(user, item))
                _xform.DropNextTo(item, user);
        }
        return true;
    }

    private EntityUid GetOrCreateEscrowHost(EntityUid terminal)
    {
        if (_escrowHost is { } existing && Exists(existing))
            return existing;
        _escrowHost = Spawn("MarketDepositStorage", Transform(terminal).Coordinates);
        return _escrowHost.Value;
    }

    private bool TryTakeBarterPayment(Guid buyerId, MarketOrder order, int purchaseQuantity, EntityUid escrowHost)
    {
        if (order.RequestedItemId == null || !_playerStorage.TryGetValue(buyerId, out var storageUid)
            || !Exists(storageUid) || !TryComp<StorageComponent>(storageUid, out var storage))
            return false;

        var required = checked(order.Price * purchaseQuantity);
        var matches = storage.Container.ContainedEntities
            .Where(e => MetaData(e).EntityPrototype?.ID == order.RequestedItemId).ToList();
        var available = matches.Sum(e => TryComp<StackComponent>(e, out var stack) ? stack.Count : 1);
        if (available < required)
            return false;

        var proceeds = _container.EnsureContainer<Container>(escrowHost, $"{ProceedsContainerPrefix}{order.OrderId}");
        var remaining = required;
        foreach (var paymentItem in matches)
        {
            if (remaining <= 0)
                break;
            var count = TryComp<StackComponent>(paymentItem, out var stack) ? stack.Count : 1;
            var take = Math.Min(count, remaining);
            EntityUid moved;
            if (stack != null && count > take)
            {
                var split = _stack.Split(paymentItem, take, Transform(storageUid).Coordinates, stack);
                if (split == null)
                    return false;
                moved = split.Value;
            }
            else
            {
                _container.Remove(paymentItem, storage.Container);
                moved = paymentItem;
            }

            if (!_container.Insert(moved, proceeds))
            {
                _container.Insert(moved, storage.Container);
                return false;
            }
            remaining -= take;
        }
        return remaining == 0;
    }

    // ── Currency Helpers ──────────────────────────────────────────────────────

    private bool TryDeductCurrency(EntityUid user, string currency, int amount)
    {
        if (amount <= 0) return true;
        var ct = currency switch { "Bottlecaps" => CurrencyType.Bottlecaps, "NCRDollars" => CurrencyType.NCRDollars, _ => (CurrencyType?)null };
        if (ct == null || !TryComp<PersistentCurrencyComponent>(user, out var w)) return false;
        var bal = ct switch { CurrencyType.Bottlecaps => w.Bottlecaps, CurrencyType.NCRDollars => w.NcrDollars, _ => 0 };
        if (bal < amount) return false;
        switch (ct) { case CurrencyType.Bottlecaps: w.Bottlecaps -= amount; break; case CurrencyType.NCRDollars: w.NcrDollars -= amount; break; }
        Dirty(user, w);
        if (w.UserId != null && w.CharacterName != null && Guid.TryParse(w.UserId, out var pid))
            _ = _db.UpsertCharacterCurrencyAsync(pid, w.CharacterName, w.Bottlecaps, w.NcrDollars, w.Silver, w.Gold);
        return true;
    }

    private void RefundCurrency(EntityUid user, string currency, int amount)
    {
        if (amount <= 0 || !TryComp<PersistentCurrencyComponent>(user, out var w)) return;
        switch (currency) { case "Bottlecaps": w.Bottlecaps += amount; break; case "NCRDollars": w.NcrDollars += amount; break; }
        Dirty(user, w);
        if (w.UserId != null && w.CharacterName != null && Guid.TryParse(w.UserId, out var pid))
            _ = _db.UpsertCharacterCurrencyAsync(pid, w.CharacterName, w.Bottlecaps, w.NcrDollars, w.Silver, w.Gold);
    }

    private void CreditSeller(Guid sellerId, string name, string currency, int amount)
    {
        if (amount <= 0) return;
        var actors = EntityQueryEnumerator<ActorComponent>();
        while (actors.MoveNext(out var uid, out var actor))
        {
            if (actor.PlayerSession.UserId.UserId != sellerId || !TryComp<PersistentCurrencyComponent>(uid, out var wallet))
                continue;

            if (currency == "Bottlecaps") wallet.Bottlecaps += amount;
            else if (currency == "NCRDollars") wallet.NcrDollars += amount;
            Dirty(uid, wallet);
            _ = PersistCurrencyAsync(sellerId, name, wallet.Bottlecaps, wallet.NcrDollars, wallet.Silver, wallet.Gold);
            return;
        }

        _ = CreditOfflineSellerAsync(sellerId, name, currency, amount);
    }

    private async Task PersistCurrencyAsync(Guid sellerId, string name, int caps, int ncr, int silver, int gold)
    {
        try
        {
            await _db.UpsertCharacterCurrencyAsync(sellerId, name, caps, ncr, silver, gold);
        }
        catch (Exception ex) { _log.Error($"PersistCurrencyAsync failed: {ex}"); }
    }

    private async Task CreditOfflineSellerAsync(Guid sellerId, string name, string currency, int amount)
    {
        if (amount <= 0) return;
        try
        {
            var row = await _db.GetCharacterCurrencyAsync(sellerId, name);
            var caps = (row?.Bottlecaps ?? 0) + (currency == "Bottlecaps" ? amount : 0);
            var ncr = (row?.NcrDollars ?? 0) + (currency == "NCRDollars" ? amount : 0);
            var sil = row?.Silver ?? 0;
            var gld = row?.Gold ?? 0;
            await _db.UpsertCharacterCurrencyAsync(sellerId, name, caps, ncr, sil, gld);
        }
        catch (Exception ex) { _log.Error($"CreditOfflineSellerAsync failed: {ex}"); }
    }

    // ── Feed & State ──────────────────────────────────────────────────────────

    private void PushFeed(string text)
    {
        _activityFeed.Insert(0, new MarketFeedEntry { Text = text, Time = DateTime.UtcNow });
        if (_activityFeed.Count > MaxFeedEntries) _activityFeed.RemoveAt(_activityFeed.Count - 1);
    }

    private void SendResult(Entity<MarketTerminalComponent> terminal, EntityUid actor, string message, bool success)
    {
        _ui.ServerSendUiMessage(terminal.Owner, MarketUiKey.Key, new MarketActionResult(message, success), actor);
    }

    private void RefreshAllMarketStates()
    {
        foreach (var terminal in _openMarketUis.Keys.ToList())
        {
            if (!TryComp<MarketTerminalComponent>(terminal, out var component))
                continue;
            RefreshMarketState((terminal, component));
        }
    }

    private void RefreshMarketState(Entity<MarketTerminalComponent> terminal)
    {
        if (!_openMarketUis.TryGetValue(terminal.Owner, out var openUsers))
            return;
        foreach (var user in openUsers.ToList())
        {
            if (!_ui.IsUiOpen(terminal.Owner, MarketUiKey.Key, user)) continue;
            _ui.SetUiState(terminal.Owner, MarketUiKey.Key, BuildState(terminal, user));
        }
    }

    private MarketStateMessage BuildState(Entity<MarketTerminalComponent> terminal, EntityUid user)
    {
        var state = new MarketStateMessage { Feed = new List<MarketFeedEntry>(_activityFeed) };
        if (TryComp<PersistentCurrencyComponent>(user, out var w))
        { state.Bottlecaps = w.Bottlecaps; state.NcrDollars = w.NcrDollars; }

        var activeOrders = _activeOrders.Values.Where(o => o.Status == "Active").ToList();
        state.ItemSummaries = BuildItemSummaries(activeOrders);

        if (TryComp<ActorComponent>(user, out var actor))
        {
            var uid = actor.PlayerSession.UserId.UserId;
            state.MyOrders = _activeOrders.Values.Where(o => o.OwnerId == uid && o.Status == "Active").ToList();
            state.MyCompletedOrders = _activeOrders.Values.Where(o => o.OwnerId == uid && o.Status == "Fulfilled").ToList();
            if (_searchResultsByUser.TryGetValue(uid, out var search))
            {
                state.LastSearchQuery = search.Query;
                state.SearchResults = new List<(string, string)>(search.Results);
            }

            var selectedProtoId = GetSelectedPrototypeId(uid, activeOrders);
            if (!string.IsNullOrWhiteSpace(selectedProtoId))
            {
                state.SelectedProtoId = selectedProtoId;
                state.SelectedProtoName = GetPrototypeName(selectedProtoId, state.ItemSummaries, activeOrders);
                state.SelectedOrderBook = BuildOrderBook(selectedProtoId, state.SelectedProtoName, activeOrders);
            }

            if (_playerStorage.TryGetValue(uid, out var storage) && Exists(storage)
                && TryComp<StorageComponent>(storage, out var sc) && sc.Container != null)
            {
                foreach (var item in sc.Container.ContainedEntities)
                {
                    var meta = MetaData(item);
                    state.DepositedItems.Add(new MarketDepositEntry
                    {
                        SlotKey = item.ToString(),
                        // #Cythisiax Add - Slot key is the exact entity id so withdraw
                        // can target the right entry inside multi-item market storage.
                        ProtoId = meta.EntityPrototype?.ID ?? "",
                        ProtoName = meta.EntityPrototype?.Name ?? meta.EntityName,
                        StackCount = TryComp<StackComponent>(item, out var stack) ? stack.Count : 0,
                        Quantity = TryComp<StackComponent>(item, out var quantityStack) ? quantityStack.Count : 1,
                    });
                }
            }
        }
        state.MarketName = "Wendover Free Market Exchange";
        return state;
    }

    private string GetSelectedPrototypeId(Guid userId, List<MarketOrder> activeOrders)
    {
        if (_selectedProtoByUser.TryGetValue(userId, out var selected) && !string.IsNullOrWhiteSpace(selected))
            return selected;

        var first = activeOrders.FirstOrDefault();
        if (first == null)
            return string.Empty;

        _selectedProtoByUser[userId] = first.PrototypeId;
        return first.PrototypeId;
    }

    private static string GetPrototypeName(string prototypeId, List<MarketItemSummary> summaries, List<MarketOrder> activeOrders)
    {
        var summary = summaries.FirstOrDefault(s => s.PrototypeId == prototypeId);
        if (!string.IsNullOrWhiteSpace(summary?.PrototypeName))
            return summary.PrototypeName;

        var order = activeOrders.FirstOrDefault(o => o.PrototypeId == prototypeId);
        return !string.IsNullOrWhiteSpace(order?.PrototypeName) ? order.PrototypeName : prototypeId;
    }

    private List<MarketItemSummary> BuildItemSummaries(List<MarketOrder> activeOrders)
    {
        var summaries = new List<MarketItemSummary>();
        foreach (var group in activeOrders.GroupBy(o => o.PrototypeId))
        {
            var orders = group.ToList();
            var prototypeName = orders.FirstOrDefault()?.PrototypeName ?? group.Key;
            var sellOrders = orders.Where(o => !o.IsBuyOrder).ToList();
            var buyOrders = orders.Where(o => o.IsBuyOrder).ToList();
            var bestAsk = sellOrders.Count > 0 ? sellOrders.Min(o => o.Price) : 0;
            var bestBid = buyOrders.Count > 0 ? buyOrders.Max(o => o.Price) : 0;
            var currencies = orders.Select(o => o.Currency).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

            summaries.Add(new MarketItemSummary
            {
                PrototypeId = group.Key,
                PrototypeName = prototypeName,
                OrderCount = orders.Count,
                BestAsk = bestAsk,
                BestBid = bestBid,
                Spread = bestAsk > 0 && bestBid > 0 ? bestAsk - bestBid : 0,
                Currency = currencies.Count == 1 ? currencies[0] : string.Join("/", currencies),
            });
        }

        return summaries
            .OrderBy(s => s.PrototypeName)
            .ThenBy(s => s.PrototypeId)
            .ToList();
    }

    private static OrderBookEntry? BuildOrderBook(string prototypeId, string prototypeName, List<MarketOrder> activeOrders)
    {
        var orders = activeOrders.Where(o => o.PrototypeId == prototypeId).ToList();
        if (orders.Count == 0)
            return null;

        return new OrderBookEntry
        {
            PrototypeId = prototypeId,
            PrototypeName = prototypeName,
            SellOrders = orders.Where(o => !o.IsBuyOrder).OrderBy(o => o.Price).ThenBy(o => o.CreatedAt).ToList(),
            BuyOrders = orders.Where(o => o.IsBuyOrder).OrderByDescending(o => o.Price).ThenBy(o => o.CreatedAt).ToList(),
            Volume24h = orders.Sum(o => o.FulfilledQty),
        };
    }

    // ── Round lifecycle ───────────────────────────────────────────────────────

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _activeOrders.Clear(); _openMarketUis.Clear(); _activityFeed.Clear();
        foreach (var storage in _playerStorage.Values)
            if (Exists(storage)) QueueDel(storage);
        _playerStorage.Clear();
        if (_escrowHost is { } escrow && Exists(escrow)) QueueDel(escrow);
        _escrowHost = null;
        _searchResultsByUser.Clear();
        _selectedProtoByUser.Clear();
    }
}
