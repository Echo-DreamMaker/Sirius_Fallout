using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Maths;

namespace Content.Shared._N14.SuperMutant;

/// <summary>
/// Implements super mutant firearm handling. Kept as a standalone shared system (NOT a base class of
/// any server-side system) so that all firearm subscriptions run on BOTH client and server. This is
/// required because both <see cref="ShotAttemptedEvent"/> and <see cref="GunRefreshModifiersEvent"/>
/// are client-predicted: ignoring this would let the client still create a projectile and fail to
/// render the spread penalty.
///
/// Rules:
/// <list type="bullet">
/// <item>Fitting two-handed firearms (with <see cref="SuperMutantFittingComponent"/>) are wielded
/// one-handed with no accuracy penalty.</item>
/// <item>Non-fitting two-handed firearms (WieldableComponent present) are wielded one-handed but
/// receive a large spread penalty (<see cref="SuperMutantComponent.UnfittingSpreadMultiplier"/>).</item>
/// <item>One-handed firearms (no WieldableComponent, no fitting marker) cannot be fired at all.</item>
/// </list>
/// </summary>
public sealed partial class SharedSuperMutantSystem : EntitySystem
{
    // NOTE: Broadcast (not directed on GunComponent) because Robust allows only ONE directed
    // subscriber per (component, event) pair, and that pair is already taken by GunHandlingModifierSystem.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunRefreshModifiersEvent>(OnGunRefreshModifiers,
            after: [typeof(WieldableSystem)]);

        // Block firing one-handed firearms. ShotAttemptedEvent is also raised on the user (the
        // super mutant), so this directed subscription fires there and prevents the projectile.
        SubscribeLocalEvent<SuperMutantComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnGunRefreshModifiers(ref GunRefreshModifiersEvent args)
    {
        var holder = Transform(args.Gun.Owner).ParentUid;

        if (!TryComp<SuperMutantComponent>(holder, out var sm))
            return;

        // Penalty applies to any non-fitting two-handed firearm held by a super mutant. We deliberately
        // do NOT gate on wield/wielded state: the auto-wield in the server-side OnGotHand is not
        // guaranteed to match on the client (and would flicker), whereas the modifier refresh is shared.
        // This mirrors the original Oni behavior which applied the penalty to every held gun.
        if (!HasComp<WieldableComponent>(args.Gun.Owner))
            return;

        // Fitting two-handed firearms get no penalty.
        if (HasComp<SuperMutantFittingComponent>(args.Gun.Owner))
            return;

        var mult = sm.UnfittingSpreadMultiplier;
        args.MinAngle = new Angle((double)args.MinAngle * mult);
        args.MaxAngle = new Angle((double)args.MaxAngle * mult);
        args.AngleIncrease = new Angle((double)args.AngleIncrease * mult);
        args.CameraRecoilScalar *= mult;
    }

    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private void OnShotAttempted(EntityUid uid, SuperMutantComponent component, ref ShotAttemptedEvent args)
    {
        var gun = args.Used.Owner;

        // One-handed firearms (not wieldable) can't be used by super mutants at all.
        if (HasComp<WieldableComponent>(gun))
            return;

        // Fitting heavy/energy firearms bypass the block entirely.
        if (HasComp<SuperMutantFittingComponent>(gun))
            return;

        args.Cancel();
        _popup.PopupClient(
            Loc.GetString("supermutant-cannot-fire-onehanded", ("item", gun)),
            gun, args.User, PopupType.SmallCaution);
    }
}
