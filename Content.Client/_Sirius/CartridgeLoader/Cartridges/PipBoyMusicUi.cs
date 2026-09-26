using Content.Client.UserInterface.Fragments;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;

namespace Content.Client._Sirius.CartridgeLoader.Cartridges;

public sealed partial class PipBoyMusicUi : UIFragment
{
    private IConfigurationManager _cfg = default!;

    private PipBoyMusicUiFragment? _fragment;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _cfg = IoCManager.Resolve<IConfigurationManager>();

        _fragment = new PipBoyMusicUiFragment();

        _fragment.OnTrackSelected += id => Send(userInterface, PipBoyMusicUiAction.Select, id);
        _fragment.OnPlayPressed += () => Send(userInterface, PipBoyMusicUiAction.Play);
        _fragment.OnPausePressed += () => Send(userInterface, PipBoyMusicUiAction.Pause);
        _fragment.OnStopPressed += () => Send(userInterface, PipBoyMusicUiAction.Stop);
        _fragment.OnToggleRepeat += () => Send(userInterface, PipBoyMusicUiAction.ToggleRepeat);
        _fragment.OnToggleAutoNext += () => Send(userInterface, PipBoyMusicUiAction.ToggleAutoNext);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not PipBoyMusicUiState musicState)
            return;
        _fragment?.UpdateState(musicState);
    }

    private void Send(BoundUserInterface ui, PipBoyMusicUiAction action, string? trackId = null)
    {
        var audible = _cfg.GetCVar(CCVars.PipBoyMusicAudibleToOthers);
        var msg = new PipBoyMusicUiMessageEvent(action, trackId, audible);
        ui.SendMessage(new CartridgeUiMessage(msg));
    }
}
