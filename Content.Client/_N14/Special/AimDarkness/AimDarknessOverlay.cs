using System.Numerics;
using Content.Shared._NC.CameraFollow.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._N14.Special.AimDarkness;

/// <summary>
/// Renders a dimming "cone" while aim mode is active: everything outside a ~90° sector
/// around the current aim line slowly darkens down to 25% visibility over a few seconds.
/// Moving the aim swings the sector, instantly scattering the darkness it covers; cancelling
/// aim mode restores full brightness immediately (the overlay just stops drawing).
/// </summary>
public sealed class AimDarknessOverlay : Overlay
{
    private const float HalfConeAngle = MathF.PI / 4f; // 45° to each side of the aim line
    private const float MaxDarkness = 0.75f;           // leaves 25% visibility outside the cone
    private const float DarknessRampTime = 5f;
    private const float FallbackApexRaise = 1f;        // tiles above feet when no usable sprite bounds

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly ShaderInstance _shader;
    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;

    private bool _wasAiming;
    private TimeSpan _aimStart;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public AimDarknessOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index<ShaderPrototype>("N14AimConeDarkness").InstanceUnique();
        _transform = _entity.System<TransformSystem>();
        _sprite = _entity.System<SpriteSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _player.LocalEntity;
        if (player is not { Valid: true })
        {
            _wasAiming = false;
            return false;
        }

        if (!_entity.TryGetComponent<CameraFollowComponent>(player.Value, out var follow) || !follow.Enabled)
        {
            _wasAiming = false;
            return false;
        }

        if (!_wasAiming)
        {
            _wasAiming = true;
            _aimStart = _timing.RealTime;
        }

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _player.LocalEntity!.Value;
        if (!_entity.TryGetComponent<TransformComponent>(player, out var xform))
            return;

        // Anchoring the cone at the character's feet looks wrong - the sector forms a couple
        // of tiles below the visible body. Raise the apex to the vertical center of the
        // character's sprite so the darkness radiates from the held weapon instead.
        var apexWorld = _transform.GetWorldPosition(xform);
        if (_entity.TryGetComponent<SpriteComponent>(player, out var sprite))
            apexWorld += new Vector2(0f, _sprite.GetLocalBounds((player, sprite)).Center.Y);
        else
            apexWorld += new Vector2(0f, FallbackApexRaise);

        // Both coordinates stay in UI screen space (top-left origin, y-down); the shader
        // converts them to framebuffer pixels internally so the cone is DPI-proof.
        var apexUi = _eye.WorldToScreen(apexWorld);
        var mouse = _input.MouseScreenPosition.Position;

        var dirUi = mouse - apexUi;
        if (dirUi.LengthSquared() < 1f)
            return;

        var elapsed = (_timing.RealTime - _aimStart).TotalSeconds;
        var ramp = Math.Clamp(elapsed / DarknessRampTime, 0.0, 1.0);
        var darkness = (float)ramp * MaxDarkness;

        _shader.SetParameter("uApex", apexUi);
        _shader.SetParameter("uDir", Vector2.Normalize(dirUi));
        _shader.SetParameter("uHalfAngle", HalfConeAngle);
        _shader.SetParameter("uDarkness", darkness);

        var handle = args.ScreenHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_shader);
        handle.DrawRect(new UIBox2(args.ViewportBounds.Left, args.ViewportBounds.Top, args.ViewportBounds.Right, args.ViewportBounds.Bottom), Color.White);
        handle.UseShader(null);
    }
}