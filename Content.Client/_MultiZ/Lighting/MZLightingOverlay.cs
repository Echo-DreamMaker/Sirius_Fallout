// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using System.Numerics;
using Content.Shared._MultiZ;
using Content.Shared._MultiZ.Core;
using Content.Shared._MultiZ.Core.Components;
using Content.Shared._MultiZ.Core.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client._MultiZ.Lighting;

/// <summary>
/// Projects point lights from adjacent Z-levels through openings.
/// Lights on the level above/below appear on the current level at matching world positions.
/// </summary>
public sealed class MZLightingOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly MZOpeningCache _openingCache = new();
    private readonly List<Entity<MapGridComponent>> _gridScratch = new();

    public MZLightingOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_cfg.GetCVar(MZCVars.ProjectedLightingEnabled))
            return;

        var player = _player.LocalSession?.AttachedEntity;
        if (player == null)
            return;

        if (!_entMan.TryGetComponent<TransformComponent>(player.Value, out var playerXform))
            return;

        if (playerXform.MapUid is not { } mapUid)
            return;

        if (!_entMan.TryGetComponent<MZMapComponent>(mapUid, out var zMap))
            return;

        var zSystem = _entMan.System<MZSharedSystem>();
        var lightSystem = _entMan.System<PointLightSystem>();

        // Project lights from the level above
        if (zSystem.TryMapUp((mapUid, zMap), out var aboveMap))
        {
            ProjectLightsFromMap(args, aboveMap.Value, lightSystem);
        }

        // Project lights from the level below
        if (zSystem.TryMapDown((mapUid, zMap), out var belowMap))
        {
            ProjectLightsFromMap(args, belowMap.Value, lightSystem);
        }
    }

    private void ProjectLightsFromMap(in OverlayDrawArgs args, Entity<MZMapComponent> sourceMap, PointLightSystem lightSystem)
    {
        // #Cythisiax Note: Full light projection requires access to SharedPointLightComponent
        // state which has restricted access permissions. Simplified for now.
        var query = _entMan.EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            if (xform.MapUid != sourceMap.Owner)
                continue;

            if (!light.Enabled)
                continue;

            var worldPos = _entMan.System<SharedTransformSystem>().GetWorldPosition(xform);
            var radius = light.Radius * 0.5f;

            // Draw a semi-transparent indicator for projected light sources
            args.WorldHandle.DrawCircle(worldPos, radius, Color.Yellow.WithAlpha(0.1f));
        }
    }
}
