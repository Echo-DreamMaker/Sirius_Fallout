using Content.Server.Chat.Systems;
using Content.Server.CombatMode.Disarm;
using Content.Server._Misfits.Movement;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions.Events;
using Content.Shared.Administration.Components;
using Content.Shared.CombatMode;
using Content.Shared.Contests;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Speech.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;
using Content.Shared.Chat;

namespace Content.Server.Weapons.Melee;

public sealed class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    // #Misfits Change /Fix/: give empty-hand shoves a short, visible displacement instead of only stamina feedback.
    private const float ShoveImpulse = 6f;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly ServerMisfitsLagCompensationSystem _lag = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly ContestsSystem _contests = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly Content.Shared._Misfits.Special.SharedSpecialSystem _special = default!;

    private readonly HashSet<EntityUid> _heavyAttackCandidates = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeSpeechComponent, MeleeHitEvent>(OnSpeechHit);
        SubscribeLocalEvent<MeleeWeaponComponent, DamageExamineEvent>(OnMeleeExamineDamage, after: [typeof(GunSystem)]);
    }

    private void OnMeleeExamineDamage(EntityUid uid, MeleeWeaponComponent component, ref DamageExamineEvent args)
    {
        if (component.Hidden)
            return;

        var damageSpec = GetDamage(uid, args.User, component);
        if (damageSpec.Empty)
            return;

        if (!component.DisableClick)
            _damageExamine.AddDamageExamine(args.Message, damageSpec, Loc.GetString("damage-melee"));

        if (!component.DisableHeavy)
        {
            if (damageSpec * component.HeavyDamageBaseModifier != damageSpec)
                _damageExamine.AddDamageExamine(args.Message, damageSpec * component.HeavyDamageBaseModifier, Loc.GetString("damage-melee-heavy"));

            if (component.HeavyStaminaCost != 0)
            {
                var staminaCostMarkup = FormattedMessage.FromMarkupOrThrow(
                    Loc.GetString("damage-stamina-cost",
                    ("type", Loc.GetString("damage-melee-heavy")), ("cost", Math.Round(component.HeavyStaminaCost, 2).ToString("0.##"))));
                args.Message.PushNewline();
                args.Message.AddMessage(staminaCostMarkup);
            }
        }
    }

    protected override bool ArcRaySuccessful(EntityUid targetUid, Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId,
        EntityUid ignore, ICommonSession? session, GameTick? lastRealTick = null)
    {
        // Originally the client didn't predict damage effects so you'd intuit some level of how far
        // in the future you'd need to predict, but then there was a lot of complaining like "why would you add artifical delay" as if ping is a choice.
        // Now damage effects are predicted but for wide attacks it differs significantly from client and server so your game could be lying to you on hits.
        // This isn't fair in the slightest because it makes ping a huge advantage and this would be a hidden system.
        // Now the client tells us what they hit and we validate if it's plausible.

        // Even if the client is sending entities they shouldn't be able to hit:
        // A) Wide-damage is split anyway
        // B) We run the same validation we do for click attacks.

        EntityCoordinates targetCoords;
        Angle targetLocalAngle;

        if (session != null)
        {
            (targetCoords, targetLocalAngle) = lastRealTick is { } tick
                ? _lag.GetCoordinatesAngle(targetUid, tick - 1)
                : _lag.GetCoordinatesAngle(targetUid, session);
            if (!Interaction.InRangeUnobstructed(ignore, targetUid, targetCoords, targetLocalAngle, range + WideArcTolerance))
                return false;
        }
        else
        {
            var xform = Transform(targetUid);
            targetCoords = xform.Coordinates;
            targetLocalAngle = xform.LocalRotation;
            if (!Interaction.InRangeUnobstructed(ignore, targetUid, range + WideArcTolerance))
                return false;
        }

        var targetMapPos = TransformSystem.ToMapCoordinates(targetCoords);
        if (targetMapPos.MapId == mapId)
        {
            var toTarget = targetMapPos.Position - position;
            if (toTarget.LengthSquared() > 0.001f)
            {
                var diff = Angle.ShortestDistance(angle, toTarget.ToWorldAngle());
                if (Math.Abs((double) diff) > (double) arcWidth / 2.0 + WideArcTolerance &&
                    !TargetBoundsOverlapArc(targetUid, targetCoords, targetLocalAngle, position, angle, arcWidth, range + WideArcTolerance))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool TargetBoundsOverlapArc(
        EntityUid targetUid,
        EntityCoordinates targetCoords,
        Angle targetLocalAngle,
        Vector2 origin,
        Angle angle,
        Angle arcWidth,
        float range)
    {
        if (!TryComp<FixturesComponent>(targetUid, out var fixtures))
            return false;

        var targetMap = _transform.ToMapCoordinates(targetCoords);
        if (targetMap.MapId == MapId.Nullspace)
            return false;

        var worldAngle = _transform.GetWorldRotation(targetCoords.EntityId) + targetLocalAngle;
        var transform = new Transform(targetMap.Position, worldAngle);
        var initialized = false;
        var bounds = new Box2(targetMap.Position, targetMap.Position);

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard ||
                (fixture.CollisionLayer & (int) (CollisionGroup.MobMask | CollisionGroup.Opaque)) == 0)
            {
                continue;
            }

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var aabb = fixture.Shape.ComputeAABB(transform, i);
                bounds = initialized ? bounds.Union(aabb) : aabb;
                initialized = true;
            }
        }

        if (!initialized)
            return false;

        // [Changed by MisfitsCrew/Operator] Center-point arc checks reject edge hits on wide
        // swings; accept when the target's physical bounds overlap the validated swing sector.
        var halfArc = arcWidth / 2.0 + WideArcTolerance;
        foreach (var point in GetBoxTestPoints(bounds, origin))
        {
            if (PointInArc(point, origin, angle, halfArc, range))
                return true;
        }

        var centerEnd = origin + angle.ToWorldVec() * range;
        var leftEnd = origin + new Angle(angle - arcWidth / 2.0).ToWorldVec() * range;
        var rightEnd = origin + new Angle(angle + arcWidth / 2.0).ToWorldVec() * range;
        return SegmentIntersectsBox(origin, centerEnd, bounds) ||
               SegmentIntersectsBox(origin, leftEnd, bounds) ||
               SegmentIntersectsBox(origin, rightEnd, bounds);
    }

    private static IEnumerable<Vector2> GetBoxTestPoints(Box2 bounds, Vector2 origin)
    {
        yield return bounds.BottomLeft;
        yield return bounds.BottomRight;
        yield return bounds.TopLeft;
        yield return bounds.TopRight;
        yield return new Vector2(
            Math.Clamp(origin.X, bounds.Left, bounds.Right),
            Math.Clamp(origin.Y, bounds.Bottom, bounds.Top));
    }

    private static bool PointInArc(Vector2 point, Vector2 origin, Angle angle, Angle halfArc, float range)
    {
        var delta = point - origin;
        if (delta.LengthSquared() > range * range || delta.LengthSquared() <= 0.001f)
            return false;

        return Math.Abs((double) Angle.ShortestDistance(angle, delta.ToWorldAngle())) <= (double) halfArc;
    }

    private static bool SegmentIntersectsBox(Vector2 start, Vector2 end, Box2 box)
    {
        if (box.Contains(start))
            return true;

        var direction = end - start;
        var min = 0f;
        var max = 1f;

        if (!ClipAxis(start.X, direction.X, box.Left, box.Right, ref min, ref max) ||
            !ClipAxis(start.Y, direction.Y, box.Bottom, box.Top, ref min, ref max))
        {
            return false;
        }

        return max >= min;
    }

    private static bool ClipAxis(float start, float direction, float minBound, float maxBound, ref float min, ref float max)
    {
        if (Math.Abs(direction) < 0.0001f)
            return start >= minBound && start <= maxBound;

        var inv = 1f / direction;
        var enter = (minBound - start) * inv;
        var exit = (maxBound - start) * inv;

        if (enter > exit)
            (enter, exit) = (exit, enter);

        min = Math.Max(min, enter);
        max = Math.Min(max, exit);
        return max >= min;
    }

    protected override void AddHeavyAttackCandidates(
        List<EntityUid> entities,
        EntityUid user,
        EntityUid meleeUid,
        MeleeWeaponComponent component,
        Vector2 position,
        Angle angle,
        float range,
        MapId mapId,
        ICommonSession? session,
        GameTick? lastRealTick)
    {
        if (session == null || entities.Count >= component.MaxTargets)
            return;

        // [Changed by MisfitsCrew/Operator] Recover server-authoritative wide-swing targets
        // from lag-compensated positions when client prediction missed the entity list.
        // [Changed by MisfitsCrew/Operator] Include the configured lag-compensation margin in
        // the candidate lookup; final acceptance still goes through ArcRaySuccessful below.
        var lookupRange = range + _lag.MarginTiles;
        var bounds = Box2.CenteredAround(position, new Vector2(lookupRange * 2f, lookupRange * 2f));
        _heavyAttackCandidates.Clear();
        _lookup.GetEntitiesIntersecting(mapId, bounds, _heavyAttackCandidates, LookupFlags.Dynamic);

        foreach (var candidate in _heavyAttackCandidates)
        {
            if (entities.Contains(candidate) ||
                IsUserRelatedAttackEntity(user, candidate) ||
                !HasComp<DamageableComponent>(candidate))
            {
                continue;
            }

            if (!ArcRaySuccessful(candidate, position, angle, component.Angle, range, mapId, user, session, lastRealTick))
                continue;

            entities.Add(candidate);
            if (entities.Count >= component.MaxTargets)
                break;
        }
    }

    protected override bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoDisarm(user, ev, meleeUid, component, session))
            return false;

        if (!TryComp<CombatModeComponent>(user, out var combatMode) ||
            combatMode.CanDisarm != true)
        {
            return false;
        }

        var target = GetEntity(ev.Target!.Value);

        if (_mobState.IsIncapacitated(target))
        {
            return false;
        }

        if (!TryComp<HandsComponent>(target, out var targetHandsComponent))
        {
            if (!TryComp<StatusEffectsComponent>(target, out var status) || !status.AllowedEffects.Contains("KnockedDown"))
                return false;
        }

        if (!InRange(user, target, component.Range, session, ev.LastRealTick))
        {
            return false;
        }

        EntityUid? inTargetHand = null;

        if (targetHandsComponent?.ActiveHand is { IsEmpty: false })
        {
            inTargetHand = targetHandsComponent.ActiveHand.HeldEntity!.Value;
        }

        Interaction.DoContactInteraction(user, target);

        var attemptEvent = new DisarmAttemptEvent(target, user, inTargetHand);

        if (inTargetHand != null)
        {
            RaiseLocalEvent(inTargetHand.Value, attemptEvent);
        }

        RaiseLocalEvent(target, attemptEvent);

        if (attemptEvent.Cancelled)
            return false;

        var chance = CalculateDisarmChance(user, target, inTargetHand, combatMode);
        if (!_random.Prob(chance))
        {
            // Don't play a sound as the swing is already predicted.
            // Also don't play popups because most disarms will miss.
            return false;
        }

        var staminaDamage = (TryComp<ShovingComponent>(user, out var shoving) ? shoving.StaminaDamage : ShovingComponent.DefaultStaminaDamage)
            * Math.Clamp(chance, 0f, 1f);

        var eventArgs = new DisarmedEvent { Target = target, Source = user, PushProbability = chance, StaminaDamage = staminaDamage };
        RaiseLocalEvent(target, eventArgs);

        // #Misfits Change /Fix/: if nothing else handled an empty-hand disarm, convert it into an actual shove impulse.
        if (!eventArgs.Handled && inTargetHand == null)
            eventArgs.Handled = TryShoveTarget(user, target);

        if (!eventArgs.Handled)
            return false;

        var emoteKey = inTargetHand == null ? "disarm-action-shove-emote" : "disarm-action-emote";
        var emoteMessage = Loc.GetString(emoteKey, ("targetName", Identity.Entity(target, EntityManager)));
        _chat.TrySendInGameICMessage(user, emoteMessage, InGameICChatType.Emote, ChatTransmitRange.Normal, ignoreActionBlocker: true);

        _audio.PlayPvs(combatMode.DisarmSuccessSound, user, AudioParams.Default.WithVariation(0.025f).WithVolume(5f));
        AdminLogger.Add(LogType.DisarmedAction, $"{ToPrettyString(user):user} used disarm on {ToPrettyString(target):target}");

        return true;
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session, GameTick? lastRealTick = null)
    {
        EntityCoordinates targetCoordinates;
        Angle targetLocalAngle;

        if (session is { } pSession)
        {
            (targetCoordinates, targetLocalAngle) = lastRealTick is { } tick
                ? _lag.GetCoordinatesAngle(target, tick - 1)
                : _lag.GetCoordinatesAngle(target, pSession);
            return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range);
        }

        return Interaction.InRangeUnobstructed(user, target, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        var filter = Filter.Pvs(targetXform.Coordinates, entityMan: EntityManager).RemoveWhereAttachedEntity(o => o == user);
        _color.RaiseEffect(Color.Red, targets, filter);
    }

    private bool TryShoveTarget(EntityUid user, EntityUid target)
    {
        if (!TryComp<PhysicsComponent>(target, out var targetPhysics))
            return false;

        if ((targetPhysics.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) == 0)
            return false;

        var shoveDirection = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(user);

        if (shoveDirection.LengthSquared() <= 0.001f)
            shoveDirection = Transform(user).LocalRotation.ToWorldVec();

        // #Misfits Change /Fix/: scale impulse by target mass so heavy mobs still move perceptibly on a valid shove.
        _physics.ApplyLinearImpulse(target, shoveDirection.Normalized() * (ShoveImpulse * targetPhysics.Mass), body: targetPhysics);
        return true;
    }

    private float CalculateDisarmChance(EntityUid disarmer, EntityUid disarmed, EntityUid? inTargetHand, CombatModeComponent disarmerComp)
    {
        if (HasComp<DisarmProneComponent>(disarmer))
            return 1.0f;

        if (HasComp<DisarmProneComponent>(disarmed))
            return 0.0f;

        var chance = 1 - disarmerComp.BaseDisarmFailChance;

        if (inTargetHand != null && TryComp<DisarmMalusComponent>(inTargetHand, out var malus))
            chance -= malus.Malus;

        if (TryComp<ShovingComponent>(disarmer, out var shoving))
            chance += shoving.DisarmBonus;

        // #Misfits Change /Add/: strong characters resist being disarmed.
        if (_special.UsesSpecialStats(disarmed))
        {
            var protection = _special.GetStrengthDisarmProtection(disarmed);
            chance *= 1f - protection;
        }

        return Math.Clamp(chance
                        * _contests.MassContest(disarmer, disarmed, false, 2f)
                        * _contests.StaminaContest(disarmer, disarmed, false, 0.5f)
                        * _contests.HealthContest(disarmer, disarmed, false, 1f),
                        0f, 1f);
    }

    public override void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true)
    {
        Filter filter;

        if (predicted)
        {
            filter = Filter.PvsExcept(user, entityManager: EntityManager);
        }
        else
        {
            filter = Filter.Pvs(user, entityManager: EntityManager);
        }

        RaiseNetworkEvent(new MeleeLungeEvent(GetNetEntity(user), GetNetEntity(weapon), angle, localPos, animation), filter);
    }

    private void OnSpeechHit(EntityUid owner, MeleeSpeechComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit ||
        !args.HitEntities.Any())
        {
            return;
        }

        if (comp.Battlecry != null)//If the battlecry is set to empty, doesn't speak
        {
            _chat.TrySendInGameICMessage(args.User, comp.Battlecry, InGameICChatType.Speak, true, true, checkRadioPrefix: false);  //Speech that isn't sent to chat or adminlogs
        }

    }
}
