using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._Sirius.NPC;
using Content.Shared._Sirius.NPC.Components;
using Content.Shared._Sirius.Verbs;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Sirius.NPC.Systems;

public sealed class SiriusFollowerSystem : EntitySystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private const string FollowCompoundId = "RuminantFollowCompound";
    private const string HoldPositionCompoundId = "HoldPositionCompound";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiriusFollowerComponent, ComponentShutdown>(OnFollowerShutdown);
        SubscribeLocalEvent<SiriusFollowerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<SiriusFollowerComponent, AttackAttemptEvent>(OnFollowerAttackAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateFollowerNoPathTimeout(frameTime);
        UpdateAutoHeldFollowers();
    }

    private void UpdateFollowerNoPathTimeout(float frameTime)
    {
        var query = EntityQueryEnumerator<SiriusFollowerComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var follower, out var htn))
        {
            if (!follower.IsFollowing || follower.Commander == null)
                continue;

            if (TerminatingOrDeleted(follower.Commander.Value))
            {
                StopFollowing(uid, follower, follower.Commander.Value);
                continue;
            }

            if (TryComp<NPCSteeringComponent>(uid, out var steering) &&
                steering.Status == SteeringStatus.NoPath)
            {
                follower.NoPathAccumulator += frameTime;
            }
            else
            {
                follower.NoPathAccumulator = 0f;
            }

            if (follower.NoPathAccumulator >= follower.NoPathTimeoutSeconds)
            {
                follower.NoPathAccumulator = 0f;
                follower.WasAutoHeld = true;
                ApplyFollowerOrder(uid, follower, SiriusFollowerOrderType.HoldPosition);
                if (follower.Commander != null)
                {
                    _popup.PopupEntity(Loc.GetString("npc-follower-lost"), uid, follower.Commander.Value, PopupType.Small);
                }
            }
        }
    }

    private void UpdateAutoHeldFollowers()
    {
        var query = EntityQueryEnumerator<SiriusFollowerComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var follower, out var htn))
        {
            if (!follower.WasAutoHeld || follower.Commander == null)
                continue;

            if (TerminatingOrDeleted(follower.Commander.Value))
            {
                StopFollowing(uid, follower, follower.Commander.Value);
                continue;
            }

            var dist = (_transform.GetWorldPosition(uid) - _transform.GetWorldPosition(follower.Commander.Value)).Length();
            if (dist <= 3f)
            {
                follower.WasAutoHeld = false;
                ApplyFollowerOrder(uid, follower, SiriusFollowerOrderType.Follow);
            }
        }
    }

    private void OnFollowerShutdown(Entity<SiriusFollowerComponent> ent, ref ComponentShutdown args)
    {
        var follower = ent.Comp;
        if (follower.Commander != null)
        {
            CleanupFollower(follower.Commander.Value, ent);
        }
    }

    private void OnFollowerAttackAttempt(Entity<SiriusFollowerComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target != null && ent.Comp.Commander != null)
        {
            if (args.Target == ent.Comp.Commander)
            {
                args.Cancel();
                return;
            }

            if (TryComp<SiriusFollowerComponent>(args.Target, out var targetFollower) &&
                targetFollower.Commander == ent.Comp.Commander)
            {
                args.Cancel();
            }
        }
    }

    private void OnGetVerbs(Entity<SiriusFollowerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == args.Target)
            return;

        if (!HasComp<ActorComponent>(args.User))
            return;

        if (TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState != MobState.Alive)
        {
            var deadVerb = new Verb
            {
                Text = Loc.GetString("follower-cant-follow-dead"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Follow
            };
            args.Verbs.Add(deadVerb);
            return;
        }

        var follower = ent.Comp;
        var user = args.User;

        if (follower.Commander != null && follower.Commander != user)
        {
            var commanderName = MetaData(follower.Commander.Value).EntityName;
            var verb = new Verb
            {
                Text = Loc.GetString("follower-verb-following", ("name", commanderName)),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Follow
            };
            args.Verbs.Add(verb);
            return;
        }

        if (follower.Commander == user && follower.IsFollowing)
        {
            var unfollow = new Verb
            {
                Text = Loc.GetString("follower-verb-unfollow"),
                Priority = 1,
                Category = SiriusVerbCategory.Follow,
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/close.svg.192dpi.png")),
                Act = () => StopFollowing(ent, user)
            };
            args.Verbs.Add(unfollow);
            return;
        }

        var follow = new Verb
        {
            Text = Loc.GetString("follower-verb-follow"),
            Priority = 1,
            Category = SiriusVerbCategory.Follow,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
            Act = () => StartFollowing(ent, user)
        };
        args.Verbs.Add(follow);
    }

    public void StartFollowing(Entity<SiriusFollowerComponent> ent, EntityUid commander)
    {
        var follower = ent.Comp;

        if (follower.Commander != null && follower.Commander != commander)
        {
            _popup.PopupEntity(Loc.GetString("follower-already-following"), ent, commander, PopupType.Small);
            return;
        }

        if (TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState != MobState.Alive)
        {
            _popup.PopupEntity(Loc.GetString("follower-cant-follow-dead"), ent, commander, PopupType.Small);
            return;
        }

        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        follower.Commander = commander;
        follower.IsFollowing = true;
        follower.WasAutoHeld = false;
        follower.NoPathAccumulator = 0f;

        if (string.IsNullOrEmpty(follower.OriginalRootTask))
        {
            follower.OriginalRootTask = htn.RootTask.Task;
        }

        if (!TryComp<FactionExceptionComponent>(ent, out var factionException))
            factionException = EnsureComp<FactionExceptionComponent>(ent);

        _npcFaction.IgnoreEntity((ent, factionException), commander);

        ApplyFollowerOrder(ent, follower, SiriusFollowerOrderType.Follow);
        _popup.PopupEntity(Loc.GetString("follower-now-following"), ent, commander, PopupType.Small);
    }

    public void StopFollowing(Entity<SiriusFollowerComponent> ent, EntityUid commander)
    {
        var follower = ent.Comp;

        if (follower.Commander != commander)
            return;

        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        if (TryComp<FactionExceptionComponent>(ent, out var factionException))
        {
            _npcFaction.UnignoreEntity((ent, factionException), commander);
        }

        if (!string.IsNullOrEmpty(follower.OriginalRootTask))
        {
            htn.RootTask.Task = follower.OriginalRootTask;
            _htn.Replan(htn);
        }

        follower.Commander = null;
        follower.IsFollowing = false;
        follower.WasAutoHeld = false;
        follower.NoPathAccumulator = 0f;

        CleanupFollower(commander, ent);

        _popup.PopupEntity(Loc.GetString("follower-stopped-following"), ent, commander, PopupType.Small);
    }

    public void StopFollowing(EntityUid uid, SiriusFollowerComponent follower, EntityUid commander)
    {
        if (follower.Commander != commander)
            return;

        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        if (TryComp<FactionExceptionComponent>(uid, out var factionException))
        {
            _npcFaction.UnignoreEntity((uid, factionException), commander);
        }

        if (!string.IsNullOrEmpty(follower.OriginalRootTask))
        {
            htn.RootTask.Task = follower.OriginalRootTask;
            _htn.Replan(htn);
        }

        follower.Commander = null;
        follower.IsFollowing = false;
        follower.WasAutoHeld = false;
        follower.NoPathAccumulator = 0f;

        CleanupFollower(commander, uid);

        _popup.PopupEntity(Loc.GetString("follower-stopped-following"), uid, commander, PopupType.Small);
    }

    private void CleanupFollower(EntityUid commander, Entity<SiriusFollowerComponent> ent)
    {
        if (TryComp<FactionExceptionComponent>(ent, out var factionException))
        {
            _npcFaction.UnignoreEntity((ent, factionException), commander);
        }

        if (TryComp<HTNComponent>(ent, out var htn))
        {
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
            htn.Blackboard.Remove<EntityUid>("Target");
            htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
            htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
        }
    }

    private void CleanupFollower(EntityUid commander, EntityUid uid)
    {
        if (TryComp<FactionExceptionComponent>(uid, out var factionException))
        {
            _npcFaction.UnignoreEntity((uid, factionException), commander);
        }

        if (TryComp<HTNComponent>(uid, out var htn))
        {
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
            htn.Blackboard.Remove<EntityUid>("Target");
            htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
            htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
        }
    }

    private void ApplyFollowerOrder(Entity<SiriusFollowerComponent> ent, SiriusFollowerComponent follower, SiriusFollowerOrderType order)
    {
        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        _npcSystem.SleepNPC(ent, htn);

        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        htn.Blackboard.Remove<EntityUid>("Target");
        htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
        htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);

        string newRoot;

        switch (order)
        {
            case SiriusFollowerOrderType.Follow:
                newRoot = FollowCompoundId;
                if (follower.Commander != null)
                {
                    htn.Blackboard.SetValue(NPCBlackboard.FollowTarget,
                        new EntityCoordinates(follower.Commander.Value, System.Numerics.Vector2.Zero));
                }
                break;
            case SiriusFollowerOrderType.HoldPosition:
                newRoot = HoldPositionCompoundId;
                break;
            default:
                newRoot = follower.OriginalRootTask;
                break;
        }

        htn.RootTask.Task = newRoot;
        _htn.Replan(htn);

        EnsureComp<InputMoverComponent>(ent);
        _npcSystem.WakeNPC(ent, htn);
    }

    private void ApplyFollowerOrder(EntityUid uid, SiriusFollowerComponent follower, SiriusFollowerOrderType order)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        _npcSystem.SleepNPC(uid, htn);

        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        htn.Blackboard.Remove<EntityUid>("Target");
        htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
        htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);

        string newRoot;

        switch (order)
        {
            case SiriusFollowerOrderType.Follow:
                newRoot = FollowCompoundId;
                if (follower.Commander != null)
                {
                    htn.Blackboard.SetValue(NPCBlackboard.FollowTarget,
                        new EntityCoordinates(follower.Commander.Value, System.Numerics.Vector2.Zero));
                }
                break;
            case SiriusFollowerOrderType.HoldPosition:
                newRoot = HoldPositionCompoundId;
                break;
            default:
                newRoot = follower.OriginalRootTask;
                break;
        }

        htn.RootTask.Task = newRoot;
        _htn.Replan(htn);

        EnsureComp<InputMoverComponent>(uid);
        _npcSystem.WakeNPC(uid, htn);
    }
}
