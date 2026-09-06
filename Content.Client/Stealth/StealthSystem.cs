using Content.Client.Interactable.Components;
using Content.Client.StatusIcon;
using Content.Shared._Misfits.StealthBoy;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Stealth;

public sealed class StealthSystem : SharedStealthSystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string StealthShaderId = "MisfitsStealth";

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _protoMan.Index<ShaderPrototype>("Stealth").InstanceUnique();

        SubscribeLocalEvent<StealthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StealthComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    public override void SetEnabled(EntityUid uid, bool value, StealthComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Enabled == value)
            return;

        base.SetEnabled(uid, value, component);
        SetShader(uid, value, component);
    }

    private void SetShader(EntityUid uid, bool enabled, StealthComponent? component = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref component, ref sprite, false))
            return;

        sprite.Color = Color.White;

        // #Misfits Fix - the legacy PostShader/GetScreenTexture/RaiseShaderEvent fields no
        // longer reach the shader entry the renderer actually reads, so the stealth shader
        // was running with no SCREEN_TEXTURE and never getting its uniforms set. Go through
        // SpriteSystem so both flags land on the entry.
        if (!enabled)
        {
            _sprite.RemovePostShader((uid, sprite), StealthShaderId);

            if (component.HadOutline && !TerminatingOrDeleted(uid))
                EnsureComp<InteractionOutlineComponent>(uid);
            return;
        }

        _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(StealthShaderId, _shader)
        {
            GetScreenTexture = true,
            RaiseShaderEvent = true,
        });

        if (TryComp(uid, out InteractionOutlineComponent? outline))
        {
            RemCompDeferred(uid, outline);
            component.HadOutline = true;
        }
    }

    private void OnStartup(EntityUid uid, StealthComponent component, ComponentStartup args)
    {
        SetShader(uid, component.Enabled, component);
    }

    private void OnShutdown(EntityUid uid, StealthComponent component, ComponentShutdown args)
    {
        if (!Terminating(uid))
            SetShader(uid, false, component);
    }

    private void OnShaderRender(EntityUid uid, StealthComponent component, ref BeforePostShaderRenderEvent args)
    {
        // Distortion effect uses screen coordinates. If a player moves, the entities appear to move on screen. this
        // makes the distortion very noticeable.

        // So we need to use relative screen coordinates. The reference frame we use is the parent's position on screen.
        // this ensures that if the Stealth is not moving relative to the parent, its relative screen position remains
        // unchanged.
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return; // should never happen, but lets not kill the client.
        var parentXform = Transform(parent);
        var reference = args.Viewport.WorldToLocal(parentXform.WorldPosition);
        reference.X = -reference.X;
        var visibility = GetVisibility(uid, component);

        // actual visual visibility effect is limited to +/- 1.
        visibility = Math.Clamp(visibility, -1f, 1f);


        _shader.SetParameter("reference", reference);
        _shader.SetParameter("visibility", visibility);

        visibility = MathF.Max(0, visibility);
        args.Sprite.Color = new Color(visibility, visibility, 1, 1);
    }
}
