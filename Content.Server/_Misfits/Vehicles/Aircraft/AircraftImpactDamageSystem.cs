using System.Numerics;
using Content.Shared._Misfits.Vehicles.Aircraft;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared._MultiZ.Core.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server._Misfits.Vehicles.Destruction;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Vehicles.Aircraft;

/// <summary>
/// Applies configurable hull damage when a flying aircraft strikes a hard
/// obstacle and removes the portion of its drift driving it into that obstacle.
/// </summary>
public sealed class AircraftImpactDamageSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DestructionMomentumSystem _momentum = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AircraftImpactDamageComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<AircraftImpactDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnStartCollide(Entity<AircraftImpactDamageComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OurFixture.Hard ||
            !args.OtherFixture.Hard ||
            !TryComp<VertibirdComponent>(ent, out var aircraft) ||
            aircraft.State != VertibirdFlightState.Cruising)
        {
            return;
        }

        if (ent.Comp.GroundLevelOnly)
        {
            var mapUid = Transform(ent).MapUid;
            if (mapUid == null || !TryComp<MZMapComponent>(mapUid.Value, out var map) || map.Depth != 0)
                return;
        }

        if (_timing.CurTime < ent.Comp.LastImpactAt + TimeSpan.FromSeconds(ent.Comp.DamageCooldown))
            return;

        var relativeVelocity = args.OurBody.LinearVelocity - args.OtherBody.LinearVelocity;
        var normal = args.WorldNormal;
        var closingSpeed = normal.LengthSquared() > 0f
            ? MathF.Abs(Vector2.Dot(relativeVelocity, Vector2.Normalize(normal)))
            : relativeVelocity.Length();

        if (closingSpeed < ent.Comp.MinimumSpeed)
            return;

        ent.Comp.LastImpactAt = _timing.CurTime;

        // #Misfits Add - Ported from CMU. Before treating the obstacle as a solid wall,
        // check whether the craft's momentum can smash through it. If so, damage the
        // obstacle, spend the break cost, and let the craft keep flying.
        if (ent.Comp.ObstacleDamageMultiplier > 0f &&
            _momentum.TryGetBreakCost(args.OtherEntity, closingSpeed, ent.Comp.ObstacleDamageMultiplier, out var breakCost))
        {
            var rawDamage = breakCost * breakCost * ent.Comp.ObstacleDamageMultiplier;
            var obstacleDamage = new DamageSpecifier();
            obstacleDamage.DamageDict["Blunt"] = FixedPoint2.New(rawDamage);
            _damageable.TryChangeDamage(args.OtherEntity, obstacleDamage, origin: ent.Owner);

            var remainingSpeed = DestructionMomentumSystem.GetRemainingSpeed(closingSpeed, breakCost);
            if (aircraft.DriftVelocity.LengthSquared() > 0f)
                aircraft.DriftVelocity = Vector2.Normalize(aircraft.DriftVelocity) * remainingSpeed;

            aircraft.HeldInputs = VertibirdControlInput.None;
            _physics.SetLinearVelocity(ent, aircraft.DriftVelocity, body: args.OurBody);

            if (ent.Comp.ImpactSound != null)
                _audio.PlayPvs(ent.Comp.ImpactSound, ent);

            return;
        }

        var excessSpeed = closingSpeed - ent.Comp.MinimumSpeed;
        var damageScale = 1f + excessSpeed * ent.Comp.SpeedDamageFactor;
        _damageable.TryChangeDamage(ent, ent.Comp.Damage * damageScale, origin: args.OtherEntity);

        // Remove movement into the obstacle while preserving a reduced amount
        // of tangential drift. Without this, VertibirdSystem reapplies its old
        // DriftVelocity every tick and the aircraft continuously drives at the wall.
        var collisionNormal = normal.LengthSquared() > 0f ? Vector2.Normalize(normal) : Vector2.Zero;
        var normalVelocity = Vector2.Dot(aircraft.DriftVelocity, collisionNormal);
        aircraft.DriftVelocity = (aircraft.DriftVelocity - collisionNormal * normalVelocity) *
            Math.Clamp(ent.Comp.VelocityRetention, 0f, 1f);
        aircraft.HeldInputs = VertibirdControlInput.None;
        _physics.SetLinearVelocity(ent, aircraft.DriftVelocity, body: args.OurBody);

        if (ent.Comp.ImpactSound != null)
            _audio.PlayPvs(ent.Comp.ImpactSound, ent);

        if (aircraft.Pilot is { } pilot && Exists(pilot))
            _popup.PopupEntity(Loc.GetString(ent.Comp.PilotWarning), ent, pilot, PopupType.LargeCaution);
    }

    private void OnDamageChanged(Entity<AircraftImpactDamageComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Destroyed ||
            !TryComp<DamageableComponent>(ent, out var damageable) ||
            !damageable.Damage.DamageDict.TryGetValue("Structural", out var structuralDamage) ||
            structuralDamage.Float() < ent.Comp.MaxIntegrity)
        {
            return;
        }

        ent.Comp.Destroyed = true;

        // Structural failure is deliberately absolute: every boarded mob is
        // placed into Dead before being released into the explosion.
        if (TryComp<VertibirdComponent>(ent, out var aircraft))
        {
            var occupants = aircraft.SeatOccupants
                .Where(occupant => occupant != null)
                .Select(occupant => occupant!.Value)
                .Distinct()
                .ToArray();

            foreach (var occupant in occupants)
            {
                if (TryComp<MobStateComponent>(occupant, out var mobState))
                    _mobState.ChangeMobState(occupant, MobState.Dead, mobState, ent.Owner);

                _buckle.Unbuckle((occupant, null), null);
            }
        }

        _explosion.QueueExplosion(
            ent.Owner,
            ent.Comp.ExplosionType,
            ent.Comp.ExplosionTotalIntensity,
            ent.Comp.ExplosionSlope,
            ent.Comp.ExplosionMaxTileIntensity);

        QueueDel(ent);
    }
}
