// #Misfits Add - readable rooftop presentation for empty MultiZ sky layers.
using System.Numerics;
using Content.Shared._MultiZ;
using Content.Shared._MultiZ.Core.Components;
using Content.Shared._MultiZ.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._MultiZ.Core;

/// <summary>
/// Replaces exposed interiors with readable rooftop panels when an empty sky
/// layer is compositing the map below. Roof authority remains the ordinary
/// <see cref="SharedRoofSystem"/>, including MarkerWeatherblocker IsRoof tiles.
/// </summary>
public sealed class MZAerialRoofOverlay : Overlay
{
    // Near-black and mostly opaque: enough structure bleeds through to read the
    // footprint from altitude, while interiors remain deliberately unattractive
    // as landing targets.
    private static readonly Color RoofPanel = Color.FromHex("#080808E8");
    private static readonly Color RoofSeam = Color.FromHex("#202020F2");

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly SharedMapSystem _map;
    private readonly SharedRoofSystem _roof;
    private readonly SharedTransformSystem _transform;

    // Draw after the lower world and its lighting. This makes roofed cells a
    // near-opaque mask instead of merely tinting the light buffer.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public MZAerialRoofOverlay()
    {
        IoCManager.InjectDependencies(this);

        _map = _entMan.System<SharedMapSystem>();
        _roof = _entMan.System<SharedRoofSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!ShouldDrawForLowerMap(args.MapId))
            return;

        var bounds = args.WorldAABB;
        var worldHandle = args.WorldHandle;

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, bounds, ref _grids);

        foreach (var grid in _grids)
        {
            if (!_entMan.TryGetComponent<RoofComponent>(grid.Owner, out var roofComponent))
                continue;

            worldHandle.SetTransform(_transform.GetWorldMatrix(grid.Owner));

            var tiles = _map.GetTilesEnumerator(grid.Owner, grid, bounds);
            var roof = (grid.Owner, grid.Comp, roofComponent);
            while (tiles.MoveNext(out var tile))
            {
                if (!_roof.IsRooved(roof, tile.GridIndices))
                    continue;

                var tileSize = grid.Comp.TileSize;
                var local = new Box2(tile.GridIndices * tileSize, (tile.GridIndices + Vector2i.One) * tileSize);
                worldHandle.DrawRect(local, RoofPanel);
                worldHandle.DrawRect(local, RoofSeam, filled: false);
            }
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private bool ShouldDrawForLowerMap(MapId renderedMap)
    {
        if (!_cfg.GetCVar(MZCVars.Enabled) || !_cfg.GetCVar(MZCVars.RenderEnabled) ||
            _player.LocalSession?.AttachedEntity is not { } player ||
            !_entMan.TryGetComponent<TransformComponent>(player, out var playerXform) ||
            playerXform.MapUid is not { } skyMap ||
            !_entMan.TryGetComponent<MZMapComponent>(skyMap, out var zMap) ||
            HasRenderableGrids(skyMap))
        {
            return false;
        }

        var zSystem = _entMan.System<MZSharedSystem>();
        return zSystem.TryMapDown((skyMap, zMap), out var belowMap) &&
            _entMan.TryGetComponent<MapComponent>(belowMap.Value.Owner, out var belowMapComponent) &&
            belowMapComponent.MapId == renderedMap;
    }

    private bool HasRenderableGrids(EntityUid mapUid)
    {
        var query = _entMan.EntityQueryEnumerator<TransformComponent, MapGridComponent>();
        while (query.MoveNext(out _, out var xform, out _))
        {
            if (xform.MapUid == mapUid)
                return true;
        }

        return false;
    }
}
