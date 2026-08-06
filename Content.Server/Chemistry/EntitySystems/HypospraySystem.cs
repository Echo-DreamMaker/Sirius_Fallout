using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Hypospray.Events;
using Content.Shared.Chemistry;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;
using Content.Server.Chat.Systems;
using Content.Server.Interaction;
using Content.Server.Body.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Robust.Shared.GameStates;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chat;
using Robust.Server.Audio;

namespace Content.Server.Chemistry.EntitySystems;

public sealed class HypospraySystem : SharedHypospraySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!; // #Misfits Change Add: broadcast successful injections as emotes.
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!; // #Misfits Add - delay configured hypospray injections.
    [Dependency] private readonly InteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HyposprayComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HyposprayComponent, HyposprayDoAfterEvent>(OnHyposprayDoAfter); // #Misfits Add
        SubscribeLocalEvent<HyposprayComponent, MeleeHitEvent>(OnAttack);
        SubscribeLocalEvent<HyposprayComponent, UseInHandEvent>(OnUseInHand);
    }

    private void UseHypospray(Entity<HyposprayComponent> entity, EntityUid target, EntityUid user)
    {
        // if target is ineligible but is a container, try to draw from the container
        if (!EligibleEntity(target, EntityManager, entity))
        {
            if (_solutionContainers.TryGetDrawableSolution(target, out var drawableSolution, out _))
                TryDraw(entity, target, drawableSolution.Value, user);

            return;
        }

        StartInjection(entity, target, user);
    }

    private void OnUseInHand(Entity<HyposprayComponent> entity, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        StartInjection(entity, args.User, args.User);
        args.Handled = true;
    }

    public void OnAfterInteract(Entity<HyposprayComponent> entity, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        UseHypospray(entity, args.Target.Value, args.User);
        args.Handled = true;
    }

    public void OnAttack(Entity<HyposprayComponent> entity, ref MeleeHitEvent args)
    {
        if (!args.HitEntities.Any())
            return;

        StartInjection(entity, args.HitEntities.First(), args.User);
    }

    // #Misfits Add - Apply configured injection times through one path for self-use, interactions, and attacks.
    private void StartInjection(Entity<HyposprayComponent> entity, EntityUid target, EntityUid user)
    {
        if (!EligibleEntity(target, EntityManager, entity.Comp))
            return;

        if (TryComp(entity.Owner, out UseDelayComponent? delayComp) &&
            _useDelay.IsDelayed((entity.Owner, delayComp)))
        {
            return;
        }

        var delay = target == user
            ? entity.Comp.SelfInjectionDelay
            : entity.Comp.OtherInjectionDelay;

        if (delay <= TimeSpan.Zero)
        {
            TryDoInject(entity, target, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            delay,
            new HyposprayDoAfterEvent(),
            entity.Owner,
            target: target,
            used: entity.Owner)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            MovementThreshold = 0.1f,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameEvent,
            CancelDuplicate = false,
        });
    }

    private void OnHyposprayDoAfter(Entity<HyposprayComponent> entity, ref HyposprayDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        TryDoInject(entity, args.Args.Target.Value, args.Args.User);
        args.Handled = true;
    }

    public bool TryDoInject(Entity<HyposprayComponent> entity, EntityUid target, EntityUid user)
    {
        var (uid, component) = entity;

        if (!EligibleEntity(target, EntityManager, component))
            return false;

        if (TryComp(uid, out UseDelayComponent? delayComp))
        {
            if (_useDelay.IsDelayed((uid, delayComp)))
                return false;
        }

        string? msgFormat = null;

        // Self event
        var selfEvent = new SelfBeforeHyposprayInjectsEvent(user, entity.Owner, target);
        RaiseLocalEvent(user, selfEvent);

        if (selfEvent.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString(selfEvent.InjectMessageOverride ?? "hypospray-cant-inject", ("owner", Identity.Entity(target, EntityManager))), target, user);
            return false;
        }

        target = selfEvent.TargetGettingInjected;

        if (!EligibleEntity(target, EntityManager, component))
            return false;

        // Target event
        var targetEvent = new TargetBeforeHyposprayInjectsEvent(user, entity.Owner, target);
        RaiseLocalEvent(target, targetEvent);

        if (targetEvent.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString(targetEvent.InjectMessageOverride ?? "hypospray-cant-inject", ("owner", Identity.Entity(target, EntityManager))), target, user);
            return false;
        }

        target = targetEvent.TargetGettingInjected;

        if (!EligibleEntity(target, EntityManager, component))
            return false;

        // The target event gets priority for the overriden message.
        if (targetEvent.InjectMessageOverride != null)
            msgFormat = targetEvent.InjectMessageOverride;
        else if (selfEvent.InjectMessageOverride != null)
            msgFormat = selfEvent.InjectMessageOverride;
        else if (target == user)
            msgFormat = "hypospray-component-inject-self-message";

        if (!_solutionContainers.TryGetSolution(uid, component.SolutionName, out var hypoSpraySoln, out var hypoSpraySolution) || hypoSpraySolution.Volume == 0)
        {
            _popup.PopupEntity(Loc.GetString("hypospray-component-empty-message"), target, user);
            return true;
        }

        if (!_solutionContainers.TryGetInjectableSolution(target, out var targetSoln, out var targetSolution))
        {
            _popup.PopupEntity(Loc.GetString("hypospray-cant-inject", ("target", Identity.Entity(target, EntityManager))), target, user);
            return false;
        }

        _popup.PopupEntity(Loc.GetString(msgFormat ?? "hypospray-component-inject-other-message", ("other", target)), target, user);

        if (target != user)
        {
            _popup.PopupEntity(Loc.GetString("hypospray-component-feel-prick-message"), target, target);
            // TODO: This should just be using melee attacks...
            // meleeSys.SendLunge(angle, user);
        }

        _audio.PlayPvs(component.InjectSound, user);

        // Medipens and such use this system and don't have a delay, requiring extra checks
        // BeginDelay function returns if item is already on delay
        if (delayComp != null)
            _useDelay.TryResetDelay((uid, delayComp));

        // Get transfer amount. May be smaller than component.TransferAmount if not enough room
        var realTransferAmount = FixedPoint2.Min(component.TransferAmount, targetSolution.AvailableVolume);

        if (realTransferAmount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("hypospray-component-transfer-already-full-message", ("owner", target)), target, user);
            return true;
        }

        // Move units from attackSolution to targetSolution
        var removedSolution = _solutionContainers.SplitSolution(hypoSpraySoln.Value, realTransferAmount);

        if (!targetSolution.CanAddSolution(removedSolution))
            return true;
        _reactiveSystem.DoEntityReaction(target, removedSolution, ReactionMethod.Injection);
        _solutionContainers.TryAddSolution(targetSoln.Value, removedSolution);

        var ev = new TransferDnaEvent { Donor = target, Recipient = uid };
        RaiseLocalEvent(target, ref ev);

        // same LogType as syringes...
        _adminLogger.Add(LogType.ForceFeed, $"{EntityManager.ToPrettyString(user):user} injected {EntityManager.ToPrettyString(target):target} with a solution {SolutionContainerSystem.ToPrettyString(removedSolution):removedSolution} using a {EntityManager.ToPrettyString(uid):using}");

        TrySendInjectionEmote(user, target, uid);

        return true;
    }

    // #Misfits Change Add: surface successful mob injections as local emote chat instead of only private popups.
    private void TrySendInjectionEmote(EntityUid user, EntityUid target, EntityUid injector)
    {
        if (user == target || !HasComp<MobStateComponent>(target))
            return;

        _chatSystem.TrySendInGameICMessage(user,
            Loc.GetString("misfits-chat-inject-other",
                ("target", Identity.Entity(target, EntityManager)),
                ("item", Identity.Entity(injector, EntityManager))),
            Content.Shared.Chat.InGameICChatType.Emote,
            Content.Shared.Chat.ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
    }

    private void TryDraw(Entity<HyposprayComponent> entity, Entity<BloodstreamComponent?> target, Entity<SolutionComponent> targetSolution, EntityUid user)
    {
        if (!_solutionContainers.TryGetSolution(entity.Owner, entity.Comp.SolutionName, out var soln,
                out var solution) || solution.AvailableVolume == 0)
        {
            return;
        }

        // Get transfer amount. May be smaller than _transferAmount if not enough room, also make sure there's room in the injector
        var realTransferAmount = FixedPoint2.Min(entity.Comp.TransferAmount, targetSolution.Comp.Solution.Volume,
            solution.AvailableVolume);

        if (realTransferAmount <= 0)
        {
            _popup.PopupEntity(
                Loc.GetString("injector-component-target-is-empty-message",
                    ("target", Identity.Entity(target, EntityManager))),
                entity.Owner, user);
            return;
        }

        var removedSolution = _solutionContainers.Draw(target.Owner, targetSolution, realTransferAmount);

        if (!_solutionContainers.TryAddSolution(soln.Value, removedSolution))
        {
            return;
        }

        _popup.PopupEntity(Loc.GetString("injector-component-draw-success-message",
            ("amount", removedSolution.Volume),
            ("target", Identity.Entity(target, EntityManager))), entity.Owner, user);
    }

    private bool EligibleEntity(EntityUid entity, IEntityManager entMan, HyposprayComponent component)
    {
        // TODO: Does checking for BodyComponent make sense as a "can be hypospray'd" tag?
        // In SS13 the hypospray ONLY works on mobs, NOT beakers or anything else.
        // But this is 14, we dont do what SS13 does just because SS13 does it.
        return component.OnlyAffectsMobs
            ? entMan.HasComponent<SolutionContainerManagerComponent>(entity) &&
              entMan.HasComponent<MobStateComponent>(entity)
            : entMan.HasComponent<SolutionContainerManagerComponent>(entity);
    }
}
