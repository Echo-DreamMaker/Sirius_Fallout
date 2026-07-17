using Content.Shared.ActionBlocker;
using Content.Shared.CCVar;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Body.Components;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Standing;
using Content.Shared.Popups;
using Content.Shared._Misfits.C27;
using Content.Shared.Silicon.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.Standing;

public abstract class SharedLayingDownSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleStanding, InputCmdHandler.FromDelegate(ToggleStanding))
            .Bind(ContentKeyFunctions.ToggleCrawlingUnder, InputCmdHandler.FromDelegate(HandleCrawlUnderRequest, handle: false))
            .Register<SharedLayingDownSystem>();

        SubscribeNetworkEvent<ChangeLayingDownEvent>(OnChangeState);

        SubscribeLocalEvent<StandingStateComponent, StandingUpDoAfterEvent>(OnStandingUpDoAfter);
        SubscribeLocalEvent<LayingDownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<LayingDownComponent, EntParentChangedMessage>(OnParentChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<SharedLayingDownSystem>();
    }

    private void ToggleStanding(ICommonSession? session)
    {
        if (session is not { AttachedEntity: { Valid: true } uid } _
            || !Exists(uid)
            || !HasComp<LayingDownComponent>(session.AttachedEntity)
            || _gravity.IsWeightless(session.AttachedEntity.Value))
            return;

        RaiseNetworkEvent(new ChangeLayingDownEvent());
    }

    private void HandleCrawlUnderRequest(ICommonSession? session)
    {
        if (session == null
            || session.AttachedEntity is not {} uid
            || !TryComp<StandingStateComponent>(uid, out var standingState)
            || !TryComp<LayingDownComponent>(uid, out var layingDown)
            || !_actionBlocker.CanInteract(uid, null))
            return;

        var newState = !layingDown.IsCrawlingUnder;
        if (standingState.CurrentState is StandingState.Standing)
            newState = false; // If the entity is already standing, this function only serves a fallback method to fix its draw depth

        // Do not allow to begin crawling under if it's disabled in config. We still, however, allow to stop it, as a failsafe.
        if (newState && !_config.GetCVar(CCVars.CrawlUnderTables))
        {
            _popups.PopupEntity(Loc.GetString("crawling-under-tables-disabled-popup"), uid, session);
            return;
        }

        layingDown.IsCrawlingUnder = newState;
        _speed.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, layingDown);
    }

    private void OnChangeState(ChangeLayingDownEvent ev, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var uid = args.SenderSession.AttachedEntity.Value;
        if (!TryComp(uid, out StandingStateComponent? standing)
            || !TryComp(uid, out LayingDownComponent? layingDown))
            return;

        RaiseNetworkEvent(new CheckAutoGetUpEvent(GetNetEntity(uid)));

        if (HasComp<KnockedDownComponent>(uid)
            || !_mobState.IsAlive(uid))
            return;

        if (_standing.IsDown(uid, standing))
            TryStandUp(uid, layingDown, standing);
        else
            TryLieDown(uid, layingDown, standing);
    }

    private void OnStandingUpDoAfter(EntityUid uid, StandingStateComponent component, StandingUpDoAfterEvent args)
    {
        // Misfits Change: clear the post-crit recovery override regardless of outcome
        if (TryComp<LayingDownComponent>(uid, out var layingDownComp))
            layingDownComp.PostCritRecoveryOverride = null;

        if (args.Handled || args.Cancelled
            || HasComp<KnockedDownComponent>(uid)
            || _mobState.IsIncapacitated(uid)
            || !_standing.Stand(uid))
        {
            component.CurrentState = StandingState.Lying;
            return; // Misfits Fix: missing return caused StandingState.Standing to always overwrite Lying on cancelled/failed stand-up
        }

        component.CurrentState = StandingState.Standing;
    }

    private void OnRefreshMovementSpeed(EntityUid uid, LayingDownComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!_standing.IsDown(uid))
            return;

        var modifier = component.LyingSpeedModifier * (component.IsCrawlingUnder ? component.CrawlingUnderSpeedModifier : 1);

        // Misfits Add: apply additional speed penalty when crawling while in Critical state
        // Only players (not NPCs) crit crawl, and robots/silicons/C27s never crit crawl
        if (_mobState.IsCritical(uid)
            && HasComp<ActorComponent>(uid)
            && !HasComp<SiliconComponent>(uid)
            && !HasComp<MisfitsC27Component>(uid))
            modifier *= component.CritCrawlSpeedModifier;

        args.ModifySpeed(modifier, modifier);
    }

    private void OnParentChanged(EntityUid uid, LayingDownComponent component, EntParentChangedMessage args)
    {
        // If the entity is not on a grid, try to make it stand up to avoid issues
        if (!TryComp<StandingStateComponent>(uid, out var standingState)
            || standingState.CurrentState is StandingState.Standing
            || Transform(uid).GridUid != null)
            return;

        _standing.Stand(uid, standingState);
    }

    public bool TryStandUp(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null)
    {
        if (!Resolve(uid, ref standingState, false)
            || !Resolve(uid, ref layingDown, false)
            || standingState.CurrentState is not StandingState.Lying
            || !_mobState.IsAlive(uid)
            || TerminatingOrDeleted(uid)
            || !TryComp<BodyComponent>(uid, out var body)
            || body.LegEntities.Count < body.RequiredLegs
            || HasComp<DebrainedComponent>(uid))
            return false;

        // Misfits Change: use the crit recovery override if set (hard crit = 8s, soft crit = 2s), otherwise normal 1s
        var standTime = layingDown.PostCritRecoveryOverride ?? layingDown.StandingUpTime;
        var args = new DoAfterArgs(EntityManager, uid, standTime, new StandingUpDoAfterEvent(), uid)
        {
            BreakOnHandChange = false,
            RequireCanInteract = false
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        standingState.CurrentState = StandingState.GettingUp;
        layingDown.IsCrawlingUnder = false;
        return true;
    }

    public bool TryLieDown(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null, DropHeldItemsBehavior behavior = DropHeldItemsBehavior.NoDrop)
    {
        if (!Resolve(uid, ref standingState, false)
            || !Resolve(uid, ref layingDown, false)
            || standingState.CurrentState is not StandingState.Standing)
        {
            if (behavior == DropHeldItemsBehavior.AlwaysDrop)
                RaiseLocalEvent(uid, new DropHandItemsEvent());

            return false;
        }

        _standing.Down(uid, true, behavior != DropHeldItemsBehavior.DropIfStanding, standingState); // Corvax-Change
        return true;
    }
}

[Serializable, NetSerializable]
public sealed partial class StandingUpDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum DropHeldItemsBehavior : byte
{
    NoDrop,
    DropIfStanding,
    AlwaysDrop
}
