using Content.Client.UserInterface.Controls;
using Content.Shared._Misfits.TreeOfLife;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.TreeOfLife;

[UsedImplicitly]
public sealed class TreeOfLifeRitesBoundUserInterface : BoundUserInterface
{
    private TreeOfLifeRitesWindow? _window;

    public TreeOfLifeRitesBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TreeOfLifeRitesWindow>();
        _window.SelectRite += rite => SendMessage(new TreeOfLifeSelectRiteMessage(rite));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is TreeOfLifeRitesState ritesState)
            _window?.SetState(ritesState);
    }
}
