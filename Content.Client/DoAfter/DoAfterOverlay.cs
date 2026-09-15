using System.Numerics;
using Content.Shared.DoAfter;
using Content.Client.UserInterface.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Configuration;
using Robust.Client.ResourceManagement;
using Content.Shared.CCVar;
using Content.Client.Stylesheets;

namespace Content.Client.DoAfter;

public sealed class DoAfterOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly IPlayerManager _player;
    private readonly IConfigurationManager _cfg;
    private readonly SharedTransformSystem _transform;
    private readonly MetaDataSystem _meta;
    private readonly ProgressColorSystem _progressColor;
    private readonly IStylesheetManager _stylesheet;

    private readonly Texture _barTexture;
    private readonly ShaderInstance _unshadedShader;

    /// <summary>
    ///     Flash time for cancelled DoAfters
    /// </summary>
    private const float FlashTime = 0.125f;

    // Hardcoded width of the progress bar because it doesn't match the texture.
    private const float StartX = 2;
    private const float EndX = 22f;

    private bool _useModernHUD = false;
    private readonly Texture _falloutBgTexture;
    private readonly Texture _falloutBlocksTexture;
    private bool _useFalloutHUD = false;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public DoAfterOverlay(IEntityManager entManager, IPrototypeManager protoManager, IGameTiming timing, IPlayerManager player)
    {
        _entManager = entManager;
        _timing = timing;
        _player = player;
        _cfg = IoCManager.Resolve<IConfigurationManager>();
        _stylesheet = IoCManager.Resolve<IStylesheetManager>();
        _transform = _entManager.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
        _meta = _entManager.EntitySysManager.GetEntitySystem<MetaDataSystem>();
        _progressColor = _entManager.System<ProgressColorSystem>();

        var sprite = new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/progress_bar.rsi"), "icon");
        _barTexture = _entManager.EntitySysManager.GetEntitySystem<SpriteSystem>().Frame0(sprite);

        _unshadedShader = protoManager.Index<ShaderPrototype>("unshaded").Instance();

        _useModernHUD = _cfg.GetCVar(CCVars.ModernProgressBar);
        _cfg.OnValueChanged(CCVars.ModernProgressBar, (newValue) => { _useModernHUD = newValue; });

        var resCache = IoCManager.Resolve<IResourceCache>();
        _falloutBgTexture = resCache.GetResource<TextureResource>("/Textures/_Sirius/Interface/Misc/fallout_bg.png").Texture;
        _falloutBlocksTexture = resCache.GetResource<TextureResource>("/Textures/_Sirius/Interface/Misc/fallout_blocks.png").Texture;

        _useFalloutHUD = _cfg.GetCVar(CCVars.FalloutProgressBar);
        _cfg.OnValueChanged(CCVars.FalloutProgressBar, (newValue) => { _useFalloutHUD = newValue; });
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        const float scale = 1f;
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        var curTime = _timing.CurTime;

        var bounds = args.WorldAABB.Enlarged(5f);
        var localEnt = _player.LocalSession?.AttachedEntity;
        var themeColor = StyleNano.NanoGold;

        var metaQuery = _entManager.GetEntityQuery<MetaDataComponent>();
        var enumerator = _entManager.AllEntityQueryEnumerator<ActiveDoAfterComponent, DoAfterComponent, SpriteComponent, TransformComponent>();

        while (enumerator.MoveNext(out var uid, out _, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (comp.DoAfters.Count == 0)
                continue;

            var worldPosition = _transform.GetWorldPosition(xform, xformQuery);
            if (!bounds.Contains(worldPosition))
                continue;

            if (uid != localEnt)
                handle.UseShader(null);
            else
                handle.UseShader(_unshadedShader);

            var meta = metaQuery.GetComponent(uid);
            var time = meta.EntityPaused
                ? curTime - _meta.GetPauseTime(uid, meta)
                : curTime;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPosition);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var offset = 0f;

            foreach (var doAfter in comp.DoAfters.Values)
            {
                var alpha = 1f;
                if (doAfter.Args.Hidden)
                {
                    if (uid != localEnt && localEnt != doAfter.Args.ShowTo)
                        continue;

                    alpha = 0.5f;
                }

                Color color;
                float elapsedRatio;

                if (doAfter.CancelledTime != null)
                {
                    var elapsed = doAfter.CancelledTime.Value - doAfter.StartTime;
                    elapsedRatio = (float) Math.Min(1, elapsed.TotalSeconds / doAfter.Args.Delay.TotalSeconds);
                    var cancelElapsed = (time - doAfter.CancelledTime.Value).TotalSeconds;
                    var flash = Math.Floor(cancelElapsed / FlashTime) % 2 == 0;
                    color = GetProgressColor(0, flash ? alpha : 0);
                }
                else
                {
                    var elapsed = time - doAfter.StartTime;
                    elapsedRatio = (float) Math.Min(1, elapsed.TotalSeconds / doAfter.Args.Delay.TotalSeconds);

                    if (_useModernHUD)
                    {
                        if (elapsedRatio < 1.0f)
                            color = GetProgressColor(elapsedRatio, alpha);
                        else
                            color = GetProgressColor(0.35f, alpha);
                    }
                    else
                    {
                        color = GetProgressColor(elapsedRatio, alpha);
                    }
                }

                if (_useFalloutHUD)
                {
                    float falloutYOffset = sprite.Bounds.Height / 2f - 0.35f;
                    var falloutPos = new Vector2(-_falloutBgTexture.Width / 2f / EyeManager.PixelsPerMeter,
                        falloutYOffset / scale + offset / EyeManager.PixelsPerMeter * scale);
                    handle.DrawTexture(_falloutBgTexture, falloutPos, themeColor.WithAlpha(alpha));
                    const int totalBlocks = 10;
                    int visibleBlocks = 0;
                    if (elapsedRatio > 0f)
                    {
                        visibleBlocks = (int) Math.Min(totalBlocks, Math.Ceiling(elapsedRatio * totalBlocks));
                    }
                    if (visibleBlocks > 0)
                    {
                        const float startXPixels = 6f;
                        const float blockWidthPixels = 2f;

                        for (int i = 0; i < visibleBlocks; i++)
                        {
                            float blockLeftPx = startXPixels + (i * blockWidthPixels);
                            var sourceRect = new UIBox2(
                                blockLeftPx,
                                0,
                                blockLeftPx + blockWidthPixels,
                                _falloutBlocksTexture.Height
                            );
                            var blockOffsetWorld = new Vector2(blockLeftPx / EyeManager.PixelsPerMeter, 0f);
                            var destRect = Box2.FromDimensions(
                                falloutPos + blockOffsetWorld,
                                new Vector2(blockWidthPixels / EyeManager.PixelsPerMeter, _falloutBlocksTexture.Height / EyeManager.PixelsPerMeter)
                            );
                            handle.DrawTextureRectRegion(_falloutBlocksTexture, destRect, color, sourceRect);
                        }
                    }

                    offset += _barTexture.Height / scale;
                }
                else
                {
                    float vanillaYOffset = sprite.Bounds.Height / 2f + 0.05f;
                    var position = new Vector2(-_barTexture.Width / 2f / EyeManager.PixelsPerMeter,
                        vanillaYOffset / scale + offset / EyeManager.PixelsPerMeter * scale);
                    handle.DrawTexture(_barTexture, position);

                    var xProgress = (EndX - StartX) * elapsedRatio + StartX;

                    if (_useModernHUD)
                    {
                        var box = new Box2(new Vector2(StartX, 2f) / EyeManager.PixelsPerMeter, new Vector2(xProgress, 5f) / EyeManager.PixelsPerMeter);
                        box = box.Translated(position);

                        var boxInner = new Box2(new Vector2(StartX, 3f) / EyeManager.PixelsPerMeter, new Vector2(xProgress, 4f) / EyeManager.PixelsPerMeter);
                        boxInner = boxInner.Translated(position);

                        handle.DrawRect(box, color);
                        handle.DrawRect(boxInner, Color.InterpolateBetween(color, Color.White, 0.5f));
                    }
                    else
                    {
                        var box = new Box2(new Vector2(StartX, 3f) / EyeManager.PixelsPerMeter, new Vector2(xProgress, 4f) / EyeManager.PixelsPerMeter);
                        box = box.Translated(position);

                        handle.DrawRect(box, color);
                    }

                    offset += _barTexture.Height / scale;
                }
            }
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    public Color GetProgressColor(float progress, float alpha = 1f)
    {
        return _progressColor.GetProgressColor(progress).WithAlpha(alpha);
    }
}
