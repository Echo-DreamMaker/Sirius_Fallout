// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared._Misfits.Genetics.Abilities;
using Content.Shared._Misfits.Genetics.Mutations;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed partial class InjectChemicalsActionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
        => SubscribeLocalEvent<InjectChemicalsActionComponent, InjectChemicalsActionEvent>(OnAction);

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<InjectChemicalsActionComponent>();
        var now = _timing.CurTime;
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextComedown is {} next && now >= next)
                Comedown((uid, comp));
        }
    }

    private void OnAction(Entity<InjectChemicalsActionComponent> ent, ref InjectChemicalsActionEvent args)
    {
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Main.Popup), args.Performer, args.Performer);
        ent.Comp.NextComedown = _timing.CurTime + ent.Comp.ComedownDelay;
        Inject(args.Performer, ent.Comp.Main.Reagents, ent.Comp.Main.Quantity);
    }

    private void Comedown(Entity<InjectChemicalsActionComponent> ent)
    {
        if (_mutation.GetActionMutation(ent)?.Comp?.Target is not {} target)
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.Comedown.Popup), target, target);
        ent.Comp.NextComedown = null;
        Inject(target, ent.Comp.Comedown.Reagents, ent.Comp.Comedown.Quantity);
    }

    private void Inject(EntityUid target, List<ProtoId<ReagentPrototype>> reagents, FixedPoint2 quantity)
    {
        foreach (var reagent in reagents)
            _bloodstream.TryAddToChemicals(target, new Solution(reagent, quantity));
    }
}
