// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Content.Shared._MultiZ.Core.Components;
using Content.Shared._MultiZ.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._MultiZ.Core;

/// <summary>
/// Client-side Multi-Z system. Manages overlays for Z-level blur, visible entity projection,
/// and stair FOV previews.
/// </summary>
public sealed class MZClientSystem : MZSharedSystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private MZBlurOverlay _blurOverlay = default!;
    private MZAerialRoofOverlay _aerialRoofOverlay = default!;
    private MZSkyPlayerOverlay _skyPlayerOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _blurOverlay = new MZBlurOverlay();
        _aerialRoofOverlay = new MZAerialRoofOverlay();
        _skyPlayerOverlay = new MZSkyPlayerOverlay();
        _overlayManager.AddOverlay(_blurOverlay);
        _overlayManager.AddOverlay(_aerialRoofOverlay);
        _overlayManager.AddOverlay(_skyPlayerOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay(_blurOverlay);
        _overlayManager.RemoveOverlay(_aerialRoofOverlay);
        _overlayManager.RemoveOverlay(_skyPlayerOverlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateStairPreviews(frameTime);
    }

    /// <summary>
    /// Updates automatic stair previews — when a player is near stair highground entities,
    /// the level above is temporarily revealed through openings.
    /// </summary>
    private void UpdateStairPreviews(float frameTime)
    {
        var player = _player.LocalSession?.AttachedEntity;
        if (player == null)
            return;

        if (!TryComp<MZViewerComponent>(player.Value, out var viewer))
            return;

        var xform = Transform(player.Value);
        if (xform.MapUid is not { } mapUid || !TryComp<MZMapComponent>(mapUid, out var zMap))
            return;

        if (!GridQuery.TryComp(mapUid, out var mapGrid))
            return;

        // Scan for highground entities near the player
        var playerTile = _map.WorldToTile(mapUid, mapGrid, _transform.GetWorldPosition(xform));
        var previewCount = 0;

        for (var x = -3; x <= 3; x++)
        {
            for (var y = -3; y <= 3; y++)
            {
                if (previewCount >= MZViewerComponent.MaxStairPreviewPositions)
                    break;

                var tile = playerTile + new Vector2i(x, y);
                var query = _map.GetAnchoredEntitiesEnumerator(mapUid, mapGrid, tile);

                while (query.MoveNext(out var uid))
                {
                    if (!TryComp<MZHighGroundComponent>(uid, out var hg) || !hg.PreviewUpLevel)
                        continue;

                    var center = _map.ToCenterCoordinates(mapUid, tile, mapGrid);
                    viewer.SetStairPreviewPosition(previewCount, center.Position);
                    previewCount++;
                    break;
                }
            }
        }

        if (viewer.StairPreviewPositionCount != previewCount)
        {
            viewer.StairPreviewPositionCount = previewCount;
            viewer.StairPreviewUp = previewCount > 0;
        }
    }
}
