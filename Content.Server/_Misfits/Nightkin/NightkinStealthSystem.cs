// #Misfits Add - Server registration for Nightkin passive Stealth Boy implant behavior.
using Content.Shared._Misfits.Nightkin;
using Content.Shared._Misfits.StealthBoy;
using Content.Server._Misfits.StealthBoy;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Nightkin;

public sealed class NightkinStealthSystem : SharedNightkinStealthSystem
{
    [Dependency] private readonly StealthBoySystem _stealthBoy = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan PassiveDuration = TimeSpan.FromDays(3650);

    protected override void ActivateNightkinStealth(EntityUid uid, NightkinPassiveStealthComponent component)
    {
        _stealthBoy.ActivateStealth(
            uid,
            PassiveDuration,
            component.Visibility,
            component.FadeInTime,
            component.FadeOutTime,
            component.ActivateMessage,
            component.DeactivateMessage,
            component.StillVisibility,
            component.WalkVisibility);

        // don't sting them the instant they cloak, wait out a full interval first
        component.NextCloakPoison = _timing.CurTime + component.CloakPoisonInterval;
        Dirty(uid, component);
    }

    // #Misfits Add - running the implant leaks toxins into the host.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NightkinPassiveStealthComponent, StealthBoyActiveComponent>();
        while (query.MoveNext(out var uid, out var nightkin, out _))
        {
            if (nightkin.CloakPoison <= 0f || now < nightkin.NextCloakPoison)
                continue;

            nightkin.NextCloakPoison = now + nightkin.CloakPoisonInterval;
            Dirty(uid, nightkin);

            var damage = new DamageSpecifier();
            damage.DamageDict["Poison"] = FixedPoint2.New(nightkin.CloakPoison);
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false);
        }
    }

    protected override void DeactivateNightkinStealth(
        EntityUid uid,
        NightkinPassiveStealthComponent component,
        StealthBoyActiveComponent active)
    {
        active.ReappearMessage = component.DeactivateMessage;
        Dirty(uid, active);
        _stealthBoy.TryBeginFadeOut(uid, active);
    }
}
