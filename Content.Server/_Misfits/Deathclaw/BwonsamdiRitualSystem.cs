using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server._Misfits.Medical;
using Content.Shared._Misfits.Deathclaw;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Deathclaw;

[RegisterComponent]
public sealed partial class BwonsamdiClaimedComponent : Component;

public sealed class BwonsamdiRitualSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ResuscitationSystem _resuscitation = default!;

    private readonly Dictionary<EntityUid, EntityUid> _pendingMercyActions = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BwonsamdiComponent, BwonsamdiRitualClaimActionEvent>(OnClaimAction);
        SubscribeLocalEvent<BwonsamdiComponent, BwonsamdiRitualClaimDoAfterEvent>(OnClaimFinished);
        SubscribeLocalEvent<BwonsamdiComponent, BwonsamdiMercyActionEvent>(OnMercyAction);
        SubscribeLocalEvent<BwonsamdiComponent, BwonsamdiMercyDoAfterEvent>(OnMercyFinished);
        SubscribeLocalEvent<BwonsamdiClaimedComponent, MobStateChangedEvent>(OnClaimedStateChanged);
    }

    private void OnClaimAction(Entity<BwonsamdiComponent> ent, ref BwonsamdiRitualClaimActionEvent args)
    {
        if (args.Handled || !IsDead(args.Target) || HasComp<BwonsamdiClaimedComponent>(args.Target))
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.Performer,
            TimeSpan.FromSeconds(7),
            new BwonsamdiRitualClaimDoAfterEvent(),
            ent.Owner,
            args.Target,
            ent.Owner)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 2f,
        });

        if (args.Handled)
            _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("bwonsamdi-claim-start"), InGameICChatType.Emote, false);
    }

    private void OnClaimFinished(Entity<BwonsamdiComponent> ent, ref BwonsamdiRitualClaimDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target
            || !IsDead(target) || HasComp<BwonsamdiClaimedComponent>(target))
            return;

        args.Handled = true;
        EnsureComp<BwonsamdiClaimedComponent>(target);
        Spawn("BwonsamdiSoulFire", Transform(target).Coordinates);
        _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("bwonsamdi-claim-finish"), InGameICChatType.Emote, false);
    }

    private void OnMercyAction(Entity<BwonsamdiComponent> ent, ref BwonsamdiMercyActionEvent args)
    {
        if (args.Handled || args.Target == ent.Owner || !IsDeadPlayer(args.Target))
            return;

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.Performer,
            TimeSpan.FromSeconds(12),
            new BwonsamdiMercyDoAfterEvent(),
            ent.Owner,
            args.Target,
            ent.Owner)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 2f,
        });

        if (!started)
            return;

        args.Handled = true;
        _pendingMercyActions[ent.Owner] = args.Action.Owner;
        _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("bwonsamdi-mercy-start"), InGameICChatType.Emote, false);
    }

    private void OnMercyFinished(Entity<BwonsamdiComponent> ent, ref BwonsamdiMercyDoAfterEvent args)
    {
        if (!_pendingMercyActions.Remove(ent.Owner, out var action))
            return;

        if (args.Handled || args.Cancelled || args.Target is not { } target || !IsDeadPlayer(target))
            return;

        args.Handled = true;
        var result = _resuscitation.TryRejuvenateWithConsent(target, success =>
        {
            if (!success || Deleted(action))
                return;

            _actions.StartUseDelay(action);
            _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("bwonsamdi-mercy-finish"), InGameICChatType.Emote, false);
            _popup.PopupEntity(Loc.GetString("bwonsamdi-mercy-restored"), target, target, PopupType.Medium);
        });

        if (!result.HasMindSession || result.Rotten)
            _popup.PopupEntity(Loc.GetString("bwonsamdi-mercy-unanswered"), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }

    private void OnClaimedStateChanged(Entity<BwonsamdiClaimedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            RemCompDeferred<BwonsamdiClaimedComponent>(ent);
    }

    private bool IsDead(EntityUid target)
        => TryComp<MobStateComponent>(target, out var mob) && mob.CurrentState == MobState.Dead;

    private bool IsDeadPlayer(EntityUid target)
        => IsDead(target)
            && TryComp<MindContainerComponent>(target, out var mind)
            && mind.HasMind;
}
