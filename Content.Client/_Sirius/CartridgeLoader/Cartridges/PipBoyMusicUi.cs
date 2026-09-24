using Content.Client.UserInterface.Fragments;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Sirius.CartridgeLoader.Cartridges;

public sealed partial class PipBoyMusicUi : UIFragment
{
    private PipBoyMusicUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new PipBoyMusicUiFragment();

        _fragment.OnTrackSelected += id => SendMessage(userInterface, PipBoyMusicUiAction.Select, id);
        _fragment.OnPlayPressed += () => SendMessage(userInterface, PipBoyMusicUiAction.Play, null);
        _fragment.OnStopPressed += () => SendMessage(userInterface, PipBoyMusicUiAction.Stop, null);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not PipBoyMusicUiState musicState)
            return;

        _fragment?.UpdateState(musicState);
    }

    private static void SendMessage(BoundUserInterface ui, PipBoyMusicUiAction action, string? trackId)
    {
        var msg = new PipBoyMusicUiMessageEvent(action, trackId);
        ui.SendMessage(new CartridgeUiMessage(msg));
    }
}
