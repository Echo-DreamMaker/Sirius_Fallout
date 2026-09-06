using System.Numerics;
using Content.Shared._Misfits.Overwatch;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client._Misfits.Overwatch;

/// <summary>
/// Screen-space banner shown to a player while they are being watched through an
/// Overwatch console. Lists the operator names from <see cref="OverwatchTargetComponent"/>.
/// </summary>
internal sealed class OverwatchTargetOverlay : Overlay
{
    private readonly IEntityManager _entityManager;
    private readonly IPlayerManager _player;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public OverwatchTargetOverlay(IEntityManager entityManager, IPlayerManager player, IResourceCache resourceCache)
    {
        _entityManager = entityManager;
        _player = player;

        // Draw above the HUD but below in-game popups/chat where practical.
        ZIndex = 210;
        _font = new VectorFont(
            resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player ||
            !_entityManager.TryGetComponent<OverwatchTargetComponent>(player, out var target) ||
            target.WatcherNames.Count == 0)
        {
            return;
        }

        var text = Loc.GetString("overwatch-target-hud", ("names", string.Join(", ", target.WatcherNames)));
        var textSize = args.ScreenHandle.GetDimensions(_font, text, 1f);
        var bounds = args.ViewportBounds;

        var pos = new Vector2((bounds.Width - textSize.X) / 2f, 48f);
        var padding = new Vector2(10f, 6f);
        var topLeft = pos - padding;
        var bottomRight = pos + textSize + padding;

        args.ScreenHandle.DrawRect(new UIBox2(topLeft, bottomRight), new Color(0.05f, 0.05f, 0.05f, 0.85f));
        args.ScreenHandle.DrawString(_font, pos, text, Color.FromHex("#D94B4B"));
    }
}
