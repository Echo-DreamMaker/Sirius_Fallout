using Content.Server.Power.Components;
using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.Weapons.Ranged.Systems;

/// <summary>
/// Server-side half of <see cref="GaussChargeCellComponent"/>: turns the cell's battery charge into
/// a whole-shot count for the examine line.
/// </summary>
/// <remarks>
/// Deliberately not in shared code. <c>BatteryComponent</c> is server-only and unnetworked, so
/// there is nothing for a client to read; examine text is server-authoritative anyway.
/// </remarks>
public sealed class GaussChargeCellSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GaussChargeCellComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, GaussChargeCellComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Not a cell after all (component inherited onto something without a battery). Stay quiet
        // rather than reporting a bogus zero.
        if (!TryComp<BatteryComponent>(uid, out var battery))
            return;

        var cost = component.ChargeCost;
        var shots = cost <= 0f ? 0 : (int) MathF.Floor(battery.CurrentCharge / cost);
        var max = cost <= 0f ? 0 : (int) MathF.Floor(battery.MaxCharge / cost);

        var percent = max > 0 ? shots * 100f / max : 0f;

        args.PushMarkup(Loc.GetString("gauss-charge-cell-examine",
            ("color", "yellow"),
            ("charge", shots),
            ("max", max),
            ("percent", (int) percent)));
    }
}