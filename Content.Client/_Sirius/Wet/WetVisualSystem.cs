using Content.Shared._Sirius.Wet;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._Sirius.Wet;

// #Sirius Add: wet characters are slightly darker and cooler while the status effect is active.
public sealed class WetVisualSystem : EntitySystem
{
    // #Sirius Add: a subtle tint instead of a full overlay, so existing sprites stay recognisable.
    private static readonly Color WetTint = new(0.82f, 0.85f, 0.9f, 1f);

    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WetComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WetComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        ent.Comp.SavedSpriteColor = sprite.Color;
        ent.Comp.SavedSpriteColorSet = true;
        _sprite.SetColor((ent.Owner, sprite), sprite.Color * WetTint);
    }

    private void OnShutdown(Entity<WetComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.SavedSpriteColorSet)
            return;

        if (TryComp<SpriteComponent>(ent.Owner, out var sprite))
            _sprite.SetColor((ent.Owner, sprite), ent.Comp.SavedSpriteColor);

        ent.Comp.SavedSpriteColorSet = false;
    }
}
