using Content.Server.Chat.Managers;
using Content.Server._Misfits.SmokeSignal;
using Content.Shared._Misfits.SmokeSignal;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.TreeOfLife;

/// <summary>
///     Sends a Tribe-only warning when a stranger enters the Tree of Life's roots.
/// </summary>
public sealed partial class TreeOfLifeWatchSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SmokeSignalSystem _signals = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var watchQuery = EntityQueryEnumerator<TreeOfLifeWatchComponent>();
        while (watchQuery.MoveNext(out var uid, out var watch))
        {
            var treeSignal = Comp<SmokeSignalComponent>(uid);
            var currentIntruders = new HashSet<EntityUid>();
            var nearby = _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, watch.Range);

            foreach (var target in nearby)
            {
                if (!TryComp<ActorComponent>(target, out var actor)
                    || actor.PlayerSession.AttachedEntity != target
                    || _mobState.IsDead(target)
                    || _signals.IsInDepartment(target, treeSignal))
                    continue;

                currentIntruders.Add(target);
                if (!watch.IntrudersInRange.Add(target))
                    continue;

                if (watch.NextAlert.TryGetValue(target, out var nextAlert) && _timing.CurTime < nextAlert)
                    continue;

                watch.NextAlert[target] = _timing.CurTime + watch.EntryCooldown;
                SendWarning(uid, treeSignal);
            }

            watch.IntrudersInRange.IntersectWith(currentIntruders);
            var deletedIntruders = watch.NextAlert.Keys.Where(intruder => Deleted(intruder)).ToArray();
            foreach (var intruder in deletedIntruders)
                watch.NextAlert.Remove(intruder);
        }
    }

    private void SendWarning(EntityUid tree, SmokeSignalComponent signal)
    {
        var filter = Filter.Empty();
        foreach (var recipient in _signals.GetRecipients(signal))
            filter.AddPlayer(Comp<ActorComponent>(recipient).PlayerSession);

        var message = Loc.GetString("tree-of-life-intruder-warning");
        _chat.ChatMessageToManyFiltered(
            filter,
            ChatChannel.Radio,
            message,
            FormattedMessage.EscapeText(message),
            tree,
            hideChat: false,
            recordReplay: false,
            Color.FromHex("#a8f6ff"));
    }
}
