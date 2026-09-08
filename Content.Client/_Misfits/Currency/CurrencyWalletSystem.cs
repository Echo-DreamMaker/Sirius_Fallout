// #Misfits Change - Client-side currency wallet system
using Content.Client._Misfits.Currency.Widgets;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Misfits.Currency;
using Content.Shared._Misfits.Currency.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.Currency;

/// <summary>
/// Handles opening the currency wallet window and processing withdrawal requests on the client.
/// </summary>
public sealed class CurrencyWalletSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private CurrencyWalletWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CurrencyWalletStateMessage>(OnCurrencyWalletState);

        // #Misfits Change - hook the dedicated HUD button on screen load/unload
        var loadController = _uiManager.GetUIController<GameplayStateLoadController>();
        loadController.OnScreenLoad += OnScreenLoad;
        loadController.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        if (_uiManager.GetActiveUIWidgetOrNull<CurrencyWalletGui>() is { } gui)
            gui.OnWalletPressed += OnHudWalletPressed;
    }

    private void OnScreenUnload()
    {
        if (_uiManager.GetActiveUIWidgetOrNull<CurrencyWalletGui>() is { } gui)
            gui.OnWalletPressed -= OnHudWalletPressed;
    }

    private void OnHudWalletPressed()
    {
        RaiseNetworkEvent(new OpenWalletHudMessage());
    }

    /// <summary>
    /// Public entry-point so external UI (e.g. Character Menu) can trigger the same server-side
    /// wallet refresh and open the full wallet window. #Misfits Add
    /// </summary>
    public void OpenWallet()
    {
        RaiseNetworkEvent(new OpenWalletHudMessage());
    }

    private void OnCurrencyWalletState(CurrencyWalletStateMessage msg)
    {
        // #Misfits Fix - Only open/focus the window when the server explicitly requests it
        // (HUD button, ATM interaction). Background updates (load, deposit, withdraw) just
        // refresh the balance if the window happens to be open already.
        if (msg.OpenWindow)
        {
            EnsureWindow();
            _window!.UpdateState(msg.Bottlecaps, msg.NcrDollars, msg.LegionDenarii, msg.PrewarMoney);
            _window.OpenCentered();
        }
        else if (_window is { Disposed: false, IsOpen: true })
        {
            _window.UpdateState(msg.Bottlecaps, msg.NcrDollars, msg.LegionDenarii, msg.PrewarMoney);
        }
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = new CurrencyWalletWindow();
        _window.OnWithdrawRequest += OnWithdrawRequest;
        _window.OnDepositInHandRequest += OnDepositInHand;
    }

    private void OnDepositInHand()
    {
        RaiseNetworkEvent(new DepositHeldCurrencyRequest());
    }

    private void OnWithdrawRequest(CurrencyType type, int amount)
    {
        RaiseNetworkEvent(new WithdrawCurrencyRequest
        {
            CurrencyType = type,
            Amount = amount,
        });
    }
}

