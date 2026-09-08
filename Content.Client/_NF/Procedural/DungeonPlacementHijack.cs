using Content.Shared.Procedural;
using Content.Shared.Procedural.DungeonGenerators;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client._NF.Procedural;

/// <summary>
/// Placement hijack that intercepts a click, converts it to tile coordinates,
/// and fires the "dungen" admin console command.
/// Also manages the footprint preview overlay lifecycle.
/// </summary>
public sealed class DungeonPlacementHijack : PlacementHijack
{
    private readonly IConsoleHost _console;
    private readonly IEntityManager _entityManager;
    private readonly IOverlayManager _overlayManager;
    private readonly DungeonConfigPrototype _config;
    private DungeonFootprintOverlay? _overlay;

    public override bool CanRotate => false;

    public DungeonPlacementHijack(DungeonConfigPrototype config)
    {
        _console = IoCManager.Resolve<IConsoleHost>();
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _overlayManager = IoCManager.Resolve<IOverlayManager>();
        _config = config;
    }

    public override void StartHijack(PlacementManager manager)
    {
        base.StartHijack(manager);
        _overlay = new DungeonFootprintOverlay(manager, _config);
        _overlayManager.AddOverlay(_overlay);
    }

    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        // Resolve map and grid
        var mapSystem = _entityManager.System<SharedMapSystem>();
        var xformSystem = _entityManager.System<SharedTransformSystem>();

        var mapId = xformSystem.GetMapId(coordinates);
        if (mapId == MapId.Nullspace)
            return true;

        // Find the map entity
        var mapManager = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedMapSystem>();
        var mapUid = mapManager.GetMap(mapId);

        // Use the snapped tile position as the dungeon origin
        var mapCoords = xformSystem.ToMapCoordinates(coordinates);
        var tileX = (int) MathF.Floor(mapCoords.Position.X);
        var tileY = (int) MathF.Floor(mapCoords.Position.Y);

        var mapIntId = (int) mapId;
        _console.ExecuteCommand($"dungen {mapIntId} {_config.ID} {tileX} {tileY}");

        // Don't chain-place; cancel after one successful placement.
        Manager.Clear();
        return true;
    }

    public void StopHijack()
    {
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
        Manager.Clear();
    }
}
