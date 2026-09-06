// Origin: misfits-14 _MultiZ
// #Cythisiax Add - visible player or vehicle marker while viewing from empty sky layers

using System.Numerics;
using Content.Shared._MultiZ;
using Content.Shared._MultiZ.Core.Components;
using Content.Shared._MultiZ.Core.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client._MultiZ.Core;

/// <summary>
/// Draws the local player or their outer vehicle while the empty sky layer is
/// rendered as the lower map. Sky-map entities are not part of that lower-map
/// viewport pass and must be composited explicitly.
/// </summary>
public sealed class MZSkyPlayerOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public MZSkyPlayerOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_cfg.GetCVar(MZCVars.Enabled) || !_cfg.GetCVar(MZCVars.RenderEnabled))
            return;

        var player = _player.LocalSession?.AttachedEntity;
        if (player == null)
            return;

        if (!_entMan.TryGetComponent<TransformComponent>(player.Value, out var xform) ||
            xform.MapUid is not { } mapUid ||
            !_entMan.TryGetComponent<MZMapComponent>(mapUid, out _))
        {
            return;
        }

        if (HasRenderableGrids(mapUid))
            return;

        if (args.ViewportControl == null)
        {
            return;
        }

        var displayEntity = GetSkyDisplayEntity(player.Value, xform);
        if (!_entMan.TryGetComponent<TransformComponent>(displayEntity, out var displayXform) ||
            !_entMan.TryGetComponent<SpriteComponent>(displayEntity, out var sprite))
        {
            return;
        }

        var worldPos = _entMan.System<SharedTransformSystem>().GetWorldPosition(displayXform);
        var screenPos = args.ViewportControl.WorldToScreen(worldPos);
        args.ScreenHandle.DrawEntity(
            displayEntity,
            screenPos,
            Vector2.One,
            null,
            args.Viewport.Eye?.Rotation ?? default,
            sprite: sprite,
            xform: displayXform,
            xformSystem: _entMan.System<SharedTransformSystem>());
    }

    /// <summary>
    /// Buckled occupants are children of their vehicle. Empty sky maps are
    /// composited manually, so draw the outer sprite-bearing parent instead
    /// of exposing the otherwise hidden occupant as a floating player marker.
    /// </summary>
    private EntityUid GetSkyDisplayEntity(EntityUid player, TransformComponent playerXform)
    {
        var displayEntity = player;
        var currentXform = playerXform;

        while (currentXform.ParentUid != currentXform.MapUid &&
               _entMan.TryGetComponent<TransformComponent>(currentXform.ParentUid, out var parentXform))
        {
            var parent = currentXform.ParentUid;
            if (_entMan.HasComponent<SpriteComponent>(parent))
                displayEntity = parent;

            currentXform = parentXform;
        }

        return displayEntity;
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
