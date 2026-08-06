using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Movement.Events;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;


namespace Content.Shared._Misfits.MeleeCharge;


public sealed class MeleeChargeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MeleeChargeComponent, UpdateCanMoveEvent>(OnMoveAttempt);
        SubscribeLocalEvent<MeleeChargeComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MeleeChargeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MeleeChargeComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<MeleeChargeEvent>(OnDash);
    }

    private void OnDash(MeleeChargeEvent ev)
    {
        PerformDash(ev.Performer, ev.Target, ev.Speed, ev.Range);
        _actions.StartUseDelay(ev.Action);
    }

    private void OnShutdown(Entity<MeleeChargeComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnInit(Entity<MeleeChargeComponent> ent, ref ComponentInit args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnLand(Entity<MeleeChargeComponent> ent, ref LandEvent args)
    {
        RemCompDeferred<MeleeChargeComponent>(ent);
    }

    private void OnMoveAttempt(Entity<MeleeChargeComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        //Cant move while dashing
        args.Cancel();
    }

    public void PerformDash(EntityUid ent, EntityCoordinates targetPosition, float speed = 10f, float maxDistance = 3.5f)
    {
        EnsureComp<MeleeChargeComponent>(ent, out var dash);
        // _audio.PlayPredicted(dash.DashSound, ent, ent);

        var entMapPos = _transform.ToMapCoordinates(Transform(ent).Coordinates);
        var targetMapPos = _transform.ToMapCoordinates(targetPosition);

        var distance = Vector2.Distance(entMapPos.Position, targetMapPos.Position);

        if (distance > maxDistance)
        {
            var direction = (targetMapPos.Position - entMapPos.Position).Normalized();
            var clampedTarget = entMapPos.Position + direction * maxDistance;
            targetMapPos = new MapCoordinates(clampedTarget, entMapPos.MapId);
        }


        var finalTarget = _transform.ToCoordinates(targetMapPos);

        _throwing.TryThrow(ent, finalTarget, speed, null, 0f, 10, true, false, false, false, false);
    }

}
