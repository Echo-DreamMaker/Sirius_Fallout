// #Misfits Change /Fix/: Pause self-recharging guns while they are attached to dead mobs.
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class RechargeBasicEntityAmmoSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RechargeBasicEntityAmmoComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<RechargeBasicEntityAmmoComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<RechargeBasicEntityAmmoComponent, BasicEntityAmmoProviderComponent>();

        while (query.MoveNext(out var uid, out var recharge, out var ammo))
        {
            if (ammo.Count is null || ammo.Count == ammo.Capacity || recharge.NextCharge == null)
                continue;

            if (IsAttachedToDeadMob(uid))
            {
                if (recharge.NextCharge < _timing.CurTime)
                {
                    recharge.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(recharge.RechargeCooldown);
                    Dirty(uid, recharge);
                }

                continue;
            }

            if (recharge.NextCharge > _timing.CurTime)
                continue;

            if (_gun.UpdateBasicEntityAmmoCount(uid, ammo.Count.Value + 1, ammo))
            {
                // We don't predict this because occasionally on client it may not play.
                // PlayPredicted will still be predicted on the client.
                if (_netManager.IsServer)
                    _audio.PlayPvs(recharge.RechargeSound, uid);
            }

            if (ammo.Count == ammo.Capacity)
            {
                recharge.NextCharge = null;
                Dirty(uid, recharge);
                continue;
            }

            recharge.NextCharge = recharge.NextCharge.Value + TimeSpan.FromSeconds(recharge.RechargeCooldown);
            Dirty(uid, recharge);
        }

        // #Misfits Change: prototype-fed ballistic providers are what creatures such as the
        // bloatfly (and bows, crossbows...) carry. The query above joins on
        // BasicEntityAmmoProviderComponent, which they do not have, so their magazine used to be
        // permanently finite - and NPCCombatSystem.Ranged keeps such a mob aiming forever instead
        // of falling back to melee, because "has a recharge component" reads as "still recharging".
        var ballQuery = EntityQueryEnumerator<RechargeBasicEntityAmmoComponent, BallisticAmmoProviderComponent>();

        while (ballQuery.MoveNext(out var uid, out var recharge, out var ball))
        {
            // An entity carrying both providers is driven by the loop above.
            if (HasComp<BasicEntityAmmoProviderComponent>(uid))
                continue;

            if (ball.Proto == null || recharge.NextCharge == null)
                continue;

            if (IsAttachedToDeadMob(uid))
            {
                if (recharge.NextCharge < _timing.CurTime)
                {
                    recharge.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(recharge.RechargeCooldown);
                    Dirty(uid, recharge);
                }

                continue;
            }

            if (recharge.NextCharge > _timing.CurTime)
                continue;

            // Full: park the timer until a round is actually spent. RechargeBasicEntityAmmoComponent.Reset
            // (called from OnBallisticTakeAmmo) is what puts a live timer back.
            if (ball.AmmoCount >= ball.Capacity)
            {
                recharge.NextCharge = null;
                Dirty(uid, recharge);
                continue;
            }

            if (_gun.TryAddUnspawnedAmmo(uid, ball))
            {
                // We don't predict this because occasionally on client it may not play.
                // PlayPredicted will still be predicted on the client.
                if (_netManager.IsServer)
                    _audio.PlayPvs(recharge.RechargeSound, uid);
            }

            if (ball.AmmoCount >= ball.Capacity)
            {
                recharge.NextCharge = null;
                Dirty(uid, recharge);
                continue;
            }

            recharge.NextCharge = recharge.NextCharge.Value + TimeSpan.FromSeconds(recharge.RechargeCooldown);
            Dirty(uid, recharge);
        }
    }

    private bool IsAttachedToDeadMob(EntityUid uid)
    {
        if (TryComp<MobStateComponent>(uid, out var selfMobState) && _mobState.IsDead(uid, selfMobState))
            return true;

        var parent = Transform(uid).ParentUid;

        while (parent.IsValid())
        {
            if (TryComp<MobStateComponent>(parent, out var mobState) && _mobState.IsDead(parent, mobState))
                return true;

            var parentXform = Transform(parent);
            if (parentXform.ParentUid == parent)
                break;

            parent = parentXform.ParentUid;
        }

        return false;
    }

    private void OnInit(EntityUid uid, RechargeBasicEntityAmmoComponent component, MapInitEvent args)
    {
        component.NextCharge = _timing.CurTime;
        Dirty(uid, component);
    }

    private void OnExamined(EntityUid uid, RechargeBasicEntityAmmoComponent component, ExaminedEvent args)
    {
        // #Misfits Change: accept ballistic providers too, otherwise a prototype-fed weapon always
        // examined as "full" even while its magazine was empty and recharging.
        var full = true;

        if (TryComp<BasicEntityAmmoProviderComponent>(uid, out var ammo))
            full = ammo.Count == ammo.Capacity;
        else if (TryComp<BallisticAmmoProviderComponent>(uid, out var ball))
            full = ball.Proto == null || ball.AmmoCount >= ball.Capacity;

        if (full || component.NextCharge == null)
        {
            args.PushMarkup(Loc.GetString("recharge-basic-entity-ammo-full"));
            return;
        }

        var timeLeft = component.NextCharge + _metadata.GetPauseTime(uid) - _timing.CurTime;
        args.PushMarkup(Loc.GetString("recharge-basic-entity-ammo-can-recharge", ("seconds", Math.Round(timeLeft.Value.TotalSeconds, 1))));
    }

    public void Reset(EntityUid uid, RechargeBasicEntityAmmoComponent? recharge = null)
    {
        if (!Resolve(uid, ref recharge, false))
            return;

        if (recharge.NextCharge == null || recharge.NextCharge < _timing.CurTime)
        {
            recharge.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(recharge.RechargeCooldown);
            Dirty(uid, recharge);
        }
    }
}
