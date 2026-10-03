using Content.Client._Sirius.UserInterface.Systems.AdminObserver.Widgets;
using Content.Client.Gameplay;
using Content.Shared._Sirius.AdminObserver;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Sirius.UserInterface.Systems.AdminObserver;

public sealed class AdminObserverHandsUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<SharedAdminObserverHandsSystem>
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private AdminObserverHandsGui? HandsGui => UIManager.GetActiveUIWidgetOrNull<AdminObserverHandsGui>();

    public void OnSystemLoaded(SharedAdminObserverHandsSystem system)
    {
        system.LocalPlayerHandsUpdated += UpdateGui;
    }

    public void OnSystemUnloaded(SharedAdminObserverHandsSystem system)
    {
        system.LocalPlayerHandsUpdated -= UpdateGui;
    }

    public void OnStateEntered(GameplayState state)
    {
        UpdateGui();
    }

    public void UpdateGui()
    {
        if (HandsGui == null)
            return;

        if (EntityManager.TryGetComponent(_playerManager.LocalEntity, out AdminObserverHandsComponent? comp))
        {
            HandsGui.Visible = true;
            HandsGui.OnHandsUpdated(comp.HandsEnabled);
        }
        else
        {
            HandsGui.Visible = false;
        }
    }

    public void ToggleHands() => EntityManager.RaisePredictiveEvent(new ToggleAdminObserverHandsEvent());
}
