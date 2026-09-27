using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared._N14.Special.Components;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._N14.Special.EntitySystems;

/// <summary>
/// Perception-driven aim mode. While aiming, the camera drifts toward the cursor
/// (reach scales with Perception, the held weapon's profile, and mounted scopes) and
/// ranged spread shrinks (accuracy scales with Perception). Aim mode is tied to the
/// weapon that was active when it was toggled on: switching weapons (dropping, hand
/// swaps, picking up) cancels it so a long-range sightline can never leak onto another
/// weapon. While the same weapon is held, changes to its wield state or attachments
/// adjust the reach live without leaving aim mode (see SpecialAimRangeSystem). Aim also
/// ends on toggle off, sleep or death.
/// </summary>
public sealed class SpecialAimingSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SpecialAimRangeSystem _aimRange = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecialAimableComponent, SpecialAimToggleActionEvent>(OnAimToggle);
        SubscribeLocalEvent<SpecialAimableComponent, SleepStateChangedEvent>(OnSleepChanged);
        SubscribeLocalEvent<SpecialAimableComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GunRefreshModifiersEvent>(OnGunRefreshModifiers);

        // Aim mode is locked to the weapon it was started with. Switching the held item
        // (hand swap, drop, pickup) cancels it so a sightline tuned for a long gun never
        // carries over to another weapon.
        SubscribeLocalEvent<HandSelectedEvent>(OnActiveWeaponChanged);
        SubscribeLocalEvent<HandDeselectedEvent>(OnActiveWeaponChanged);
        SubscribeLocalEvent<GotEquippedHandEvent>(OnActiveWeaponChanged);
        SubscribeLocalEvent<GotUnequippedHandEvent>(OnActiveWeaponChanged);
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
        if (component.Aiming)
            return;

        component.Aiming = true;
        component.AimedWeapon = _hands.TryGetActiveItem(uid, out var item) ? item : null;
        Dirty(uid, component);
        _actions.SetToggled(component.ToggleActionEntity, true);

        _aimRange.EnableAimCamera(uid);

        RefreshGun(uid);
    }

    private void DisableAim(EntityUid uid, SpecialAimableComponent component)
    {
        if (!component.Aiming)
            return;

        component.Aiming = false;
        component.AimedWeapon = null;
        Dirty(uid, component);
        _actions.SetToggled(component.ToggleActionEntity, false);

        _aimRange.DisableAimCamera(uid);

        RefreshGun(uid);
    }

    private void OnSleepChanged(EntityUid uid, SpecialAimableComponent component, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            DisableAim(uid, component);
    }

    private void OnMobStateChanged(EntityUid uid, SpecialAimableComponent component, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            DisableAim(uid, component);
    }

    private void OnActiveWeaponChanged(HandSelectedEvent args)
    {
        CancelAimIfWeaponChanged(args.User);
    }

    private void OnActiveWeaponChanged(HandDeselectedEvent args)
    {
        CancelAimIfWeaponChanged(args.User);
    }

    private void OnActiveWeaponChanged(GotEquippedHandEvent args)
    {
        CancelAimIfWeaponChanged(args.User);
    }

    private void OnActiveWeaponChanged(GotUnequippedHandEvent args)
    {
        CancelAimIfWeaponChanged(args.User);
    }

    /// <summary>
    /// Cancels aim mode if the weapon it was started with is no longer active in hand.
    /// Equipping items into other hands (while the aim weapon stays active) does not
    /// cancel aim.
    /// </summary>
    private void CancelAimIfWeaponChanged(EntityUid uid)
    {
        if (!TryComp<SpecialAimableComponent>(uid, out var component) || !component.Aiming)
            return;

        var held = _hands.TryGetActiveItem(uid, out var item) ? item : null;
        if (held != component.AimedWeapon)
            DisableAim(uid, component);
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

    private void RefreshGun(EntityUid uid)
    {
        if (_hands.TryGetActiveItem(uid, out var item) && TryComp<GunComponent>(item.Value, out _))
            _gun.RefreshModifiers(item.Value);
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