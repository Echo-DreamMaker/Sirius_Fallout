// #Misfits Add - Repair half of the weapon assembly station.
//
// The station mends durability that the weapon already carries: the receiver
// (the bare frame a modular weapon is stripped to, or the body of a weapon that
// has no parts), and the four working slots. A modular weapon has to be taken
// apart first, because until then its parts live inside the weapon rather than
// in the station's slots - which is what RepairedInPlace decides.
//
// One press does not repair anything by itself: it runs the same one second
// stroke over and over until the part is whole, each stroke restoring a fixed
// chunk of durability and burning a little fuel. The clamp stays on and the
// station stays at its working power draw for the whole run, so letting go or
// walking away (which cancels the do-after) stops it halfway, with the work done
// so far kept.
using Content.Shared.DoAfter;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared._Sirius.WeaponAssemblyStation;
using Content.Shared._Sirius.Weapons.Durability;
using Robust.Shared.Audio;

namespace Content.Server._Sirius.WeaponAssemblyStation;

public sealed partial class WeaponAssemblyStationSystem
{
    private static readonly SoundPathSpecifier RepairSound = new("/Audio/Items/welder.ogg");

    [Dependency] private readonly SharedWeaponDurabilitySystem _durability = default!;

    /// <summary>
    /// UI entry point for the repair buttons: 0 mends the body, 1-4 the working
    /// slots. Starts the stroke loop, which then carries itself on.
    /// </summary>
    private void TryRepair(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid user, int step)
    {
        if (comp.IsBusy || !comp.Powered)
            return;

        if (!TryResolveRepairTarget(uid, comp, user, step, out var target))
            return;

        if (!NeedsRepair(target))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-nothing"), uid, user);
            return;
        }

