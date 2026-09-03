using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared._N14.Special.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._N14.Special.EntitySystems;

/// <summary>
/// Enforces a minimum Strength requirement to wield two-handed weapons.
/// A weapon carrying <see cref="StrengthRequirementComponent"/> cannot be wielded
/// while the wielder's effective Strength is below the requirement, and is
/// automatically unwielded if Strength drops while already wielded.
/// </summary>
public sealed class StrengthRequirementSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly WieldableSystem _wieldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrengthRequirementComponent, BeforeWieldEvent>(OnBeforeWield);
        SubscribeLocalEvent<StrengthRequirementComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<StrengthRequirementComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<StrengthRequirementComponent, ShotAttemptedEvent>(OnAttemptShoot);
        SubscribeLocalEvent<SpecialChangedEvent>(OnSpecialChanged);
    }

    private void OnBeforeWield(EntityUid uid, StrengthRequirementComponent component, BeforeWieldEvent args)
    {
        var required = component.RequiredStrength;

        if (_special.GetEffective(args.User, SpecialStat.Strength) >= required)
            return;

        args.Cancel();
        _popup.PopupClient(
            Loc.GetString("strength-requirement-too-weak",
                ("item", uid),
                ("required", required)),
            uid,
            args.User);
    }

    private void OnAttemptMelee(EntityUid uid, StrengthRequirementComponent component, ref AttemptMeleeEvent args)
    {
        if (_special.GetEffective(args.PlayerUid, SpecialStat.Strength) >= component.RequiredStrength)
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("strength-requirement-cannot-apply",
            ("item", uid),
            ("required", component.RequiredStrength));
    }

    private void OnAttemptShoot(EntityUid uid, StrengthRequirementComponent component, ref ShotAttemptedEvent args)
    {
        if (_special.GetEffective(args.User, SpecialStat.Strength) >= component.RequiredStrength)
            return;

        args.Cancel();
        _popup.PopupClient(
            Loc.GetString("strength-requirement-cannot-apply",
                ("item", uid),
                ("required", component.RequiredStrength)),
            uid,
            args.User);
    }

    private void OnExamined(EntityUid uid, StrengthRequirementComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("strength-requirement-examine", ("required", component.RequiredStrength)));
    }

    private void OnSpecialChanged(ref SpecialChangedEvent args)
    {
        if (!TryComp<WieldableComponent>(args.ChangedEntity, out _))
            return;

        var strength = _special.GetEffective(args.ChangedEntity, SpecialStat.Strength);

        foreach (var held in _hands.EnumerateHeld(args.ChangedEntity))
        {
            if (!TryComp<WieldableComponent>(held, out var wieldable) ||
                !wieldable.Wielded ||
                !TryComp<StrengthRequirementComponent>(held, out var requirement) ||
                strength >= requirement.RequiredStrength)
            {
                continue;
            }

            _wieldable.TryUnwield(held, wieldable, args.ChangedEntity);
            _popup.PopupClient(
                Loc.GetString("strength-requirement-unwield",
                    ("item", held),
                    ("required", requirement.RequiredStrength)),
                held,
                args.ChangedEntity);
        }
    }
}
