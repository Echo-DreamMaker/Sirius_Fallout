using Content.Shared._Horizon.NightVision;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Horizon.NightVision;

public sealed class NightVisionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = null!;
    [Dependency] private readonly IOverlayManager _overlayMan = null!;
    [Dependency] private readonly ILightManager _lightManager = null!;

    private NightVisionOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionUserComponent, ComponentInit>(OnNightVisionInit);
        SubscribeLocalEvent<NightVisionUserComponent, ComponentShutdown>(OnNightVisionShutdown);
        SubscribeLocalEvent<NightVisionUserComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NightVisionUserComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(RoundRestartCleanup);
    }

    private void OnPlayerAttached(EntityUid uid, NightVisionUserComponent component, LocalPlayerAttachedEvent args)
    {
        if (_overlay != null)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, NightVisionUserComponent component, LocalPlayerDetachedEvent args)
    {
        if (_overlay != null)
        {
            _overlayMan.RemoveOverlay(_overlay);
            _lightManager.DrawLighting = true;
        }
    }

    private void OnNightVisionInit(EntityUid uid, NightVisionUserComponent component, ComponentInit args)
    {
        _overlay = new NightVisionOverlay(component.NightVisionColor);
        if (_player.LocalSession?.AttachedEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnNightVisionShutdown(EntityUid uid, NightVisionUserComponent component, ComponentShutdown args)
    {
        if (_overlay != null && _player.LocalSession?.AttachedEntity == uid)
        {
            _lightManager.DrawLighting = true;
            _overlayMan.RemoveOverlay(_overlay);
        }
        _overlay = null;
    }

    private void RoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _lightManager.DrawLighting = true;
    }
}
