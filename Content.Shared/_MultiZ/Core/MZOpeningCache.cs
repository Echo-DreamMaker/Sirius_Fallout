// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Shared._MultiZ.Core;

/// <summary>
/// Caches which grid chunks contain Z-level openings (empty or transparent tiles).
/// Provides fast queries for finding openings near world positions.
/// </summary>
public sealed class MZOpeningCache
{
    public const int DefaultChunkSize = 8;

    private readonly Dictionary<EntityUid, GridOpeningCache> _gridCaches = new();
    private readonly int _chunkSize;

    public MZOpeningCache(int chunkSize = DefaultChunkSize)
    {
        _chunkSize = chunkSize;
    }

    public int ChunkSize => _chunkSize;

    public void Clear()
    {
        _gridCaches.Clear();
    }

    public void RemoveGrid(EntityUid grid)
    {
        _gridCaches.Remove(grid);
    }

    public void InvalidateTiles(Entity<MapGridComponent> grid, ReadOnlySpan<TileChangedEntry> changes)
    {
        if (!_gridCaches.TryGetValue(grid.Owner, out var cache))
            return;

        cache.LastTileModifiedTick = grid.Comp.LastTileModifiedTick;

        if (changes.Length == 0)
        {
            cache.Chunks.Clear();
            return;
        }

        for (var i = 0; i < changes.Length; i++)
        {
            var chunk = SharedMapSystem.GetChunkIndices(changes[i].GridIndices, _chunkSize);
            cache.Chunks.Remove(chunk);
        }
    }

    /// <summary>
    /// Returns true if any tile in the given chunk is an opening (empty or transparent).
    /// </summary>
    public bool ChunkHasOpening(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        return GetChunkOpenings(grid, chunk, map, tile).HasOpening;
    }

