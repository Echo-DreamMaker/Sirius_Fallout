// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
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

        // using the action on yourself opens the long-range window
        if (target == user)
        {
            _ui.SetUiState(ent.Owner, TelepathyUiKey.Far, new TelepathyFarState(GetReachableMinds(ent, user)));
            if (!_ui.TryOpenUi(ent.Owner, TelepathyUiKey.Far, user))
                Log.Error($"Failed to open far UI for {ToPrettyString(ent)} of {ToPrettyString(user)}");
            return;
        }

        ent.Comp.Target = target; // so it can be used later

        if (!_ui.TryOpenUi(ent.Owner, TelepathyUiKey.Key, user))
            Log.Error($"Failed to open UI for {ToPrettyString(ent)} of {ToPrettyString(user)}");

        // intentionally not handled, only start the cooldown after a message is sent
    }

    /// <summary>
    /// Every mind this telepath can currently reach: ones they've touched in person, plus any
    /// other telepath, who are always on the same wavelength.
    /// </summary>
    private List<TelepathyFarEntry> GetReachableMinds(Entity<TelepathyActionComponent> ent, EntityUid user)
    {
        var entries = new List<TelepathyFarEntry>();
        var seen = new HashSet<EntityUid>();

        foreach (var mind in GetOtherTelepaths(ent.Owner))
        {
            if (mind == user || !seen.Add(mind))
                continue;

            entries.Add(new TelepathyFarEntry(GetNetEntity(mind), Identity.Name(mind, EntityManager), true));
        }

        // minds contacted in person, dropping any that have since stopped existing
        ent.Comp.KnownMinds.RemoveWhere(known => !Exists(known));
        foreach (var known in ent.Comp.KnownMinds)
        {
            if (known == user || !seen.Add(known) || !HasComp<MindContainerComponent>(known))
                continue;

            entries.Add(new TelepathyFarEntry(GetNetEntity(known), Identity.Name(known, EntityManager), false));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    /// <summary>
    /// The mobs carrying any other telepathy action.
    /// </summary>
    private List<EntityUid> GetOtherTelepaths(EntityUid self)
    {
        var found = new List<EntityUid>();
        var query = EntityQueryEnumerator<TelepathyActionComponent>();
        while (query.MoveNext(out var actionUid, out _))
        {
            if (actionUid == self)
                continue;

            BaseActionComponent? action = null;
            if (!_actions.ResolveActionData(actionUid, ref action) || action.AttachedEntity is not {} mob)
                continue;

            if (HasComp<MindContainerComponent>(mob) && HasComp<MobStateComponent>(mob))
                found.Add(mob);
        }

        return found;
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

        // has to still be someone we can actually reach - don't trust the client's pick
        var wanted = args.Target; // can't capture a ref parameter in the lambda
        if (!GetReachableMinds(ent, user).Any(entry => entry.Target == wanted))
            return;

        Deliver(ent, user, target.Value, args.Message);
    }

    private void Deliver(Entity<TelepathyActionComponent> ent, EntityUid user, EntityUid target, string message)
    {
        var msg = message.Trim();
        if (msg.Length == 0 || msg.Length > ent.Comp.MaxLength) // no malf
            return;

        // no prediction beyond here since client doesn't know other entities' ActorComponent
        if (_net.IsClient)
            return;

        var ident = Identity.Entity(target, EntityManager);
        if (!HasComp<MindContainerComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("MutationTelepathy-popup-mindless", ("target", ident)), user, user);
            return;
        }

        // start the delay now that a message is being sent
        _actions.StartUseDelay(ent.Owner);

        // touching a mind directly means you can find it again later
        if (ent.Comp.KnownMinds.Add(target))
            _popup.PopupEntity(Loc.GetString("MutationTelepathy-popup-remembered", ("target", ident)), user, user);

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{user:user} sent a telepathic message to {target:target}: {msg}");

        // TODO: handle mind magic protection with -popup-blocked
        // Delivery goes to the target's chat rather than a popup: popups don't render markup
        // and fade on their own, which is no good for something you're meant to read and reply
        // to. Chat needs the chat manager, so the server picks this up.
        var ev = new TelepathyDeliverEvent(user, target, msg);
        RaiseLocalEvent(ref ev);

        _popup.PopupEntity(Loc.GetString("MutationTelepathy-popup-sent", ("target", ident)), user, user);
        // TODO: send message for ghosts too
    }
}
