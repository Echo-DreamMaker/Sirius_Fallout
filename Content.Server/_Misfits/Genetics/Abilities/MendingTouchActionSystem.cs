// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared._Misfits.Genetics.Abilities;

namespace Content.Server._Misfits.Genetics.Abilities;

/// <summary>
/// Handles <see cref="MendingTouchActionComponent"/>: transfers a portion of a target's
/// physical injuries onto the user, or heals the user themselves at the cost of blood.
/// Lives on the server because the self-heal needs the bloodstream.
/// </summary>
public sealed class MendingTouchActionSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MendingTouchActionComponent, MendingTouchActionEvent>(OnMend);
    }

    private void OnMend(Entity<MendingTouchActionComponent> ent, ref MendingTouchActionEvent args)
    {
        var user = args.Performer;
        var target = args.Target;

        if (user == target)
        {
            MendSelf(ent, user, ref args);
            return;
        }

        if (!TryComp<DamageableComponent>(target, out var damage))
            return;

        // Pull up to Amount of the target's physical injuries into a specifier.
        var transfer = CollectDamage(ent, damage);

        var ident = Identity.Entity(target, EntityManager);
        if (transfer.DamageDict.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("MutationMendingTouch-nothing", ("target", ident)), user, user);
            return;
        }

        // Heal the target, take those same wounds onto yourself.
        _damageable.TryChangeDamage(target, transfer * -1f, ignoreResistances: true, interruptsDoAfters: false);
        _damageable.TryChangeDamage(user, transfer, ignoreResistances: true, interruptsDoAfters: false);

        _actions.StartUseDelay(ent.Owner);
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("MutationMendingTouch-mended", ("target", ident)), user, user);
    }

    /// <summary>
    /// Self-mending: heal your own physical injuries, paying with a chunk of your blood.
    /// </summary>
    private void MendSelf(Entity<MendingTouchActionComponent> ent, EntityUid user, ref MendingTouchActionEvent args)
    {
        if (!TryComp<DamageableComponent>(user, out var damage) ||
            !TryComp<BloodstreamComponent>(user, out var bloodstream))
            return;

        var heal = CollectDamage(ent, damage);
        if (heal.DamageDict.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("MutationMendingTouch-self-nothing"), user, user);
            return;
        }

        _damageable.TryChangeDamage(user, heal * -1f, ignoreResistances: true, interruptsDoAfters: false);

        // the wounds have to go somewhere: drain a fraction of max blood volume
        var cost = bloodstream.BloodMaxVolume * ent.Comp.SelfBloodFraction;
        _blood.TryModifyBloodLevel(user, -cost, bloodstream);

        _actions.StartUseDelay(ent.Owner);
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("MutationMendingTouch-self"), user, user, PopupType.MediumCaution);
    }

    /// <summary>
    /// Collects up to Amount of the allowed damage types from a damageable into a specifier.
    /// </summary>
    private static DamageSpecifier CollectDamage(Entity<MendingTouchActionComponent> ent, DamageableComponent damage)
    {
        var collected = new DamageSpecifier();
        var remaining = FixedPoint2.New(ent.Comp.Amount);
        foreach (var (type, amount) in damage.Damage.DamageDict)
        {
            if (remaining <= FixedPoint2.Zero)
                break;
            if (amount <= FixedPoint2.Zero || !ent.Comp.Types.Contains(type))
                continue;

            var take = amount < remaining ? amount : remaining;
            collected.DamageDict[type] = take;
            remaining -= take;
        }
        return collected;
    }
}
