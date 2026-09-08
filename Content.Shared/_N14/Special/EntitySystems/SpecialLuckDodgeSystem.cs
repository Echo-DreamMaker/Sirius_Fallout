using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared._Misfits.Special.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._N14.Special.EntitySystems;

/// <summary>
/// Grants Luck-based chance to dodge incoming bullets and hitscan shots.
/// A successful dodge turns the shot into a clean miss, driven by the curved
/// Luck stat delta scaled by <see cref="SpecialTuningPrototype.LuckDodgeChancePerPoint"/>.
/// </summary>
public sealed class SpecialLuckDodgeSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecialComponent, ProjectileReflectAttemptEvent>(OnProjectileReflect);
        SubscribeLocalEvent<SpecialComponent, HitScanReflectAttemptEvent>(OnHitScanReflect);
    }

    private void OnProjectileReflect(EntityUid uid, SpecialComponent component, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled ||
            !TryDodge(uid))
        {
            return;
        }

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("special-lucky-evasion"), uid, PopupType.Medium);
    }

    private void OnHitScanReflect(EntityUid uid, SpecialComponent component, ref HitScanReflectAttemptEvent args)
    {
        if (args.Cancelled ||
            args.Reflected ||
            !TryDodge(uid))
        {
            return;
        }

        // Cancel the shot server-side so it becomes a clean miss.
        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("special-lucky-evasion"), uid, PopupType.Medium);
    }

    private bool TryDodge(EntityUid uid)
    {
        if (!_net.IsServer || !_special.UsesSpecialStats(uid))
            return false;

        var tuning = _special.GetTuning();
        var delta = _special.GetCurvedEffectDelta(uid, SpecialStat.Luck);
        var chance = Math.Clamp(delta * tuning.LuckDodgeChancePerPoint, 0f, 0.5f);

        return _random.Prob(chance);
    }
}
