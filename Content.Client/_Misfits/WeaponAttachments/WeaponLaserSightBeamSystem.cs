// #Misfits Add - Client-only visible aiming beam for the laser sight attachment.
using System.Numerics;
using Content.Client.CombatMode;
using Content.Client.Effects;
using Content.Shared._Misfits.WeaponAttachments;
using Content.Shared._Misfits.WeaponAttachments.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Effects;
using Content.Shared.Physics;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client._Misfits.WeaponAttachments;

/// <summary>
/// Draws a continuous red beam from the held weapon's laser sight toward the cursor
/// while the local player is in combat mode and the sight is toggled on.
/// Visual only: the server is not involved; other players still see the red point light.
/// </summary>
public sealed class WeaponLaserSightBeamSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private const string BeamProto = "HitscanEffect";

    private EntityUid? _beam;
    private EntityUid? _impact;

    public override void FrameUpdate(float frameTime)
    {
        if (!TryGetActiveLaser(out var user, out var laser, out var from))
        {
            RemoveBeam();
            return;
        }

        if (!_inputManager.MouseScreenPosition.IsValid)
        {
            RemoveBeam();
            return;
        }

        var mouse = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace || mouse.MapId != from.MapId)
        {
            RemoveBeam();
            return;
        }

        var direction = mouse.Position - from.Position;
        var fullLength = direction.Length();
        if (fullLength < 0.05f)
        {
            RemoveBeam();
            return;
        }

        var normalized = direction / fullLength;
        var limit = MathF.Min(fullLength, laser.MaxLength);
        var rayDistance = RaycastDistance(from, normalized, user, laser.MaxLength);
        var contact = rayDistance < limit;
        var length = contact ? rayDistance : limit;
        if (length < 0.25f)
        {
            RemoveBeam();
            return;
        }

        UpdateBeam(from, normalized, length, laser);
        UpdateImpact(from, normalized, length, contact, laser);
    }

    public override void Shutdown()
    {
        RemoveBeam();
        RemoveImpact();
        base.Shutdown();
    }

    private bool TryGetActiveLaser(out EntityUid user, out WeaponLaserSightComponent laser, out MapCoordinates from)
    {
        laser = default!;
        from = default;
        user = default;

        if (_player.LocalEntity is not { } local || !_combatMode.IsInCombatMode())
            return false;

        if (!_gun.TryGetGun(local, out var gunUid, out _))
            return false;

        if (!_itemSlots.TryGetSlot(gunUid, WeaponAttachmentSlots.Bottom, out var slot) || slot.Item is not { } item)
            return false;

        if (!TryComp(item, out WeaponLaserSightComponent? sight) || !sight.Enabled)
            return false;

        // Prefer the gun's position; fall back to the holder if something is off.
        var gunXform = Transform(gunUid);
        from = new MapCoordinates(gunXform.WorldPosition, gunXform.MapID);
        if (from.MapId == MapId.Nullspace)
        {
            var userXform = Transform(local);
            from = new MapCoordinates(userXform.WorldPosition, userXform.MapID);
            if (from.MapId == MapId.Nullspace)
                return false;
        }

        user = local;
        laser = sight;
        return true;
    }

    private float RaycastDistance(MapCoordinates from, Vector2 direction, EntityUid source, float maxLength)
    {
        var ray = new CollisionRay(
            from.Position,
            direction,
            (int) (CollisionGroup.Opaque | CollisionGroup.BulletImpassable));

        var results = _physics.IntersectRayWithPredicate(
            from.MapId,
            ray,
            source,
            static (hit, ignore) => hit == ignore,
            maxLength,
            false);

        foreach (var result in results)
            return result.Distance;

        return maxLength;
    }

    private void UpdateBeam(MapCoordinates from, Vector2 direction, float length, WeaponLaserSightComponent laser)
    {
        if (_beam is not { } beam || Deleted(beam))
        {
            beam = Spawn(BeamProto, from);
            RemComp<TimedDespawnComponent>(beam);
            RemComp<EffectVisualsComponent>(beam);
            RemComp<AnimationPlayerComponent>(beam);
            ConfigureBeamSprite(beam, laser);
            _beam = beam;
        }

        var mid = from.Position + direction * (length / 2f);
        var mapEntity = _map.GetMap(from.MapId);
        _xform.SetCoordinates(beam, new EntityCoordinates(mapEntity, mid));
        _xform.SetWorldRotationNoLerp(beam, direction.ToAngle());

        if (TryComp<SpriteComponent>(beam, out var sprite))
            sprite.Scale = new Vector2(length, laser.BeamWidth);
    }

    private void ConfigureBeamSprite(EntityUid beam, WeaponLaserSightComponent laser)
    {
        if (!TryComp<SpriteComponent>(beam, out var sprite))
            return;

        var rsi = new SpriteSpecifier.Rsi(new ResPath(laser.BeamSprite), laser.BeamState);

        sprite[EffectLayers.Unshaded].AutoAnimated = false;
        sprite.LayerSetSprite(EffectLayers.Unshaded, rsi);
        sprite.LayerSetState(EffectLayers.Unshaded, rsi.RsiState);
        sprite.LayerSetColor(EffectLayers.Unshaded, laser.BeamColor);
        sprite[EffectLayers.Unshaded].Visible = true;
        sprite.Scale = new Vector2(1f, laser.BeamWidth);
    }

    private void RemoveBeam()
    {
        if (_beam is not { } beam)
            return;

        _beam = null;
        if (!Deleted(beam))
            Del(beam);

        RemoveImpact();
    }

    /// <summary>
    /// Shows a laser hit flash at the point where the aiming beam is stopped by an obstacle.
    /// Mirrors the vanilla hitscan impact effect so the laser sight reads like the real deal.
    /// </summary>
    private void UpdateImpact(MapCoordinates from, Vector2 direction, float length, bool contact, WeaponLaserSightComponent laser)
    {
        if (!contact)
        {
            RemoveImpact();
            return;
        }

        if (_impact is not { } impact || Deleted(impact))
        {
            impact = Spawn(BeamProto, from);
            RemComp<EffectVisualsComponent>(impact);
            ConfigureImpactSprite(impact, laser);
            _impact = impact;
        }

        var end = from.Position + direction * length;
        var mapEntity = _map.GetMap(from.MapId);
        _xform.SetCoordinates(impact, new EntityCoordinates(mapEntity, end));
        _xform.SetWorldRotationNoLerp(impact, direction.ToAngle().FlipPositive());

        if (TryComp<TimedDespawnComponent>(impact, out var despawn))
            despawn.Lifetime = laser.ImpactLifetime;
    }

    private void ConfigureImpactSprite(EntityUid impact, WeaponLaserSightComponent laser)
    {
        if (!TryComp<SpriteComponent>(impact, out var sprite))
            return;

        var rsi = new SpriteSpecifier.Rsi(new ResPath(laser.BeamSprite), laser.ImpactState);

        sprite.LayerSetSprite(EffectLayers.Unshaded, rsi);
        sprite.LayerSetState(EffectLayers.Unshaded, rsi.RsiState);
        sprite.LayerSetColor(EffectLayers.Unshaded, laser.BeamColor);
        sprite[EffectLayers.Unshaded].Visible = true;
    }

    private void RemoveImpact()
    {
        if (_impact is not { } impact)
            return;

        _impact = null;
        if (!Deleted(impact))
            Del(impact);
    }
}
