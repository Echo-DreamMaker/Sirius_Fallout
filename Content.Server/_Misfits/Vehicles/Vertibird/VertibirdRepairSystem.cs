// #Misfits Add - Welding repair for the vertibird airframe, plus turret restocking.
// Trained crew (Lancers and pilots) work faster thanks to VertibirdCrewComponent.
using Content.Server.Administration.Logs;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared.Charges.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;

namespace Content.Server._Misfits.Vehicles.Vertibird;

public sealed class VertibirdRepairSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private VertibirdSystem _vertibird = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VertibirdRepairComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<VertibirdRepairComponent, VertibirdRepairFinishedEvent>(OnRepairFinished);
    }

    private void OnInteractUsing(Entity<VertibirdRepairComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Restocking takes priority: a crewman holding a belt of rounds is reloading,
        // not welding, and the restock item is not a welding tool anyway.
        if (HasComp<VertibirdTurretRestockComponent>(args.Used))
        {
            args.Handled = TryRestock(ent.Owner, args.Used, args.User);
            return;
        }

        if (!TryComp<DamageableComponent>(ent, out var damageable) || damageable.TotalDamage == 0)
        {
            _popup.PopupEntity(Loc.GetString("vertibird-repair-undamaged"), ent, args.User);
            return;
        }

        var delay = ent.Comp.DoAfterDelay;
        if (TryComp<VertibirdCrewComponent>(args.User, out var crew))
            delay *= crew.RepairSpeedMultiplier;

        args.Handled = _tool.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            delay,
            ent.Comp.QualityNeeded,
            new VertibirdRepairFinishedEvent(),
            ent.Comp.FuelCost);
    }

    private void OnRepairFinished(Entity<VertibirdRepairComponent> ent, ref VertibirdRepairFinishedEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<DamageableComponent>(ent, out var damageable) || damageable.TotalDamage == 0)
            return;

        if (ent.Comp.Damage != null)
        {
            var changed = _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage, true, false, origin: args.User);
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} repaired {ToPrettyString(ent.Owner):target} by {changed?.GetTotal()}");
        }
        else
        {
            _damageable.SetAllDamage(ent.Owner, damageable, 0);
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} repaired {ToPrettyString(ent.Owner):target} back to full health");
        }

        _popup.PopupEntity(Loc.GetString("vertibird-repair-success"), ent, args.User);
    }

    private bool TryRestock(EntityUid vertibird, EntityUid restock, EntityUid user)
    {
        if (!TryComp<LimitedChargesComponent>(vertibird, out var magazine))
            return false;

        if (magazine.Charges >= magazine.MaxCharges)
        {
            _popup.PopupEntity(Loc.GetString("vertibird-restock-full"), vertibird, user);
            return true;
        }

        if (!_vertibird.TryRestockTurret(vertibird, restock))
            return false;

        _popup.PopupEntity(
            Loc.GetString("vertibird-restock-success", ("rounds", magazine.Charges)),
            vertibird,
            user);

        // A belt that gave up its last round is spent.
        if (TryComp<LimitedChargesComponent>(restock, out var supply) && supply.Charges <= 0)
            QueueDel(restock);

        return true;
    }
}
