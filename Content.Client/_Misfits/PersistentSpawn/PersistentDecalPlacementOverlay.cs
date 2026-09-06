// #Misfits Add - Cursor overlay for the Persistent Decal Spawn panel.
// Mirrors DecalPlacementOverlay (Content.Client/Decals/Overlays/DecalPlacementOverlay.cs)
// but reads placement data from PersistentDecalPlacementSystem instead.
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.PersistentSpawn;

/// <summary>
/// Renders a preview sprite under the cursor when the Persistent Decal Spawn window is active.
/// </summary>
public sealed class PersistentDecalPlacementOverlay : Overlay
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    private readonly SharedMapSystem _mapManager;

    private readonly PersistentDecalPlacementSystem _placement;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public PersistentDecalPlacementOverlay(
        PersistentDecalPlacementSystem placement,
        SharedTransformSystem transform,
        SpriteSystem sprite)
    {
        IoCManager.InjectDependencies(this);
        _mapManager = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedMapSystem>();
        _placement = placement;
        _transform = transform;
        _sprite = sprite;
        // Render on top of regular decals but below regular entity placement overlay
        ZIndex = 999;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var (decal, snap, rotation, color) = _placement.GetActiveDecal();

        if (decal == null)
            return;

        var mouseScreenPos = _inputManager.MouseScreenPosition;
        var mousePos = _eyeManager.PixelToMap(mouseScreenPos);

        if (mousePos.MapId != args.MapId)
            return;

        // Decals require a grid — no support for empty map space
        if (!_mapManager.TryFindGridAt(mousePos, out var gridUid, out var grid))
            return;

        var worldMatrix = _transform.GetWorldMatrix(gridUid);
        var invMatrix = _transform.GetInvWorldMatrix(gridUid);

        var handle = args.WorldHandle;
        handle.SetTransform(worldMatrix);

        var localPos = Vector2.Transform(mousePos.Position, invMatrix);

        if (snap)
            localPos = localPos.Floored() + grid.TileSizeHalfVector;

        var aabb = Box2.UnitCentered.Translated(localPos);
        var box = new Box2Rotated(aabb, rotation, localPos);

        handle.DrawTextureRect(_sprite.Frame0(decal.Sprite), box, color);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
