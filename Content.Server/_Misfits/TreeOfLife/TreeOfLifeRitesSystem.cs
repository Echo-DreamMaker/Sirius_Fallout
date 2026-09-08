using Content.Shared._Misfits.TreeOfLife;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.TreeOfLife;

public sealed partial class TreeOfLifeRitesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TreeOfLifeRitesComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerb);
        SubscribeLocalEvent<TreeOfLifeRitesComponent, TreeOfLifeSelectRiteMessage>(OnSelectRite);
    }

    private void OnGetVerb(Entity<TreeOfLifeRitesComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("tree-of-life-rites-verb"),
            Act = () => OpenUi(ent, user),
            Priority = 2,
        });
    }

    private void OpenUi(Entity<TreeOfLifeRitesComponent> ent, EntityUid user)
    {
        _ui.OpenUi(ent.Owner, TreeOfLifeRitesUiKey.Key, user);
        _ui.SetUiState(ent.Owner, TreeOfLifeRitesUiKey.Key, BuildState(ent, user));
    }

    private void OnSelectRite(Entity<TreeOfLifeRitesComponent> ent, ref TreeOfLifeSelectRiteMessage args)
    {
        if (args.Actor is not { Valid: true } user
            || !_interaction.InRangeUnobstructed(user, ent.Owner)
            || !IsShaman(user, ent.Comp))
            return;

        if (ent.Comp.NextChangeAt is { } next && _timing.CurTime < next)
            return;

        if (args.Rite is TreeOfLifeRite.None or TreeOfLifeRite.GreenHand)
            return;

        ent.Comp.ActiveRite = args.Rite;
        ent.Comp.NextChangeAt = _timing.CurTime + ent.Comp.ChangeCooldown;
        Dirty(ent);
        _ui.SetUiState(ent.Owner, TreeOfLifeRitesUiKey.Key, BuildState(ent, user));
    }

    private TreeOfLifeRitesState BuildState(Entity<TreeOfLifeRitesComponent> ent, EntityUid user)
    {
        var seconds = ent.Comp.NextChangeAt is { } next
            ? Math.Max(0, (int) Math.Ceiling((next - _timing.CurTime).TotalSeconds))
            : 0;
        return new TreeOfLifeRitesState(ent.Comp.ActiveRite, seconds, IsShaman(user, ent.Comp) && seconds == 0);
    }

    private bool IsShaman(EntityUid user, TreeOfLifeRitesComponent component)
    {
        return _mind.TryGetMind(user, out var mind, out _)
            && _jobs.MindTryGetJob(mind, out _, out var job)
            && component.ShamanJobs.Contains(job.ID);
    }
}