    /// <summary>
    /// Finds the nearest opening center within searchRadius of sourcePosition.
    /// </summary>
    public bool TryFindNearestOpeningCenterNear(
        MapId mapId,
        Vector2 sourcePosition,
        float searchRadius,
        out Vector2 openingCenter,
        List<Entity<MapGridComponent>> gridScratch,
        SharedMapSystem map,
        SharedTransformSystem transform,
        ITileDefinitionManager tileDefinition,
        bool edgeOnly = true)
    {
        openingCenter = default;

        var searchBounds = Box2.CenteredAround(sourcePosition, new Vector2(searchRadius * 2f, searchRadius * 2f));
        gridScratch.Clear();
        map.FindGridsIntersecting(mapId, searchBounds, ref gridScratch, approx: true, includeMap: true);

        if (gridScratch.Count == 0)
            return false;

        var bottomLeft = new MapCoordinates(searchBounds.BottomLeft, mapId);
        var topRight = new MapCoordinates(searchBounds.TopRight, mapId);
        var searchRadiusSquared = searchRadius * searchRadius;
        var bestDistanceSquared = float.PositiveInfinity;
        var foundOpening = false;

        foreach (var grid in gridScratch)
        {
            GetTileSearchBounds(grid, bottomLeft, topRight, map, out var startX, out var endX, out var startY, out var endY);

            var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), _chunkSize);
            var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), _chunkSize);
            var gridWorldMatrix = transform.GetWorldMatrix(grid.Owner);
            if (!Matrix3x2.Invert(gridWorldMatrix, out var gridInvWorldMatrix))
                continue;

            var localSourcePosition = Vector2.Transform(sourcePosition, gridInvWorldMatrix);
            var sourceInsideOpening = IsExistingOpeningTile(
                grid,
                new Vector2i((int)MathF.Floor(localSourcePosition.X), (int)MathF.Floor(localSourcePosition.Y)),
                map, tileDefinition);

            for (var chunkX = startChunk.X; chunkX <= endChunk.X; chunkX++)
            {
                for (var chunkY = startChunk.Y; chunkY <= endChunk.Y; chunkY++)
                {
                    var chunk = new Vector2i(chunkX, chunkY);
                    var chunkStart = chunk * _chunkSize;
                    var chunkEnd = chunkStart + new Vector2i(_chunkSize, _chunkSize);
                    var tileStartX = Math.Max(startX, chunkStart.X);
                    var tileEndX = Math.Min(endX, chunkEnd.X - 1);
                    var tileStartY = Math.Max(startY, chunkStart.Y);
                    var tileEndY = Math.Min(endY, chunkEnd.Y - 1);

                    TryFindNearestOpeningInChunk(
                        grid, chunk, tileStartX, tileEndX, tileStartY, tileEndY,
                        sourcePosition, localSourcePosition, sourceInsideOpening,
                        gridWorldMatrix, searchRadiusSquared, edgeOnly,
                        map, tileDefinition,
                        ref foundOpening, ref bestDistanceSquared, ref openingCenter);
                }
            }
        }

        return foundOpening;
    }

    /// <summary>
    /// Returns true if the given tile is an opening (empty space).
    /// Transparent tile support not yet implemented — add Transparent field to ContentTileDefinition later.
    /// </summary>
    public static bool IsOpeningTile(Tile tile, ITileDefinitionManager tileDefinition)
    {
        if (tile.IsEmpty)
            return true;

        // #Cythisiax Note: CMU checks ContentTileDefinition.Transparent here.
        // misfits doesn't have that field yet. Can add later.
        return false;
    }

    public static bool IsOpeningTile(
        Entity<MapGridComponent> grid, Vector2i tile,
        SharedMapSystem map, ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef))
            return true;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    public static bool IsOpeningTile(
        EntityUid mapUid, MapGridComponent grid, Vector2 position,
        SharedMapSystem map, ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(mapUid, grid, position, out var tileRef))
            return true;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    private static bool IsExistingOpeningTile(
        Entity<MapGridComponent> grid, Vector2i tile,
        SharedMapSystem map, ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef))
            return false;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    private CachedChunk GetChunkOpenings(
        Entity<MapGridComponent> grid, Vector2i chunk,
        SharedMapSystem map, ITileDefinitionManager tile)
    {
        if (!_gridCaches.TryGetValue(grid.Owner, out var cache))
        {
            cache = new GridOpeningCache();
            _gridCaches[grid.Owner] = cache;
        }

        if (cache.LastTileModifiedTick != grid.Comp.LastTileModifiedTick)
        {
            cache.LastTileModifiedTick = grid.Comp.LastTileModifiedTick;
            cache.Chunks.Clear();
        }

        if (cache.Chunks.TryGetValue(chunk, out var cached))
            return cached;

        cached = CalculateChunkOpenings(grid, chunk, map, tile);
        cache.Chunks[chunk] = cached;
        return cached;
    }

    private CachedChunk CalculateChunkOpenings(
        Entity<MapGridComponent> grid, Vector2i chunk,
        SharedMapSystem map, ITileDefinitionManager tile)
    {
        var startX = chunk.X * _chunkSize;
        var startY = chunk.Y * _chunkSize;
        var endX = startX + _chunkSize;
        var endY = startY + _chunkSize;

        var hasOpening = false;
        var openingMask = 0UL;

        for (var x = startX; x < endX; x++)
        {
            for (var y = startY; y < endY; y++)
            {
                if (IsOpeningTile(grid, new Vector2i(x, y), map, tile))
                {
                    hasOpening = true;
                    if (_chunkSize == DefaultChunkSize)
                        openingMask |= OpeningMaskBit(new Vector2i(startX, startY), x, y);
                }
            }
        }

        return new CachedChunk(hasOpening, openingMask);
    }

    private void TryFindNearestOpeningInChunk(
        Entity<MapGridComponent> grid, Vector2i chunk,
        int startX, int endX, int startY, int endY,
        Vector2 sourcePosition, Vector2 localSourcePosition, bool sourceInsideOpening,
        Matrix3x2 gridWorldMatrix, float searchRadiusSquared, bool edgeOnly,
        SharedMapSystem map, ITileDefinitionManager tileDefinition,
        ref bool foundOpening, ref float bestDistanceSquared, ref Vector2 bestOpeningCenter)
    {
        var cached = GetChunkOpenings(grid, chunk, map, tileDefinition);
        if (!cached.HasOpening)
            return;

        if (_chunkSize == DefaultChunkSize)
        {
            var chunkStart = chunk * DefaultChunkSize;
            var tStartX = Math.Max(startX, chunkStart.X);
            var tEndX = Math.Min(endX, chunkStart.X + DefaultChunkSize - 1);
            var tStartY = Math.Max(startY, chunkStart.Y);
            var tEndY = Math.Min(endY, chunkStart.Y + DefaultChunkSize - 1);

            for (var tileY = tStartY; tileY <= tEndY; tileY++)
            {
                for (var tileX = tStartX; tileX <= tEndX; tileX++)
                {
                    var bit = OpeningMaskBit(chunkStart, tileX, tileY);
                    if ((cached.OpeningMask & bit) == 0)
                        continue;

                    TryUseNearestOpeningTile(grid, new Vector2i(tileX, tileY),
                        sourcePosition, localSourcePosition, sourceInsideOpening,
                        gridWorldMatrix, searchRadiusSquared, edgeOnly,
                        map, tileDefinition,
                        ref foundOpening, ref bestDistanceSquared, ref bestOpeningCenter);
                }
            }
            return;
        }

        // Fallback for non-standard chunk sizes
        var fbChunkStart = chunk * _chunkSize;
        var fbTileStartX = Math.Max(startX, fbChunkStart.X);
        var fbTileEndX = Math.Min(endX, fbChunkStart.X + _chunkSize - 1);
        var fbTileStartY = Math.Max(startY, fbChunkStart.Y);
        var fbTileEndY = Math.Min(endY, fbChunkStart.Y + _chunkSize - 1);

        for (var tileX = fbTileStartX; tileX <= fbTileEndX; tileX++)
        {
            for (var tileY = fbTileStartY; tileY <= fbTileEndY; tileY++)
            {
                var openingTile = new Vector2i(tileX, tileY);
                if (!IsOpeningTile(grid, openingTile, map, tileDefinition))
                    continue;

                TryUseNearestOpeningTile(grid, openingTile,
                    sourcePosition, localSourcePosition, sourceInsideOpening,
                    gridWorldMatrix, searchRadiusSquared, edgeOnly,
                    map, tileDefinition,
                    ref foundOpening, ref bestDistanceSquared, ref bestOpeningCenter);
            }
        }
    }

    private static void TryUseNearestOpeningTile(
        Entity<MapGridComponent> grid, Vector2i openingTile,
        Vector2 sourcePosition, Vector2 localSourcePosition, bool sourceInsideOpening,
        Matrix3x2 gridWorldMatrix, float searchRadiusSquared, bool edgeOnly,
        SharedMapSystem map, ITileDefinitionManager tileDefinition,
        ref bool foundOpening, ref float bestDistanceSquared, ref Vector2 bestOpeningCenter)
    {
        var center = Vector2.Transform(
            new Vector2(openingTile.X + 0.5f, openingTile.Y + 0.5f),
            gridWorldMatrix);
        var distanceSquared = Vector2.DistanceSquared(sourcePosition, center);
        if (distanceSquared > searchRadiusSquared || distanceSquared >= bestDistanceSquared)
            return;

        foundOpening = true;
        bestDistanceSquared = distanceSquared;
        bestOpeningCenter = center;
    }

    private static ulong OpeningMaskBit(Vector2i chunkStart, int tileX, int tileY)
    {
        var localX = tileX - chunkStart.X;
        var localY = tileY - chunkStart.Y;
        var bit = localY * DefaultChunkSize + localX;
        return 1UL << bit;
    }

    private static void GetTileSearchBounds(
        Entity<MapGridComponent> grid, MapCoordinates bottomLeft, MapCoordinates topRight,
        SharedMapSystem map, out int startX, out int endX, out int startY, out int endY)
    {
        var tileBottomLeft = map.TileIndicesFor(grid.Owner, grid.Comp, bottomLeft);
        var tileTopRight = map.TileIndicesFor(grid.Owner, grid.Comp, topRight);

        startX = Math.Min(tileBottomLeft.X, tileTopRight.X) - 1;
        endX = Math.Max(tileBottomLeft.X, tileTopRight.X) + 1;
        startY = Math.Min(tileBottomLeft.Y, tileTopRight.Y) - 1;
        endY = Math.Max(tileBottomLeft.Y, tileTopRight.Y) + 1;
    }

    private sealed class GridOpeningCache
    {
        public GameTick LastTileModifiedTick;
        public readonly Dictionary<Vector2i, CachedChunk> Chunks = new();
    }

    private readonly record struct CachedChunk(bool HasOpening, ulong OpeningMask);
}