        if (!HasFuel(uid, comp, comp.RepairFuelCostPerStep))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-no-fuel"), uid, user);
            return;
        }

        comp.IsBusy = true;

        // The weapon is clamped for as long as the repair runs, whatever the
        // bolt state was beforehand.
        _itemSlots.SetLock(uid, WeaponAssemblyStationComponent.MainSlotId, true);
        UpdatePowerLoad(uid, comp);
        UpdateUi(uid, comp);

        _audio.PlayPvs(RepairSound, uid);
        StartRepairStep(uid, comp, user, step);
    }

    private void StartRepairStep(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid user, int step)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user,
            comp.RepairStepDelay,
            new WeaponRepairDoAfterEvent { Step = step },
            uid,
            target: uid)
        {
            BlockDuplicate = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 2f,
        });
    }

    // #Misfits Fix - component scoped handler: the do-after raises its event
    // directly on the station, so the component is handed to us.
    private void OnRepairDoAfter(EntityUid uid, WeaponAssemblyStationComponent comp, WeaponRepairDoAfterEvent args)
    {
        if (args.Cancelled || !comp.Powered)
        {
            EndOperation(uid, comp);
            return;
        }

        // Re-resolve every stroke: between two of them the player may have taken
        // the part back out or swapped the weapon.
        if (!TryResolveRepairTarget(uid, comp, args.User, args.Step, out var target))
        {
            EndOperation(uid, comp);
            return;
        }

        if (!HasFuel(uid, comp, comp.RepairFuelCostPerStep))
        {
            if (args.User is { } fuelUser)
                _popup.PopupEntity(Loc.GetString("misfits-weapon-station-no-fuel"), uid, fuelUser);

            EndOperation(uid, comp);
            return;
        }

        if (!ApplyRepairStep(target, args.Step, comp.RepairStepRestore))
        {
            // Nothing left to mend (or it stopped being repairable): stop rather
            // than spin the do-after forever.
            EndOperation(uid, comp);
            return;
        }

        TryConsumeFuel(uid, comp, comp.RepairFuelCostPerStep);

        // #Misfits Add - push the drained fuel (and the mended part) to the UI
        // after every stroke so the gauge tracks a long repair run.
        UpdateUi(uid, comp);

        // Keep going while there is wear left; the next stroke waits another
        // RepairStepDelay with the station still busy.
        if (NeedsRepair(target))
        {
            StartRepairStep(uid, comp, args.User, args.Step);
            return;
        }

        EndOperation(uid, comp);

        if (args.User is { } popupUser)
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-repaired"), uid, popupUser);
    }

    /// <summary>
    ///     Hands the clamp back to the player's own bolt state and drops the
    ///     station back to its idle power draw.
    /// </summary>
    private void EndOperation(EntityUid uid, WeaponAssemblyStationComponent comp)
    {
        comp.IsBusy = false;
        _itemSlots.SetLock(uid, WeaponAssemblyStationComponent.MainSlotId, comp.IsBolted);
        UpdatePowerLoad(uid, comp);
        UpdateUi(uid, comp);
    }

    /// <summary>
    /// Looks the repair up and complains to the user when it cannot run; the
    /// do-after loop uses <see cref="ResolveRepairTarget"/> instead, silently.
    /// </summary>
    private bool TryResolveRepairTarget(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid? user, int step,
        out EntityUid target)
    {
        if (!ResolveRepairTarget(comp, step, out target))
        {
            if (user is { } noTargetUser)
            {
                _popup.PopupEntity(Loc.GetString(step == 0
                    ? "misfits-weapon-station-need-weapon"
                    : "misfits-weapon-station-repair-empty"), uid, noTargetUser);
            }

            return false;
        }

        if (IsRepairable(target, step))
            return true;

        // A modular weapon that is still built has to be taken apart first; a
        // weapon with no durability of its own cannot be worked on at all.
        var reason = "misfits-weapon-station-cannot-repair";
        if (step == 0 &&
            TryComp<WeaponDurabilityComponent>(target, out var durability) &&
            durability.Modular &&
            !IsStripped(target))
        {
            reason = "misfits-weapon-station-need-stripped";
        }

        if (user is { } userUid)
            _popup.PopupEntity(Loc.GetString(reason), uid, userUid);

        target = EntityUid.Invalid;
        return false;
    }

    /// <summary>
    /// Which entity a repair step works on: the entity in the main slot for the
    /// body, the part seated in one of the working slots otherwise.
    /// </summary>
    private static bool ResolveRepairTarget(WeaponAssemblyStationComponent comp, int step, out EntityUid target)
    {
        target = step == 0
            ? comp.MainContainer.ContainedEntity ?? EntityUid.Invalid
            : (StationSlotContainer(comp, StationPartSlots[step - 1]).ContainedEntity ?? EntityUid.Invalid);

        return target.IsValid();
    }

    /// <summary>
    /// Whether this target is something the station may mend. A modular weapon
    /// has to be stripped to its frame first, and a weapon without durability
    /// (a "forbidden" prototype) cannot be repaired at all.
    /// </summary>
    private bool IsRepairable(EntityUid target, int step)
    {
        if (step == 0)
        {
            return TryComp<WeaponDurabilityComponent>(target, out var durability) &&
                   (!durability.Modular || IsStripped(target));
        }

        return HasComp<WeaponPartDurabilityComponent>(target);
    }

    /// <summary>
    /// True once a weapon has been taken apart: its body is a bare frame rather
    /// than a gun or a melee weapon any more.
    /// </summary>
    private bool IsStripped(EntityUid body)
    {
        return !HasComp<GunComponent>(body) && !HasComp<MeleeWeaponComponent>(body);
    }

    private bool ApplyRepairStep(EntityUid target, int step, float amount)
    {
        if (step == 0)
            return TryComp<WeaponDurabilityComponent>(target, out var durability) &&
                   _durability.TryRepairBody(target, durability, amount);

        return TryComp<WeaponPartDurabilityComponent>(target, out var part) &&
               _durability.TryRepairPart(part, amount);
    }

    private bool NeedsRepair(EntityUid target)
    {
        return TryComp<WeaponPartDurabilityComponent>(target, out var part)
            ? part.Deferred > 0f || part.Current < part.Max
            : TryComp<WeaponDurabilityComponent>(target, out var durability) &&
              (durability.Deferred > 0f || durability.FrameCurrent < durability.FrameMax);
    }

    /// <summary>
    /// Which of the five repair buttons the station can offer right now, in the
    /// same order as the UI (body first, then the four working slots).
    /// </summary>
    private bool[] RepairAvailability(WeaponAssemblyStationComponent comp, out bool bodyIsWeapon)
    {
        var available = new bool[StationPartSlots.Length + 1];
        bodyIsWeapon = false;

        if (comp.MainContainer.ContainedEntity is { } body &&
            TryComp<WeaponDurabilityComponent>(body, out var durability))
        {
            // A weapon with parts of its own says "repair frame" and only once
            // it is stripped; a weapon without them says "repair weapon" and can
            // be mended while it is still whole.
            bodyIsWeapon = !durability.Modular;
            available[0] = (!durability.Modular || IsStripped(body)) && NeedsRepair(body);
        }

        for (var i = 0; i < StationPartSlots.Length; i++)
        {
            available[i + 1] = StationSlotContainer(comp, StationPartSlots[i]).ContainedEntity is { } part &&
                               NeedsRepair(part);
        }

        return available;
    }
}
