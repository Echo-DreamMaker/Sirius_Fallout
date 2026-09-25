using Content.Shared.Projectiles;
using Robust.Shared.Spawners;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._Misfits.Weapons;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    private const string TracerTrailEffect = "N14TracerTrailEffect";
    private static readonly TimeSpan TracerTrailInterval = TimeSpan.FromSeconds(0.03f);

    [Dependency] private readonly AnimationPlayerSystem _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<ImpactEffectEvent>(OnProjectileImpact);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<TracerVisualComponent, ProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var projectile, out var physics, out var xform))
        {
            if (projectile.DamagedEntity || physics.LinearVelocity.LengthSquared() < 1f)
                continue;

            if (comp.NextEmit > curTime)
                continue;

            Spawn(TracerTrailEffect, xform.Coordinates);
            comp.NextEmit = curTime + TracerTrailInterval;
        }
    }

    private void OnProjectileImpact(ImpactEffectEvent ev)
    {
        var coords = GetCoordinates(ev.Coordinates);

        if (Deleted(coords.EntityId))
            return;

        var ent = Spawn(ev.Prototype, coords);

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            sprite[EffectLayers.Unshaded].AutoAnimated = false;
            sprite.LayerMapTryGet(EffectLayers.Unshaded, out var layer);
            var state = sprite.LayerGetState(layer);
            var lifetime = 0.5f;

            if (TryComp<TimedDespawnComponent>(ent, out var despawn))
                lifetime = despawn.Lifetime;

            var anim = new Animation()
            {
                Length = TimeSpan.FromSeconds(lifetime),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick()
                    {
                        LayerKey = EffectLayers.Unshaded,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state.Name, 0f),
                        }
                    }
                }
            };

            _player.Play(ent, anim, "impact-effect");
        }
    }
}
