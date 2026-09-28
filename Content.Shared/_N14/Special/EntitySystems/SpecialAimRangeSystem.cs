using System.Numerics;
using Content.Shared._Misfits.Scope;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Wielding;
using Content.Shared._NC.CameraFollow.Components;
using Content.Shared._NC.FollowDistance.Components;
using Content.Shared._N14.Special.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._N14.Special.EntitySystems;

/// <summary>
/// Computes how far the camera is allowed to drift toward the cursor while aim mode
/// is active. The reach depends on the held weapon's profile (pistols drift less than
/// rifles, rifles less than snipers), the character's Perception (moderate influence),
/// whether the weapon has a scope mounted (strong influence) and whether a two-handed
/// gun is actually wielded (one-hand hold cuts the reach). Aiming without a gun still
/// works but with a greatly reduced range. Reach changes are recomputed live while aim
/// mode is held for the same weapon (wield/unwield, scope, stats); switching weapons
/// cancels aim mode entirely (see SpecialAimingSystem). The resulting
/// <see cref="CameraFollowComponent.MaxDistance"/> is networked so the client derives
/// the per-frame chase from it.
/// </summary>
public sealed class SpecialAimRangeSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Swapping the aimed weapon entirely cancels aim mode (see SpecialAimingSystem), so
        // the recomputes below only fire while the SAME weapon is held: they adjust the
        // reach live — without leaving aim mode — for two-handing changes, attachments,
        // and stat changes on the current firearm.
        SubscribeLocalEvent<MisfitsItemWieldedEvent>(OnWeaponWield);
        SubscribeLocalEvent<ItemUnwieldedEvent>(OnWeaponUnwield);

        SubscribeLocalEvent<SpecialChangedEvent>(OnSpecialChanged);
        SubscribeLocalEvent<GunRefreshModifiersEvent>(OnGunRefresh);
    }

    /// <summary>
    /// Turns the aim camera on and computes the range for the current loadout.
    /// </summary>
    public void EnableAimCamera(EntityUid uid)
    {
        var cam = EnsureComp<CameraFollowComponent>(uid);
        cam.Enabled = true;
        cam.Offset = Vector2.Zero;
        RecalculateRange(uid, cam);
        Dirty(uid, cam);
    }

    /// <summary>
    /// Turns the aim camera off and removes the follow component so the eye offset
    /// pipeline falls back to its non-aim behaviour.
    /// </summary>
    public void DisableAimCamera(EntityUid uid)
    {
        if (TryComp<CameraFollowComponent>(uid, out var cam))
        {
            cam.Enabled = false;
            cam.Offset = Vector2.Zero;
            Dirty(uid, cam);
        }

        _eye.SetOffset(uid, Vector2.Zero);
        RemCompDeferred<CameraFollowComponent>(uid);
    }

    private void OnSpecialChanged(ref SpecialChangedEvent args)
    {
        RecalculateRangeIfAiming(args.ChangedEntity);
    }

    private void OnGunRefresh(ref GunRefreshModifiersEvent args)
    {
        RecalculateRangeIfAiming(Transform(args.Gun.Owner).ParentUid);
    }

    private void OnWeaponWield(MisfitsItemWieldedEvent args)
    {
        RecalculateRangeIfAiming(args.User);
    }

    private void OnWeaponUnwield(ItemUnwieldedEvent args)
    {
        if (args.User is { } user)
            RecalculateRangeIfAiming(user);
    }

    private void RecalculateRangeIfAiming(EntityUid uid)
    {
        if (TryComp<SpecialAimableComponent>(uid, out var aimable) && aimable.Aiming)
            RecalculateRange(uid);
    }

    private void RecalculateRange(EntityUid uid)
    {
        if (!TryComp<SpecialAimableComponent>(uid, out var aimable) || !aimable.Aiming)
            return;
        if (!TryComp<CameraFollowComponent>(uid, out var cam))
            return;

        RecalculateRange(uid, cam);
    }

    private void RecalculateRange(EntityUid uid, CameraFollowComponent cam)
    {
        var tuning = _special.GetTuning();

        var perception = _special.GetEffective(uid, SpecialStat.Perception);
        var perceptionFactor = 1f + SharedSpecialSystem.GetCurvedEffectDelta(perception) * tuning.AimCameraRangeMultiplierPerPoint;

        var range = tuning.AimCameraNoWeaponRange * perceptionFactor;

        if (_hands.TryGetActiveItem(uid, out var held) && TryComp<GunComponent>(held.Value, out _))
        {
            var profile = 1f;

            // Weapon profile: the existing per-class FollowDistance.BackStrength values
            // (pistols ~6, rifles ~4, snipers ~3) translate into camera reach, so larger
            // guns drift the view further away from the character.
            if (TryComp<FollowDistanceComponent>(held.Value, out var followDist) && followDist.BackStrength > 0)
                profile = tuning.AimCameraWeaponProfileScale / followDist.BackStrength;

            if (_tags.HasTag(held.Value, "Sniper"))
                profile *= tuning.AimCameraSniperMultiplier;

            if (HasScopeMounted(held.Value))
                profile *= tuning.AimCameraScopeMultiplier;

            // Two-handed guns scale with how the character actually holds them: aiming
            // one-handed cuts the reach significantly, while actually wielding (both hands
            // on it) boosts it beyond the one-hand baseline. The change applies live while
            // aim mode is held (wield/unwield recomputes the range).
            if (TryComp<WieldableComponent>(held.Value, out var wieldable))
                profile *= wieldable.Wielded ? tuning.AimCameraWieldMultiplier : tuning.AimCameraOneHandMultiplier;

            range = tuning.AimCameraWeaponBaseRange * profile * perceptionFactor;
        }

        range = Math.Max(range, tuning.AimCameraMinRange);

        cam.DefaultBackStrength = cam.BackStrength = tuning.AimCameraChaseRate;
        cam.DefaultMaxDistance = new Vector2(range, range);
        cam.MaxDistance = new Vector2(range, range);
        Dirty(uid, cam);
    }

    /// <summary>
    /// True if any item slotted into the held gun carries a scope (bolted-on or integrated).
    /// </summary>
    private bool HasScopeMounted(EntityUid gun)
    {
        if (!TryComp<ItemSlotsComponent>(gun, out var slots))
            return false;

        foreach (var slot in slots.Slots.Values)
        {
            if (slot.Item is { } item && TryComp<ScopeComponent>(item, out _))
                return true;
        }

        return false;
    }
}