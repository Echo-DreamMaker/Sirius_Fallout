using Content.Server.Chat.Managers;
using Content.Server._Misfits.SmokeSignal;
using Content.Shared._Misfits.SmokeSignal;
using Content.Shared._Misfits.TreeOfLife;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.TreeOfLife;

/// <summary>
///     Calls the Tribe to a member who falls critical during the Rite of Returning.
/// </summary>
public sealed partial class TreeOfLifeReturningSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SmokeSignalSystem _signals = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
        {
            RemCompDeferred<TreeOfLifeReturningMarkerComponent>(args.Target);
            return;
        }

        if (!TryComp<ActorComponent>(args.Target, out var actor)
            || actor.PlayerSession.AttachedEntity != args.Target
            || !TryGetActiveReturningTree(out var tree, out var signal)
            || !_signals.IsInDepartment(args.Target, signal))
            return;

        EnsureComp<TreeOfLifeReturningMarkerComponent>(args.Target);
        var filter = Filter.Empty();
        foreach (var recipient in _signals.GetRecipients(signal))
            filter.AddPlayer(Comp<ActorComponent>(recipient).PlayerSession);

        var message = Loc.GetString("tree-of-life-returning-alert", ("target", MetaData(args.Target).EntityName));
        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, FormattedMessage.EscapeText(message), tree,
            hideChat: false, recordReplay: false, Color.FromHex("#a8f6ff"));
    }

    private bool TryGetActiveReturningTree(out EntityUid tree, out SmokeSignalComponent signal)
    {
        var query = EntityQueryEnumerator<TreeOfLifeRitesComponent, SmokeSignalComponent>();
        while (query.MoveNext(out var uid, out var rites, out var treeSignal))
        {
            if (rites.ActiveRite != TreeOfLifeRite.Returning)
                continue;

            tree = uid;
            signal = treeSignal;
            return true;
        }

        tree = EntityUid.Invalid;
        signal = default!;
        return false;
    }
}
