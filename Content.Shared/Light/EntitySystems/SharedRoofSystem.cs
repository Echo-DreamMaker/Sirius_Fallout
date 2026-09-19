using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Light.EntitySystems;

/// <summary>
/// Handles the roof flag for tiles that gets used for the RoofOverlay.
/// </summary>
public abstract class SharedRoofSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private HashSet<Entity<IsRoofComponent>> _roofSet = new();

    /// <summary>
    /// Returns whether the specified tile is roof-occupied.
    /// </summary>
    /// <returns>Returns false if no data or not rooved.</returns>
    public bool IsRooved(Entity<MapGridComponent, RoofComponent> grid, Vector2i index)
    {
        var roof = grid.Comp2;
        var chunkOrigin = SharedMapSystem.GetChunkIndices(index, RoofComponent.ChunkSize);

        if (roof.Data.TryGetValue(chunkOrigin, out var bitMask))
        {
            var chunkRelative = SharedMapSystem.GetChunkRelative(index, RoofComponent.ChunkSize);
            var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);

            var isRoof = (bitMask & bitFlag) == bitFlag;

            // Early out, otherwise check for components on tile.
            if (isRoof)
                return true;
        }

        _roofSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, index, _roofSet);

        foreach (var isRoofEnt in _roofSet)
        {
            if (!isRoofEnt.Comp.Enabled)
                continue;

            return true;
        }

        return false;
    }

    // Sirius edit start
    public bool SetRoof(Entity<MapGridComponent?, RoofComponent?> grid, Vector2i index, bool value, bool dirty = true)
    {
        if (!Resolve(grid.Owner, ref grid.Comp1, false))
            return false;

        if (!Resolve(grid.Owner, ref grid.Comp2, false))
        {
            if (!value)
                return false;

            grid.Comp2 = EnsureComp<RoofComponent>(grid.Owner);
        }
        // Sirius edit end

        var chunkOrigin = SharedMapSystem.GetChunkIndices(index, RoofComponent.ChunkSize);
        var roof = grid.Comp2;

        if (!roof.Data.TryGetValue(chunkOrigin, out var chunkData))
        {
            // No value to remove so leave it.
            if (!value)
            {
                return false; // Sirius edit
            }

            chunkData = 0;
        }
        // Sirius edit start
        else if (chunkData == 0)
        {
            if (!value)
            {
                roof.Data.Remove(chunkOrigin);
                if (dirty)
                    Dirty(grid.Owner, roof);

                return true;
            }
        }
        // Sirius edit end

        var chunkRelative = SharedMapSystem.GetChunkRelative(index, RoofComponent.ChunkSize);
        var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);

        if (value)
        {
            // Already set
            if ((chunkData & bitFlag) == bitFlag)
                return false; // Sirius edit

            chunkData |= bitFlag;
            // Sirius edit start
            roof.Data[chunkOrigin] = chunkData;

            if (dirty)
                Dirty(grid.Owner, roof);

            return true;
            // Sirius edit end
        }
        else
        {
            // Not already set
            if ((chunkData & bitFlag) == 0x0)
                return false; // Sirius edit

            chunkData &= ~bitFlag;

            // Sirius edit start
            if (chunkData == 0)
                roof.Data.Remove(chunkOrigin);
            else
                roof.Data[chunkOrigin] = chunkData;

            if (dirty)
                Dirty(grid.Owner, roof);

            return true;
        }
    }

    public void SetRoofArea(Entity<MapGridComponent?, RoofComponent?> grid, Box2i area, bool value)
    {
        if (!Resolve(grid.Owner, ref grid.Comp1, false))
            return;

        if (!Resolve(grid.Owner, ref grid.Comp2, false))
        {
            if (!value)
                return;

            grid.Comp2 = EnsureComp<RoofComponent>(grid.Owner);
        }

        var minX = Math.Min(area.Left, area.Right);
        var maxX = Math.Max(area.Left, area.Right);
        var minY = Math.Min(area.Bottom, area.Top);
        var maxY = Math.Max(area.Bottom, area.Top);

        var modified = false;
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (SetRoof(grid, new Vector2i(x, y), value, dirty: false))
                    modified = true;
            }
        }

        if (modified && grid.Comp2 != null)
            Dirty(grid.Owner, grid.Comp2);
    }

    public bool SetRoof(Entity<MapGridComponent> grid, Vector2i index, bool value, bool dirty = true)
    {
        return SetRoof((grid.Owner, (MapGridComponent?) grid.Comp, null), index, value, dirty);
    }

    public bool SetRoof(EntityUid gridUid, Vector2i index, bool value, bool dirty = true)
    {
        return SetRoof((gridUid, null, null), index, value, dirty);
    }

    public void SetRoofArea(Entity<MapGridComponent> grid, Box2i area, bool value)
    {
        SetRoofArea((grid.Owner, (MapGridComponent?) grid.Comp, null), area, value);
    }

    public void SetRoofArea(EntityUid gridUid, Box2i area, bool value)
    {
        SetRoofArea((gridUid, null, null), area, value);
    }
}

[Serializable, NetSerializable]
public sealed class RequestSetRoofTileEvent : EntityEventArgs
{
    public NetEntity Grid { get; }
    public Vector2i Tile { get; }
    public bool Value { get; }

    public RequestSetRoofTileEvent(NetEntity grid, Vector2i tile, bool value)
    {
        Grid = grid;
        Tile = tile;
        Value = value;
    }
}

[Serializable, NetSerializable]
public sealed class RequestSetRoofAreaEvent : EntityEventArgs
{
    public NetEntity Grid { get; }
    public Box2i Area { get; }
    public bool Value { get; }

    public RequestSetRoofAreaEvent(NetEntity grid, Box2i area, bool value)
    {
        Grid = grid;
        Area = area;
        Value = value;
    }
}
// Sirius edit end
