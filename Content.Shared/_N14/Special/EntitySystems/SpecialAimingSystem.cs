using System.Numerics;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared._N14.Special.Components;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._N14.Special.EntitySystems;

/// <summary>
/// Perception-driven aim mode. While aiming, the character's viewport zooms in
/// (range scales with Perception) and ranged spread shrinks (accuracy scales with
/// Perception). Aim mode automatically deactivates when the character switches hands.
/// </summary>
public sealed class SpecialAimingSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecialAimableComponent, SpecialAimToggleActionEvent>(OnAimToggle);
        SubscribeLocalEvent<HandSelectedEvent>(OnHandSelected);
        SubscribeLocalEvent<GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnAimToggle(EntityUid uid, SpecialAimableComponent component, SpecialAimToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (component.Aiming)
            DisableAim(uid, component);
        else
            EnableAim(uid, component);
    }

    private void EnableAim(EntityUid uid, SpecialAimableComponent component)
    {
        if (!_hands.TryGetActiveItem(uid, out _))
            return;

        component.Aiming = true;
        Dirty(uid, component);
        _actions.SetToggled(component.ToggleActionEntity, true);

        var perception = _special.GetEffective(uid, SpecialStat.Perception);
        var zoom = 1f + SharedSpecialSystem.GetCurvedEffectDelta(perception) * GetZoomMultiplierPerPoint();
        _contentEye.SetZoom(uid, Vector2.One * MathF.Max(1f, zoom), true);

        RefreshGun(uid);
    }

    private void DisableAim(EntityUid uid, SpecialAimableComponent component)
    {
        if (!component.Aiming)
            return;

        component.Aiming = false;
        Dirty(uid, component);
        _actions.SetToggled(component.ToggleActionEntity, false);
        _contentEye.ResetZoom(uid);
        RefreshGun(uid);
    }

    private void OnGunRefreshModifiers(ref GunRefreshModifiersEvent args)
    {
        var holder = Transform(args.Gun.Owner).ParentUid;

        if (!TryComp<SpecialAimableComponent>(holder, out var aimable) || !aimable.Aiming)
            return;

        if (!TryComp<SpecialComponent>(holder, out var special))
            return;

        var perception = _special.GetEffective(holder, SpecialStat.Perception, special);
        var spreadPerPoint = GetSpreadMultiplierPerPoint();

        // Sniper rifles get a greater accuracy bonus while aiming (stronger spread reduction).
        if (_tags.HasTag(args.Gun.Owner, "Sniper"))
            spreadPerPoint = GetSpreadSniperMultiplierPerPoint();

        var keepFraction = 1f - SharedSpecialSystem.GetCurvedEffectDelta(perception) * spreadPerPoint;
        keepFraction = Math.Clamp(keepFraction, 0.1f, 1f);

        args.MinAngle = new Angle((double) args.MinAngle * keepFraction);
        args.MaxAngle = new Angle((double) args.MaxAngle * keepFraction);
        args.AngleIncrease = new Angle((double) args.AngleIncrease * keepFraction);
        args.AngleDecay = new Angle((double) args.AngleDecay * keepFraction);
    }

    private void OnHandSelected(HandSelectedEvent args)
    {
        if (TryComp<SpecialAimableComponent>(args.User, out var aimable) && aimable.Aiming)
            DisableAim(args.User, aimable);
    }

    private void RefreshGun(EntityUid uid)
    {
        if (_hands.TryGetActiveItem(uid, out var item))
            _gun.RefreshModifiers(item.Value);
    }

    private float GetZoomMultiplierPerPoint()
    {
        return _special.GetTuning().PerceptionAimZoomMultiplierPerPoint;
    }

    private float GetSpreadMultiplierPerPoint()
    {
        return _special.GetTuning().PerceptionAimSpreadMultiplierPerPoint;
    }

    private float GetSpreadSniperMultiplierPerPoint()
    {
        return _special.GetTuning().PerceptionAimSpreadSniperMultiplierPerPoint;
    }
}
