using Content.Shared._Nuclear14.AutodocSirius;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using System.Numerics;
using DrawDepthShared = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Nuclear14.AutodocSirius;
public sealed class SiriusAutodocSystem : SharedSiriusAutodocSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SiriusAutodocComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<InsideAutodocComponent, ComponentStartup>(OnInsideStartup);
        SubscribeLocalEvent<InsideAutodocComponent, ComponentRemove>(OnInsideRemove);
    }
    private void OnInsideStartup(EntityUid uid, InsideAutodocComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        component.PreviousOffset = sprite.Offset;
        _spriteSystem.SetOffset(uid, new Vector2(0, 0.35f));

        if (TryComp<SiriusAutodocComponent>(Transform(uid).ParentUid, out var autodoc))
        {
            sprite.Visible = autodoc.IsOpen;
        }
    }
    private void OnInsideRemove(EntityUid uid, InsideAutodocComponent component, ComponentRemove args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _spriteSystem.SetOffset(uid, component.PreviousOffset);
        sprite.Visible = true;
    }
    private void OnAppearanceChange(EntityUid uid, SiriusAutodocComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        if (!_appearance.TryGetData<bool>(uid, SiriusAutodocComponent.AutodocVisuals.IsOn, out var isOn, args.Component))
            isOn = false;
        if (!_appearance.TryGetData<bool>(uid, SiriusAutodocComponent.AutodocVisuals.IsOpen, out var isOpen, args.Component))
            isOpen = true;
        var baseLayer = args.Sprite.LayerMapReserveBlank(AutodocVisualLayers.Base);
        _spriteSystem.LayerSetRsiState(uid, baseLayer, isOn ? "autodoc_on" : "autodoc_off");
        var doorLayer = args.Sprite.LayerMapReserveBlank(AutodocVisualLayers.Door);
        if (isOpen)
        {
            _spriteSystem.LayerSetVisible(uid, doorLayer, false);
            _spriteSystem.SetDrawDepth(uid, (int) DrawDepthShared.Objects);

            if (component.BodyContainer.ContainedEntity is { } patient)
            {
                if (TryComp<SpriteComponent>(patient, out var patientSprite))
                    patientSprite.Visible = true;
            }
        }
        else
        {
            _spriteSystem.LayerSetVisible(uid, doorLayer, true);
            _spriteSystem.LayerSetRsiState(uid, doorLayer, "autodoc_door");
            _spriteSystem.SetDrawDepth(uid, (int) DrawDepthShared.Walls);

            if (component.BodyContainer.ContainedEntity is { } patient)
            {
                if (TryComp<SpriteComponent>(patient, out var patientSprite))
                    patientSprite.Visible = false;
            }
        }
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }
}
public enum AutodocVisualLayers : byte
{
    Base,
    Door
}
