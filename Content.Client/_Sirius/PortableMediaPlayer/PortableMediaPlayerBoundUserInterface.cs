using Content.Client._Sirius.CartridgeLoader.Cartridges;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared._Sirius.PortableMediaPlayer;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;

namespace Content.Client._Sirius.PortableMediaPlayer;

public sealed class PortableMediaPlayerBoundUserInterface : BoundUserInterface
{
    private PipBoyMusicUiFragment? _fragment;
    private DefaultWindow? _window;

    private IConfigurationManager _cfg = default!;

    public PortableMediaPlayerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _cfg = IoCManager.Resolve<IConfigurationManager>();

        _fragment = new PipBoyMusicUiFragment();

        _fragment.OnTrackSelected += id => Send(PipBoyMusicUiAction.Select, id);
        _fragment.OnPlayPressed += () => Send(PipBoyMusicUiAction.Play);
        _fragment.OnPausePressed += () => Send(PipBoyMusicUiAction.Pause);
        _fragment.OnStopPressed += () => Send(PipBoyMusicUiAction.Stop);
        _fragment.OnToggleRepeat += () => Send(PipBoyMusicUiAction.ToggleRepeat);
        _fragment.OnToggleAutoNext += () => Send(PipBoyMusicUiAction.ToggleAutoNext);

        _window = new DefaultWindow
        {
            Title = Loc.GetString("portable-media-player-window-title"),
            MinSize = new System.Numerics.Vector2(420, 500),
        };
        _window.Contents.AddChild(_fragment);
        _window.OpenCentered();
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PipBoyMusicUiState musicState)
            return;

        _fragment?.UpdateState(musicState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }

    private void Send(PipBoyMusicUiAction action, string? trackId = null)
    {
        var audible = _cfg.GetCVar(CCVars.PortableMediaPlayerAudibleToOthers);
        SendMessage(new PortableMediaPlayerMessage(action, trackId, audible));
    }
}
