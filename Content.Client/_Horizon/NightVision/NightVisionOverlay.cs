using Content.Shared._Horizon.NightVision;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.NightVision;

public sealed class NightVisionOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = null!;
    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly IEntityManager _entityManager = null!;
    [Dependency] private readonly ILightManager _lightManager = null!;
    [Dependency] private readonly IGameTiming _timing = null!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _greyscaleShader;
    private readonly Color _baseNightVisionColor;

    private float _currentIntensity = 0f;
    private const float TransitionSpeed = 1.5f;
    private const float MaxIntensity = 0.9f;

    private bool _isTransitioning = false;
    private bool _lastNightVisionState = false;

    public NightVisionOverlay(Color color)
    {
        IoCManager.InjectDependencies(this);
        _greyscaleShader = _prototypeManager.Index<ShaderPrototype>("GreyscaleFullscreen").InstanceUnique();
        _baseNightVisionColor = color;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var playerEntity = _playerManager.LocalSession?.AttachedEntity;

        if (playerEntity == null ||
            !_entityManager.TryGetComponent(playerEntity, out NightVisionUserComponent? nvComp))
        {
            return false;
        }

        if (_lastNightVisionState != nvComp.IsNightVision)
        {
            _isTransitioning = true;
            _lastNightVisionState = nvComp.IsNightVision;
        }

        if (!nvComp.IsNightVision && _currentIntensity <= 0f)
        {
            _lightManager.DrawLighting = true;
            return true;
        }

        return nvComp.IsNightVision || _currentIntensity > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        if (_isTransitioning)
        {
            if (_lastNightVisionState)
            {
                _lightManager.DrawLighting = false;
                _currentIntensity = Math.Min(_currentIntensity + TransitionSpeed * (float) _timing.FrameTime.TotalSeconds, MaxIntensity);
                if (_currentIntensity >= MaxIntensity)
                    _isTransitioning = false;
            }
            else
            {
                _currentIntensity = Math.Max(_currentIntensity - TransitionSpeed * (float) _timing.FrameTime.TotalSeconds, 0);
                _lightManager.DrawLighting = true;
                if (_currentIntensity <= 0f)
                    _isTransitioning = false;
            }
        }

        _greyscaleShader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        var targetColor = _baseNightVisionColor.WithAlpha(_currentIntensity);

        worldHandle.UseShader(_greyscaleShader);
        worldHandle.DrawRect(viewport, targetColor);
        worldHandle.UseShader(null);
    }
}
