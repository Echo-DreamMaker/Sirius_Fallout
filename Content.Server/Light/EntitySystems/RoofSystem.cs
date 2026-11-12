using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Light.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Content.Server.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Server.Light.EntitySystems;

/// <inheritdoc/>
public sealed class RoofSystem : SharedRoofSystem
{
    [Dependency] private readonly SharedMapSystem _maps = default!;
    // Sirius edit start
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    // Sirius edit end

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        SubscribeLocalEvent<SetRoofComponent, ComponentStartup>(OnFlagStartup);
        // Sirius edit start
        SubscribeNetworkEvent<RequestSetRoofTileEvent>(OnRequestSetRoofTile);
        SubscribeNetworkEvent<RequestSetRoofAreaEvent>(OnRequestSetRoofArea);
        // Sirius edit end
    }

    private void OnFlagStartup(Entity<SetRoofComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent.Owner);

        if (_gridQuery.TryComp(xform.GridUid, out var grid))
        {
            var index = _maps.LocalToTile(xform.GridUid.Value, grid, xform.Coordinates);
            SetRoof((xform.GridUid.Value, grid, null), index, ent.Comp.Value);
        }

        QueueDel(ent.Owner);
    }

    // Sirius edit start
    private void OnRequestSetRoofTile(RequestSetRoofTileEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session)
            return;

        if (!_adminManager.HasAdminFlag(session, AdminFlags.Mapping))
            return;

        if (ev.Tile.X is < -100_000 or > 100_000 || ev.Tile.Y is < -100_000 or > 100_000)
            return;

        var gridUid = GetEntity(ev.Grid);
        if (!_gridQuery.TryComp(gridUid, out var grid))
            return;

        SetRoof((gridUid, grid, null), ev.Tile, ev.Value);
    }

    private void OnRequestSetRoofArea(RequestSetRoofAreaEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session)
            return;

        if (!_adminManager.HasAdminFlag(session, AdminFlags.Mapping))
            return;

        var gridUid = GetEntity(ev.Grid);
        if (!_gridQuery.TryComp(gridUid, out var grid))
            return;

        var minX = Math.Min(ev.Area.Left, ev.Area.Right);
        var maxX = Math.Max(ev.Area.Left, ev.Area.Right);
        var minY = Math.Min(ev.Area.Bottom, ev.Area.Top);
        var maxY = Math.Max(ev.Area.Bottom, ev.Area.Top);

        if (minX < -100_000 || maxX > 100_000 || minY < -100_000 || maxY > 100_000)
            return;

        var width = (long) maxX - minX;
        var height = (long) maxY - minY;

        if (width < 0 || width > 500 || height < 0 || height > 500)
            return;

        SetRoofArea((gridUid, grid, null), new Box2i(minX, minY, maxX, maxY), ev.Value);
    }

    public (int deletedCount, int writtenCount) MigrateMarkersToRoofs(EntityUid gridUid, MapGridComponent grid)
    {
        var roofComp = EnsureComp<RoofComponent>(gridUid);
        var roofTiles = new HashSet<Vector2i>();
        var markersToDelete = new List<EntityUid>();

        var query = EntityManager.AllEntityQueryEnumerator<IsRoofComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out var meta))
        {
            if (meta.EntityPrototype?.ID != "MarkerWeatherblocker")
                continue;

            var onThisGrid = xform.GridUid == gridUid || xform.ParentUid == gridUid;
            if (!onThisGrid)
            {
                var mapPos = _transformSystem.GetMapCoordinates(uid, xform);
                if (_maps.TryFindGridAt(mapPos, out var foundGrid, out _) && foundGrid == gridUid)
                    onThisGrid = true;
            }

            if (!onThisGrid)
                continue;

            var tilePos = _maps.LocalToTile(gridUid, grid, xform.Coordinates);
            roofTiles.Add(tilePos);
            markersToDelete.Add(uid);
        }

        var writtenCount = 0;
        foreach (var tile in roofTiles)
        {
            if (SetRoof((gridUid, grid, roofComp), tile, true, dirty: false))
            {
                writtenCount++;
            }
        }

        Dirty(gridUid, roofComp);

        foreach (var uid in markersToDelete)
        {
            QueueDel(uid);
        }

        return (markersToDelete.Count, writtenCount);
    }
}

[AdminCommand(AdminFlags.Mapping)]
public sealed class MigrateMarkersToRoofsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "migratemarkerstoproofs";

    public string Description => Loc.GetString("cmd-migratemarkerstoproofs-desc");

    public string Help => Loc.GetString("cmd-migratemarkerstoproofs-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid gridUid;
        MapGridComponent? gridComp = null;

        if (args.Length > 0)
        {
            if (NetEntity.TryParse(args[0], out var netEntity) && _entManager.TryGetEntity(netEntity, out var parsedUid) && parsedUid != null)
            {
                gridUid = parsedUid.Value;
            }
            else if (EntityUid.TryParse(args[0], out var rawUid) && _entManager.EntityExists(rawUid))
            {
                gridUid = rawUid;
            }
            else
            {
                shell.WriteError(Loc.GetString("cmd-migratemarkerstoproofs-invalid-grid", ("id", args[0])));
                return;
            }

            if (!_entManager.TryGetComponent(gridUid, out gridComp))
            {
                shell.WriteError(Loc.GetString("cmd-migratemarkerstoproofs-not-a-grid", ("id", args[0])));
                return;
            }
        }
        else
        {
            gridUid = default;

            if (shell.Player?.AttachedEntity is { } playerEnt &&
                _entManager.TryGetComponent<TransformComponent>(playerEnt, out var playerXform))
            {
                if (playerXform.GridUid is { } pGridUid && _entManager.TryGetComponent(pGridUid, out gridComp))
                {
                    gridUid = pGridUid;
                }
                else
                {
                    var mapPos = _entManager.System<SharedTransformSystem>().GetMapCoordinates(playerEnt, playerXform);
                    if (_entManager.System<SharedMapSystem>().TryFindGridAt(mapPos, out var foundGrid, out gridComp))
                    {
                        gridUid = foundGrid;
                    }
                }
            }

            if (gridComp == null)
            {
                shell.WriteError(Loc.GetString("cmd-migratemarkerstoproofs-no-grid"));
                return;
            }
        }

        var roofSystem = _entManager.System<RoofSystem>();
        var (deleted, written) = roofSystem.MigrateMarkersToRoofs(gridUid, gridComp);

        shell.WriteLine(Loc.GetString("cmd-migratemarkerstoproofs-result",
            ("grid", gridUid),
            ("deleted", deleted),
            ("written", written)));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromOptions(CompletionHelper.Components<MapGridComponent>(args[0], _entManager));
        }

        return CompletionResult.Empty;
    }
}
// Sirius edit end
