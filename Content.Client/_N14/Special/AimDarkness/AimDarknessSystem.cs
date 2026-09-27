using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._N14.Special.AimDarkness;

/// <summary>
/// Owns the <see cref="AimDarknessOverlay"/>, adding it when a local player exists and
/// removing it when they leave so it never lingers over spectator/menu states.
/// </summary>
public sealed class AimDarknessSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private AimDarknessOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new AimDarknessOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (!_overlayMan.HasOverlay<AimDarknessOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }
}