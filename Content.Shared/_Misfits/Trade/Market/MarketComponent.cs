// #Cythisiax Add - Wendover Free Market Exchange (order-book system)
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Trade.Market;

// ── Component ─────────────────────────────────────────────────────────────────

[RegisterComponent, NetworkedComponent]
public sealed partial class MarketTerminalComponent : Component
{
}

// ── UI Key ────────────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public enum MarketUiKey : byte { Key }

// ── Core Data Types ───────────────────────────────────────────────────────────

/// <summary>An individual order on the market book.</summary>
[Serializable, NetSerializable]
public sealed class MarketOrder
{
    public string OrderId = string.Empty;
    public string PrototypeId = string.Empty;
    public string PrototypeName = string.Empty;
    public int Quantity;
    public int Price;
    public string Currency = string.Empty;
    public bool IsBuyOrder;
    public string OwnerName = string.Empty;
    public Guid OwnerId;
    public DateTime CreatedAt;
    public DateTime ExpiresAt;
    public string Status = "Active"; // Active, Fulfilled, Cancelled, Expired
    public int FulfilledQty;
    public string? RequestedItemId;
    public string? RequestedItemName;
}

/// <summary>Grouped order-book entry for a single item prototype (bid/ask depth).</summary>
[Serializable, NetSerializable]
public sealed class OrderBookEntry
{
    public string PrototypeId = string.Empty;
    public string PrototypeName = string.Empty;
    public List<MarketOrder> SellOrders = new();  // asks — ascending price
    public List<MarketOrder> BuyOrders = new();    // bids — descending price
    public int BestAsk => SellOrders.Count > 0 ? SellOrders[0].Price : 0;
    public int BestBid => BuyOrders.Count > 0 ? BuyOrders[0].Price : 0;
    public int Spread => BestAsk > 0 && BestBid > 0 ? BestAsk - BestBid : 0;
    public int Volume24h;
}

/// <summary>Per-item summary row for the item directory.</summary>
[Serializable, NetSerializable]
public sealed class MarketItemSummary
{
    public string PrototypeId = string.Empty;
    public string PrototypeName = string.Empty;
    public int OrderCount;
    public int BestAsk;
    public int BestBid;
    public int Spread;
    public string Currency = string.Empty;
}

/// <summary>Activity feed entry.</summary>
[Serializable, NetSerializable]
public sealed class MarketFeedEntry
{
    public string Text = string.Empty;
    public DateTime Time;
}

/// <summary>Deposit storage item.</summary>
[Serializable, NetSerializable]
public sealed class MarketDepositEntry
{
    public string SlotKey = string.Empty;
    public string ProtoId = string.Empty;
    public string ProtoName = string.Empty;
    public int StackCount;
    public int Quantity = 1;
}

// ── Client → Server Messages ──────────────────────────────────────────────────

/// <summary>Create a new buy or sell order.</summary>
[Serializable, NetSerializable]
public sealed class CreateOrderMessage(
    string prototypeId, int quantity, string currency, int price,
    bool isBuyOrder, string? requestedItemId = null, int requestedQuantity = 0)
    : BoundUserInterfaceMessage
{
    public string PrototypeId = prototypeId;
    public int Quantity = quantity;
    public string Currency = currency;
    public int Price = price;
    public bool IsBuyOrder = isBuyOrder;
    public string? RequestedItemId = requestedItemId;
    public int RequestedQuantity = requestedQuantity;
}

/// <summary>Cancel an active order.</summary>
[Serializable, NetSerializable]
public sealed class CancelOrderMessage(string orderId) : BoundUserInterfaceMessage
{
    public string OrderId = orderId;
}

/// <summary>Purchase a quantity directly from an active market listing.</summary>
[Serializable, NetSerializable]
public sealed class PurchaseListingMessage(string orderId, int quantity) : BoundUserInterfaceMessage
{
    public string OrderId = orderId;
    public int Quantity = quantity;
}

/// <summary>Claim escrowed items/currency from a fulfilled order.</summary>
[Serializable, NetSerializable]
public sealed class ClaimEscrowMessage(string orderId) : BoundUserInterfaceMessage
{
    public string OrderId = orderId;
}

/// <summary>Deposit held item into personal market storage.</summary>
[Serializable, NetSerializable]
public sealed class MarketDepositItemMessage : BoundUserInterfaceMessage { }

/// <summary>Withdraw from personal market storage.</summary>
[Serializable, NetSerializable]
public sealed class MarketWithdrawItemMessage(string slotKey) : BoundUserInterfaceMessage
{
    public string SlotKey = slotKey;
}

/// <summary>Search entity prototypes by partial name match.</summary>
[Serializable, NetSerializable]
public sealed class ProtoSearchMessage(string query) : BoundUserInterfaceMessage
{
    public string Query = query;
}

/// <summary>Select a prototype to view its market book.</summary>
[Serializable, NetSerializable]
public sealed class SelectOrderBookMessage(string prototypeId) : BoundUserInterfaceMessage
{
    public string PrototypeId = prototypeId;
}

/// <summary>Sent from server to client: proto search results.</summary>
[Serializable, NetSerializable]
public sealed class ProtoSearchResults(List<(string Id, string Name)> results) : BoundUserInterfaceMessage
{
    public List<(string Id, string Name)> Results = results;
}

/// <summary>Immediate feedback for a market action that succeeded or failed.</summary>
[Serializable, NetSerializable]
public sealed class MarketActionResult(string message, bool success) : BoundUserInterfaceMessage
{
    public string Message = message;
    public bool Success = success;
}

// ── Server → Client State ─────────────────────────────────────────────────────

/// <summary>Full market state snapshot for the viewing player.</summary>
[Serializable, NetSerializable]
public sealed class MarketStateMessage : BoundUserInterfaceState
{
    // Item directory (grouped by prototype)
    public List<MarketItemSummary> ItemSummaries = new();
    // Full order book for the selected item
    public OrderBookEntry? SelectedOrderBook;
    // Player's own active orders
    public List<MarketOrder> MyOrders = new();
    // Player's escrowed items ready to claim
    public List<MarketOrder> MyCompletedOrders = new();
    // Activity feed
    public List<MarketFeedEntry> Feed = new();
    // Player's deposit storage
    public List<MarketDepositEntry> DepositedItems = new();
    // Currency balances
    public int Bottlecaps;
    public int NcrDollars;
    // Currently selected prototype (for order book detail)
    public string SelectedProtoId = string.Empty;
    public string SelectedProtoName = string.Empty;
    public string LastSearchQuery = string.Empty;
    public List<(string Id, string Name)> SearchResults = new();

    public string MarketName = "Wendover Free Market Exchange";
}

/// <summary>Sent when the player selects an item in the directory — returns full order book.</summary>
[Serializable, NetSerializable]
public sealed class OrderBookState : BoundUserInterfaceState
{
    public OrderBookEntry? Book;
}
