using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Rounding;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void InitializeMagazineVisuals()
    {
        SubscribeLocalEvent<MagazineVisualsComponent, ComponentInit>(OnMagazineVisualsInit);
        SubscribeLocalEvent<MagazineVisualsComponent, AppearanceChangeEvent>(OnMagazineVisualsChange);
    }
    private static readonly bool LOG = false;
    private void OnMagazineVisualsInit(EntityUid uid, MagazineVisualsComponent component, ComponentInit args)
    {
        //_spriteQuery.TryComp(uid, out var s);
        if (!_spriteQuery.TryComp(uid, out var s)) return;
        Entity<SpriteComponent?> sprite = (uid, s);
        if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.Mag, out _, LOG))
        {
            _sprite.LayerSetRsiState(sprite, GunVisualLayers.Mag, $"{component.MagState}-{component.MagSteps - 1}");
            _sprite.LayerSetVisible(sprite, GunVisualLayers.Mag, false);
        }

        if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.MagUnshaded, out _, LOG))
        {
            _sprite.LayerSetRsiState(sprite, GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{component.MagSteps - 1}");
            _sprite.LayerSetVisible(sprite, GunVisualLayers.MagUnshaded, false);
        }
    }

    private void OnMagazineVisualsChange(EntityUid uid, MagazineVisualsComponent component, ref AppearanceChangeEvent args)
    {
        // tl;dr
        // 1.If no mag then hide it OR
        // 2. If step 0 isn't visible then hide it (mag or unshaded)
        // 3. Otherwise just do mag / unshaded as is
        Entity<SpriteComponent?> sprite = (uid, args.Sprite);

        if (sprite.Comp is null) return;

        if (!args.AppearanceData.TryGetValue(AmmoVisuals.MagLoaded, out var magloaded) ||
            magloaded is true)
        {
            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoMax, out var capacity))
            {
                capacity = component.MagSteps;
            }

            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoCount, out var current))
            {
                current = component.MagSteps;
            }

            var step = ContentHelpers.RoundToLevels((int) current, (int) capacity, component.MagSteps);

            if (step == 0 && !component.ZeroVisible)
            {
                if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.Mag, out _, LOG))
                {
                    _sprite.LayerSetVisible(sprite, GunVisualLayers.Mag, false);
                }

                if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.MagUnshaded, out _, LOG))
                {
                    _sprite.LayerSetVisible(sprite, GunVisualLayers.MagUnshaded, false);
                }

                return;
            }

            if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.Mag, out _, LOG))
            {
                _sprite.LayerSetVisible(sprite, GunVisualLayers.Mag, true);
                _sprite.LayerSetRsiState(sprite, GunVisualLayers.Mag, $"{component.MagState}-{step}");
            }

            if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.MagUnshaded, out _, LOG))
            {
                _sprite.LayerSetVisible(sprite, GunVisualLayers.MagUnshaded, true);
                _sprite.LayerSetRsiState(sprite, GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{step}");
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.Mag, out _, LOG))
            {
                _sprite.LayerSetVisible(sprite, GunVisualLayers.Mag, false);
            }

            if (_sprite.LayerMapTryGet(sprite, GunVisualLayers.MagUnshaded, out _, LOG))
            {
                _sprite.LayerSetVisible(sprite, GunVisualLayers.MagUnshaded, false);
            }
        }
    }
}
