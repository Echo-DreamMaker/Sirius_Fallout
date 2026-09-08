using System.Numerics;
using Content.Shared._Misfits.Scavenging;
using Robust.Client.GameObjects;

namespace Content.Client._Misfits.Scavenging;

/// <summary>
/// Makes searched junk piles visibly smaller until their shared loot cooldown has elapsed.
/// </summary>
public sealed class JunkPileSearchVisualizerSystem : VisualizerSystem<JunkPileSearchableComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    protected override void OnAppearanceChange(EntityUid uid, JunkPileSearchableComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !_appearance.TryGetData(uid, JunkPileVisuals.Depleted, out bool depleted, args.Component))
            return;

        _sprite.SetScale((uid, args.Sprite), depleted ? new Vector2(0.45f) : new Vector2(0.8f));
    }
}
