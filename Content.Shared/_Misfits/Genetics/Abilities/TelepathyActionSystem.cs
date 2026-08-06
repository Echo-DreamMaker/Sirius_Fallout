// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Shared._Misfits.Genetics.Abilities;

public sealed partial class TelepathyActionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelepathyActionComponent, TelepathyActionEvent>(OnTelepathyPrompt);

        Subs.BuiEvents<TelepathyActionComponent>(TelepathyUiKey.Key, subs =>
        {
            subs.Event<TelepathyChosenMessage>(OnTelepathyChosen);
        });

        Subs.BuiEvents<TelepathyActionComponent>(TelepathyUiKey.Far, subs =>
        {
            subs.Event<TelepathyFarChosenMessage>(OnTelepathyFarChosen);
        });
    }

    private void OnTelepathyPrompt(Entity<TelepathyActionComponent> ent, ref TelepathyActionEvent args)
    {
        // for this specifically, prediction is fucked
        // but other predicted opens are fine (e.g. debug effect stick)
        // incomprehensible shitcode
        if (_net.IsClient)
            return;

        var user = args.Performer;
        var target = args.Target;

        // using the action on yourself opens the long-range window with a list of
        // everyone online, so you can reach minds you can't see.
        if (target == user)
        {
            var players = new List<TelepathyFarEntry>();
            foreach (var session in _player.Sessions)
            {
                if (session.AttachedEntity is not {} character ||
                    character == user ||
                    !HasComp<MobStateComponent>(character) ||
                    !HasComp<MindContainerComponent>(character)) // match the action's own whitelist
                    continue;

                // Identity.Name, not Name: reaching out to a mind shouldn't tell you who is
                // behind a mask or a forged ID.
                players.Add(new TelepathyFarEntry(GetNetEntity(character), Identity.Name(character, EntityManager)));
            }
            players.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            _ui.SetUiState(ent.Owner, TelepathyUiKey.Far, new TelepathyFarState(players));
            if (!_ui.TryOpenUi(ent.Owner, TelepathyUiKey.Far, user))
                Log.Error($"Failed to open far UI for {ToPrettyString(ent)} of {ToPrettyString(user)}");
            return;
        }

        ent.Comp.Target = target; // so it can be used later

        if (!_ui.TryOpenUi(ent.Owner, TelepathyUiKey.Key, user))
            Log.Error($"Failed to open UI for {ToPrettyString(ent)} of {ToPrettyString(user)}");

        // intentionally not handled, only start the cooldown after a message is sent
    }

    private void OnTelepathyChosen(Entity<TelepathyActionComponent> ent, ref TelepathyChosenMessage args)
    {
        var user = args.Actor;
        if (ent.Comp.Target is not {} target)
            return;

        ent.Comp.Target = null;

        Deliver(ent, user, target, args.Message);
    }

    private void OnTelepathyFarChosen(Entity<TelepathyActionComponent> ent, ref TelepathyFarChosenMessage args)
    {
        var user = args.Actor;
        if (!TryGetEntity(args.Target, out var target) || target == user)
            return;

        // only allow reaching actual player characters, same filter as the list
        if (!HasComp<MobStateComponent>(target.Value) || !HasComp<MindContainerComponent>(target.Value))
            return;

        Deliver(ent, user, target.Value, args.Message);
    }

    private void Deliver(Entity<TelepathyActionComponent> ent, EntityUid user, EntityUid target, string message)
    {
        var msg = message.Trim();
        if (msg.Length == 0 || msg.Length > ent.Comp.MaxLength) // no malf
            return;

        // TODO: close it if the target leaves range

        // no prediction beyond here since client doesn't know other entities' ActorComponent
        if (_net.IsClient)
            return;

        var ident = Identity.Entity(target, EntityManager);
        if (!HasComp<MetaDataComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("MutationTelepathy-popup-mindless", ("target", ident)), user, user);
            return;
        }

        // start the delay now that a message is being sent
        _actions.StartUseDelay(ent.Owner);

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{user:user} sent a telepathic message to {target:target}: {msg}");

        // TODO: handle mind magic protection with -popup-blocked
        // deliver the message into the target's mind - previously this was sent to the
        // sender instead, so the target never saw anything.
        Tell(target, msg);
        _popup.PopupEntity(Loc.GetString("MutationTelepathy-popup-sent", ("target", ident)), user, user);
        // TODO: send message for ghosts too
    }

    private void Tell(EntityUid target, string message)
    {
        _popup.PopupEntity(
            Loc.GetString("MutationTelepathy-message-wrap", ("message", FormattedMessage.EscapeText(message))),
            target,
            target,
            PopupType.Medium);
    }
}
