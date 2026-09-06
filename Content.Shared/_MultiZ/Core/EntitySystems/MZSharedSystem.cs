// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._MultiZ.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Profiling;

namespace Content.Shared._MultiZ.Core.EntitySystems;

/// <summary>
/// Core shared Multi-Z API. Provides methods to query and traverse Z-level networks.
/// Server and client both inherit from this.
/// </summary>
public abstract partial class MZSharedSystem : EntitySystem
{
    /// <summary>
    /// World-space sprite displacement used when projecting adjacent Z-levels into the active view.
    /// </summary>
    public const float ZLevelVisualOffset = 0.75f;

    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] protected ProfManager Prof = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MZMapComponent> _zMapQuery;
    protected EntityQuery<MapGridComponent> GridQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _mapQuery = GetEntityQuery<MapComponent>();
        _zMapQuery = GetEntityQuery<MZMapComponent>();
        GridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        InitMovement();
    }

    // ── Z-Network Queries ────────────────────────────────────────────────

    /// <summary>
    /// Checks whether the map is part of a Z-level network.
    /// If so, returns the network entity and its component.
    /// </summary>
    [PublicAPI]
    public bool TryGetZNetwork(EntityUid mapUid, [NotNullWhen(true)] out Entity<MZNetworkComponent>? zLevel)
    {
        zLevel = null;

        // Fast path: check the map's own MZMapComponent for a cached network reference
        if (_zMapQuery.TryComp(mapUid, out var zLevelMapComp) &&
            zLevelMapComp.NetworkUid.IsValid() &&
            !TerminatingOrDeleted(zLevelMapComp.NetworkUid) &&
            TryComp<MZNetworkComponent>(zLevelMapComp.NetworkUid, out var cachedNetwork))
        {
            zLevel = (zLevelMapComp.NetworkUid, cachedNetwork);
            return true;
        }

        // Slow path: scan all networks
        var query = EntityQueryEnumerator<MZNetworkComponent>();
        while (query.MoveNext(out var uid, out var zLevelComp))
        {
            if (!zLevelComp.ZLevelByEntity.ContainsKey(mapUid))
                continue;

            zLevel = (uid, zLevelComp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the given map is in the specified Z-network.
    /// </summary>
    [PublicAPI]
    public bool IsMapInNetwork(Entity<MZNetworkComponent> network, EntityUid mapUid)
        => network.Comp.ZLevelByEntity.ContainsKey(mapUid);

    // ── Map Offset Lookup ────────────────────────────────────────────────

    /// <summary>
    /// Gets the map at the given offset (+1 = above, -1 = below) from the input map.
    /// </summary>
    [PublicAPI]
    public bool TryMapOffset(Entity<MZMapComponent?> inputMapUid, int offset,
        [NotNullWhen(true)] out Entity<MZMapComponent>? outputMapUid)
    {
        outputMapUid = null;
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false))
            return false;

        // Fast path: direct MapAbove / MapBelow
        if (offset == 1 && inputMapUid.Comp.MapAbove is { } mapAbove &&
            _zMapQuery.TryComp(mapAbove, out var mapAboveComp))
        {
            outputMapUid = (mapAbove, mapAboveComp);
            return true;
        }

        if (offset == -1 && inputMapUid.Comp.MapBelow is { } mapBelow &&
            _zMapQuery.TryComp(mapBelow, out var mapBelowComp))
        {
            outputMapUid = (mapBelow, mapBelowComp);
            return true;
        }

        // Try via cached network
        if (inputMapUid.Comp.NetworkUid.IsValid() &&
            TryComp<MZNetworkComponent>(inputMapUid.Comp.NetworkUid, out var cachedNetwork) &&
            cachedNetwork.ZLevels.TryGetValue(inputMapUid.Comp.Depth + offset, out var cachedTargetMapUid) &&
            cachedTargetMapUid is { } targetUid &&
            _zMapQuery.TryComp(targetUid, out var cachedTargetZLevelComp))
        {
            outputMapUid = (targetUid, cachedTargetZLevelComp);
            return true;
        }

        // Slow path: scan all networks
        var query = EntityQueryEnumerator<MZNetworkComponent>();
        while (query.MoveNext(out var network))
        {
            if (!network.ZLevelByEntity.TryGetValue(inputMapUid, out var inputDepth))
                continue;

            if (!network.ZLevels.TryGetValue(inputDepth + offset, out var targetMapUid))
                continue;

            if (targetMapUid is not { } resolved || !_zMapQuery.TryComp(resolved, out var targetZLevelComp))
                continue;

            outputMapUid = (resolved, targetZLevelComp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Like TryMapOffset but also returns the MapComponent.
    /// </summary>
    [PublicAPI]
    public bool TryMapOffset(
        Entity<MZMapComponent?> inputMapUid, int offset,
        [NotNullWhen(true)] out Entity<MZMapComponent>? outputMapUid,
        [NotNullWhen(true)] out MapComponent? outputMap)
    {
        outputMap = null;

        if (!TryMapOffset(inputMapUid, offset, out outputMapUid) ||
            !_mapQuery.TryComp(outputMapUid.Value.Owner, out outputMap))
        {
            return false;
        }

        return true;
    }

    // ── Convenience Methods ──────────────────────────────────────────────

    [PublicAPI]
    public bool TryMapUp(Entity<MZMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<MZMapComponent>? aboveMapUid)
        => TryMapOffset(inputMapUid, 1, out aboveMapUid);

    [PublicAPI]
    public bool TryMapDown(Entity<MZMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<MZMapComponent>? belowMapUid)
        => TryMapOffset(inputMapUid, -1, out belowMapUid);

    // ── Coordinate Projection ────────────────────────────────────────────

    /// <summary>
    /// Projects a world position to an adjacent Z-level map.
    /// </summary>
    [PublicAPI]
    public bool TryProjectToZMap(
        Entity<MZMapComponent?> inputMapUid, int offset,
        Vector2 worldPosition,
        out MapCoordinates coordinates,
        [NotNullWhen(true)] out Entity<MZMapComponent>? outputMapUid)
    {
        coordinates = default;

        if (!TryMapOffset(inputMapUid, offset, out outputMapUid, out var outputMap))
            return false;

        coordinates = new MapCoordinates(worldPosition, outputMap.MapId);
        return true;
    }

    /// <summary>
    /// Creates MapCoordinates for a given map entity and world position.
    /// </summary>
    [PublicAPI]
    public bool TryGetMapCoordinates(EntityUid map, Vector2 worldPosition, out MapCoordinates coordinates)
    {
        coordinates = default;
        if (!_mapQuery.TryComp(map, out var mapComp))
            return false;

        coordinates = new MapCoordinates(worldPosition, mapComp.MapId);
        return true;
    }

    // ── Bulk Queries ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all maps above the specified map, closest first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsAbove(Entity<MZMapComponent> inputMapUid)
    {
        var result = new List<EntityUid>();
        var currentMap = inputMapUid;

        while (currentMap.Comp.MapAbove is { } above &&
               _zMapQuery.TryComp(above, out var aboveComp))
        {
            result.Add(above);
            currentMap = (above, aboveComp);
        }

        return result;
    }

    /// <summary>
    /// Returns all maps below the specified map, closest first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsBelow(Entity<MZMapComponent> inputMapUid)
    {
        var result = new List<EntityUid>();
        var currentMap = inputMapUid;

        while (currentMap.Comp.MapBelow is { } below &&
               _zMapQuery.TryComp(below, out var belowComp))
        {
            result.Add(below);
            currentMap = (below, belowComp);
        }

        return result;
    }

    // ── Network Depth Queries ────────────────────────────────────────────

    [PublicAPI]
    public bool TryGetDepthBounds(Entity<MZNetworkComponent> network, out int minDepth, out int maxDepth)
    {
        minDepth = int.MaxValue;
        maxDepth = int.MinValue;

        foreach (var entry in network.Comp.ZLevels)
        {
            if (!entry.Value.HasValue)
                continue;

            minDepth = Math.Min(minDepth, entry.Key);
            maxDepth = Math.Max(maxDepth, entry.Key);
        }

        return minDepth != int.MaxValue;
    }

    [PublicAPI]
    public bool TryGetMapAtDepth(Entity<MZNetworkComponent> network, int depth, out EntityUid map)
    {
        map = default;

        if (!network.Comp.ZLevels.TryGetValue(depth, out var mapUid) || mapUid is not { } resolved)
            return false;

        map = resolved;
        return true;
    }

    // ── Entity Movement ──────────────────────────────────────────────────

    /// <summary>
    /// Moves an entity to the specified Z-level offset from its current map.
    /// Preserves world position and handles pulled entities.
    /// </summary>
    [PublicAPI]
    public bool TryMove(EntityUid ent, int offset, Entity<MZMapComponent?>? map = null, Vector2? worldPosition = null)
    {
        map ??= Transform(ent).MapUid;

        if (map is null)
            return false;

        if (!TryMapOffset(map.Value, offset, out _, out var targetMapComp))
            return false;

        var target = new MapCoordinates(worldPosition ?? _transform.GetWorldPosition(ent), targetMapComp.MapId);
        _transform.SetMapCoordinates(ent, target);
        RaiseLocalEvent(ent, new MZLevelMoveEvent(offset));

        return true;
    }

    [PublicAPI]
    public bool TryMoveUp(EntityUid ent) => TryMove(ent, 1);

    [PublicAPI]
    public bool TryMoveDown(EntityUid ent) => TryMove(ent, -1);

    // ── Tile Above Query ─────────────────────────────────────────────────

    /// <summary>
    /// Checks whether there is a solid tile directly above the entity on the next Z-level.
    /// </summary>
    [PublicAPI]
    public bool HasTileAbove(EntityUid ent, Entity<MZMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        if (!GridQuery.TryComp(mapAboveUid.Value, out var mapAboveGrid))
            return false;

        if (_map.TryGetTileRef(mapAboveUid.Value, mapAboveGrid, _transform.GetWorldPosition(ent), out var tileRef) &&
            !tileRef.Tile.IsEmpty)
        {
            return true;
        }

        return false;
    }
}
