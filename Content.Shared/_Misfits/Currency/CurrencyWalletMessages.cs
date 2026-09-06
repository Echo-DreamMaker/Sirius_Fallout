// #Misfits Change - Currency wallet UI messages
using Content.Shared._Misfits.Currency.Components;
using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Currency;

/// <summary>
/// Action event raised when the player wants to open the currency wallet UI.
/// </summary>
public sealed partial class OpenCurrencyWalletEvent : InstantActionEvent
{
}

/// <summary>
/// Sent from the server to the client with the current Bottle Caps balance so the wallet UI can be shown/updated.
/// </summary>
[Serializable, NetSerializable]
public sealed class CurrencyWalletStateMessage : EntityEventArgs
{
    public int Bottlecaps;

    // #Misfits Add - track NCR Dollars, Legion Denarii, and Pre-War Money alongside Bottlecaps -Tytos
    public int NcrDollars;
    public int LegionDenarii;
    public int PrewarMoney;

    /// <summary>
    /// When true the client should open/focus the wallet window.
    /// When false the client only updates the balance if the window is already visible.
    /// </summary>
    public bool OpenWindow;
}

/// <summary>
/// Sent from the client to the server to request a currency withdrawal.
/// </summary>
[Serializable, NetSerializable]
public sealed class WithdrawCurrencyRequest : EntityEventArgs
{
    public CurrencyType CurrencyType;
    public int Amount;
}

/// <summary>
/// Sent from the client HUD button to the server to request the wallet UI be opened.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenWalletHudMessage : EntityEventArgs
{
}

/// <summary>
/// Sent from the client wallet window to the server to deposit whatever ConsumableCurrency item is currently in hand.
/// </summary>
[Serializable, NetSerializable]
public sealed class DepositHeldCurrencyRequest : EntityEventArgs
{
}
