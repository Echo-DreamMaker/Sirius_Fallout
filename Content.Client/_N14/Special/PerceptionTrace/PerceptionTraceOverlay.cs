using System.Numerics;
using Content.Shared.Body.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._N14.Special.PerceptionTrace;

/// <summary>
/// Renders faint "infrared" afterimages of living mobs that moved recently, exactly like
/// thermal/IR goggles but driven by high Perception. Because it lives in world space the
/// afterimages are drawn over walls and other obstacles.
/// </summary>
public sealed class PerceptionTraceOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private readonly TransformSystem _transform;
    private readonly PerceptionTraceSystem _system;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public PerceptionTraceOverlay(PerceptionTraceSystem system)
    {
        IoCManager.InjectDependencies(this);

        _transform = _entity.System<TransformSystem>();
        _system = system;

        ZIndex = -1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null)
            return;

        var map = eye.Position.MapId;
        var eyeRot = eye.Rotation;
        var worldHandle = args.WorldHandle;

        var color = _system.CurrentColor;
        var alpha = _system.CurrentAlpha;
        if (alpha <= 0f)
            return;

        foreach (var (uid, fade) in _system.EnumerateActive())
        {
            if (!_entity.TryGetComponent(uid, out BodyComponent? body)
                || !_entity.TryGetComponent(uid, out TransformComponent? xform)
                || !_entity.TryGetComponent(uid, out SpriteComponent? sprite))
                continue;

            if (xform.MapID != map || !sprite.Visible)
                continue;

            var position = _transform.GetWorldPosition(xform);
            var rotation = _transform.GetWorldRotation(xform);

            var original = sprite.Color;
            sprite.Color = color.WithAlpha(Math.Clamp(alpha * fade, 0f, 1f));
            sprite.Render(worldHandle, eyeRot, rotation, position: position);
            sprite.Color = original;
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
    }
}
