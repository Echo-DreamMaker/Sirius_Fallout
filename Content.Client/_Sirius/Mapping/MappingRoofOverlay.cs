using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Mapping;

public sealed class MappingRoofOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly SharedMapSystem _mapSystem;
    private readonly SharedRoofSystem _roof;
    private readonly SharedTransformSystem _xform;
    private readonly EntityLookupSystem _lookup;
    private readonly ShaderInstance _shader;

    private readonly MappingState? _state;

    public bool Enabled { get; set; } = true;

    private static readonly Color RoofFillColor = Color.FromHex("#00BFFF44");
    private static readonly Color RoofBorderColor = Color.FromHex("#00BFFF88");

    private static readonly Color BoxDragPlaceFill = Color.FromHex("#00BFFF66");
    private static readonly Color BoxDragPlaceBorder = Color.FromHex("#00BFFFFF");
    private static readonly Color BoxDragEraseFill = Color.FromHex("#FF222266");
    private static readonly Color BoxDragEraseBorder = Color.FromHex("#FF2222FF");

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private List<Entity<MapGridComponent>> _grids = new();
    private HashSet<Entity<IsRoofComponent>> _isRoofEntities = new();
    private HashSet<Vector2i> _drawnMarkerTiles = new();

    public MappingRoofOverlay(MappingState? state = null)
    {
        IoCManager.InjectDependencies(this);

        _mapSystem = _entManager.System<SharedMapSystem>();
        _roof = _entManager.System<SharedRoofSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _lookup = _entManager.System<EntityLookupSystem>();
        _shader = _prototypeManager.Index(UnshadedShader).Instance();
        _state = state;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        EntityUid dragGridUid = default;
        var hasDrag = _state != null && _state.TryGetBoxDrag(out dragGridUid, out _, out _);
        if (!Enabled && !hasDrag)
            return;

        var bounds = args.WorldAABB;
        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);

        _grids.Clear();
        if (Enabled)
        {
            _mapSystem.FindGridsIntersecting(args.MapId, bounds, ref _grids);
        }

        if (hasDrag
            && !_grids.Exists(g => g.Owner == dragGridUid)
            && _xform.GetMapId(dragGridUid) == args.MapId
            && _entManager.TryGetComponent<MapGridComponent>(dragGridUid, out var dragGridComp))
        {
            _grids.Add(new Entity<MapGridComponent>(dragGridUid, dragGridComp));
        }

        foreach (var grid in _grids)
        {
            var tileSize = grid.Comp.TileSize;

            worldHandle.SetTransform(_xform.GetWorldMatrix(grid.Owner));

            var invMatrix = _xform.GetInvWorldMatrix(grid.Owner);
            var localAABB = invMatrix.TransformBox(bounds);

            if (Enabled)
            {
                var hasRoof = _entManager.TryGetComponent<RoofComponent>(grid.Owner, out var roofComp);

            if (hasRoof && roofComp != null && roofComp.Data.Count > 0)
            {
                foreach (var (chunkOrigin, chunkData) in roofComp.Data)
                {
                    if (chunkData == 0)
                        continue;

                    var chunkTileMin = chunkOrigin * RoofComponent.ChunkSize;
                    var chunkTileMax = chunkTileMin + new Vector2i(RoofComponent.ChunkSize, RoofComponent.ChunkSize);
                    var chunkBounds = new Box2(chunkTileMin * tileSize, chunkTileMax * tileSize);

                    if (!localAABB.Intersects(chunkBounds))
                        continue;

                    for (var rx = 0; rx < RoofComponent.ChunkSize; rx++)
                    {
                        for (var ry = 0; ry < RoofComponent.ChunkSize; ry++)
                        {
                            var bitFlag = (ulong) 1 << (rx + ry * RoofComponent.ChunkSize);
                            if ((chunkData & bitFlag) == 0)
                                continue;

                            var tilePos = chunkTileMin + new Vector2i(rx, ry);
                            var local = new Box2(tilePos * tileSize, (tilePos + Vector2i.One) * tileSize);
                            worldHandle.DrawRect(local, RoofFillColor);
                            worldHandle.DrawRect(local, RoofBorderColor, filled: false);
                        }
                    }
                }
            }

            _isRoofEntities.Clear();
            _drawnMarkerTiles.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid.Owner, localAABB, _isRoofEntities);

            foreach (var isRoof in _isRoofEntities)
            {
                if (!isRoof.Comp.Enabled)
                    continue;

                if (!_entManager.TryGetComponent<TransformComponent>(isRoof.Owner, out var xform))
                    continue;

                var tilePos = _mapSystem.LocalToTile(grid.Owner, grid.Comp, xform.Coordinates);

                if (!_drawnMarkerTiles.Add(tilePos))
                    continue;

                if (hasRoof && roofComp != null)
                {
                    var chunkOrigin = SharedMapSystem.GetChunkIndices(tilePos, RoofComponent.ChunkSize);
                    if (roofComp.Data.TryGetValue(chunkOrigin, out var cd))
                    {
                        var chunkRelative = SharedMapSystem.GetChunkRelative(tilePos, RoofComponent.ChunkSize);
                        var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);
                        if ((cd & bitFlag) != 0)
                            continue;
                    }
                }

                var local = new Box2(tilePos * tileSize, (tilePos + Vector2i.One) * tileSize);
                worldHandle.DrawRect(local, RoofFillColor);
                worldHandle.DrawRect(local, RoofBorderColor, filled: false);
            }
        }

            if (_state != null && _state.TryGetBoxDrag(grid.Owner, out var dragBox, out var isPlacing))
            {
                var min = dragBox.BottomLeft * tileSize;
                var max = (dragBox.TopRight + Vector2i.One) * tileSize;
                var previewBox = new Box2(min, max);

                var fillColor = isPlacing ? BoxDragPlaceFill : BoxDragEraseFill;
                var borderColor = isPlacing ? BoxDragPlaceBorder : BoxDragEraseBorder;

                worldHandle.DrawRect(previewBox, fillColor);
                worldHandle.DrawRect(previewBox, borderColor, filled: false);
            }
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);
    }
}
