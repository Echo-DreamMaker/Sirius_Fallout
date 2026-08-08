using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._Sirius.NPC.Actions;
using Content.Shared._Sirius.NPC.Components;
using Content.Shared.Actions;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server._Sirius.NPC.Systems;

public sealed class PetActionsSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private const string FollowCompoundId = "RuminantFollowCompound";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PetFollowStayActionEvent>(OnPetFollowStay);
        SubscribeLocalEvent<PetAttackCancelActionEvent>(OnPetAttackCancel);
        SubscribeLocalEvent<PetReleaseActionEvent>(OnPetRelease);
        SubscribeLocalEvent<ActorComponent, AfterPointedAtEvent>(OnPlayerPointedAt);
    }
    private void OnPlayerPointedAt(Entity<ActorComponent> player, ref AfterPointedAtEvent args)
    {
        if (!TryGetPet(player, out var pet, out var follower))
            return;
        if (!follower.AttackMode)
            return;
        var pointed = args.Pointed;
        if (pointed == player.Owner)
        {
            _popup.PopupEntity(Loc.GetString("follower-cant-attack-owner"), pet, player, PopupType.Small);
            return;
        }
        if (TryComp<SiriusFollowerComponent>(pointed, out var targetFollower) &&
            targetFollower.IsTamed && targetFollower.Tamer == player)
        {
            _popup.PopupEntity(Loc.GetString("follower-cant-attack-pet"), pet, player, PopupType.Small);
            return;
        }
        if (!TryComp<HTNComponent>(pet, out var htn))
            return;
        htn.Blackboard.SetValue(NPCBlackboard.CurrentOrderedTarget, pointed);

        if (!TryComp<FactionExceptionComponent>(pet, out var factionException))
            factionException = EnsureComp<FactionExceptionComponent>(pet);

        _npcFaction.AggroEntity((pet, factionException), pointed);
        if (follower.IsFollowing)
        {
            htn.Blackboard.SetValue("ReturnToFollow", true);
            _npcSystem.SleepNPC(pet, htn);
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.MovementTarget);
            htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
            string attackTask = "N14RangedHostileCompound";
            if (!string.IsNullOrEmpty(follower.OriginalRootTask))
            {
                if (follower.OriginalRootTask.Contains("Hostile") ||
                    follower.OriginalRootTask.Contains("Ranged") ||
                    follower.OriginalRootTask.Contains("Melee"))
                {
                    attackTask = follower.OriginalRootTask;
                }
            }
            htn.RootTask.Task = attackTask;
            _htn.Replan(htn);
            _npcSystem.WakeNPC(pet, htn);
        }
        else
        {
            _htn.Replan(htn);
        }
    }
    private void OnPetFollowStay(PetFollowStayActionEvent ev)
    {
        if (!TryGetPet(ev.Performer, out var pet, out var follower))
            return;

        if (follower.Tamer != ev.Performer)
            return;

        if (follower.IsStaying)
        {
            follower.IsStaying = false;
            follower.IsFollowing = true;
            follower.WasAutoHeld = false;
            follower.NoPathAccumulator = 0f;
            follower.Commander = ev.Performer;
            ApplyFollowOrder(pet, follower);
            _actions.SetToggled(ev.Action, false);
        }
        else
        {
            follower.IsStaying = true;
            follower.IsFollowing = false;
            follower.WasAutoHeld = true;
            follower.Commander = null;
            ApplyWanderOrder(pet, follower);
            _actions.SetToggled(ev.Action, true);
        }
    }
    private void OnPetAttackCancel(PetAttackCancelActionEvent ev)
    {
        if (!TryGetPet(ev.Performer, out var pet, out var follower))
            return;

        if (follower.Tamer != ev.Performer)
            return;

        if (!TryComp<HTNComponent>(pet, out var htn))
            return;

        if (follower.AttackMode)
        {
            if (TryComp<FactionExceptionComponent>(pet, out var factionException))
            {
                foreach (var hostile in factionException.Hostiles.ToList())
                {
                    _npcFaction.DeAggroEntity((pet, factionException), hostile);
                }
            }
            htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
            htn.Blackboard.Remove<EntityUid>("Target");
            htn.Blackboard.Remove<bool>("AttackMode");
            htn.Blackboard.Remove<bool>("ReturnToFollow");

            follower.AttackMode = false;
            _actions.SetToggled(ev.Action, false);
            if (follower.IsFollowing && follower.Commander != null)
            {
                ApplyFollowOrder(pet, follower);
            }
            else
            {
                _htn.Replan(htn);
            }
        }
        else
        {
            follower.AttackMode = true;
            htn.Blackboard.SetValue("AttackMode", true);
            _actions.SetToggled(ev.Action, true);
            _htn.Replan(htn);
        }
    }
    private void OnPetRelease(PetReleaseActionEvent ev)
    {
        if (!TryGetPet(ev.Performer, out var pet, out var follower))
            return;

        if (follower.Tamer != ev.Performer)
            return;

        ReleasePet(pet, follower, ev.Performer);
        _popup.PopupEntity(Loc.GetString("follower-released"), pet, ev.Performer, PopupType.Small);
    }
    private bool TryGetPet(EntityUid owner, out EntityUid pet, out SiriusFollowerComponent follower)
    {
        pet = EntityUid.Invalid;
        follower = null!;
        var query = EntityQueryEnumerator<SiriusFollowerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsTamed && comp.Tamer == owner)
            {
                pet = uid;
                follower = comp;
                return true;
            }
        }

        return false;
    }
    private void ReleasePet(EntityUid pet, SiriusFollowerComponent follower, EntityUid owner)
    {
        if (follower.Tamer != owner)
            return;

        RemovePetActions(owner, follower);

        if (TryComp<FactionExceptionComponent>(pet, out var factionException))
        {
            foreach (var hostile in factionException.Hostiles.ToList())
            {
                _npcFaction.DeAggroEntity((pet, factionException), hostile);
            }
            _npcFaction.UnignoreEntity((pet, factionException), owner);
        }

        _npcFaction.RemoveFriendlyEntity(pet, owner);

        if (TryComp<HTNComponent>(pet, out var htn))
        {
            if (!string.IsNullOrEmpty(follower.OriginalRootTask))
            {
                htn.RootTask.Task = follower.OriginalRootTask;
                _htn.Replan(htn);
            }
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
            htn.Blackboard.Remove<EntityUid>("Target");
            htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
            htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
            htn.Blackboard.Remove<bool>("AttackMode");
            htn.Blackboard.Remove<bool>("ReturnToFollow");
        }
        follower.IsTamed = false;
        follower.Tamer = null;
        follower.Commander = null;
        follower.IsFollowing = false;
        follower.IsStaying = false;
        follower.AttackMode = false;
        follower.WasAutoHeld = false;
        follower.NoPathAccumulator = 0f;
        follower.PetActionEntities.Clear();
        Dirty(pet, follower);
    }

    private void RemovePetActions(EntityUid owner, SiriusFollowerComponent follower)
    {
        foreach (var actionId in follower.PetActionEntities)
        {
            if (Exists(actionId))
            {
                _actions.RemoveAction(owner, actionId);
            }
        }
        follower.PetActionEntities.Clear();
    }

    private void ApplyFollowOrder(EntityUid uid, SiriusFollowerComponent follower)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;
        _npcSystem.SleepNPC(uid, htn);
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        htn.Blackboard.Remove<EntityUid>("Target");
        htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
        htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
        htn.Blackboard.Remove<bool>("AttackMode");
        htn.Blackboard.Remove<bool>("ReturnToFollow");
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.MovementTarget);
        htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
        if (follower.Commander != null)
        {
            htn.Blackboard.SetValue(NPCBlackboard.FollowTarget,
                new EntityCoordinates(follower.Commander.Value, System.Numerics.Vector2.Zero));
        }
        htn.RootTask.Task = FollowCompoundId;
        _htn.Replan(htn);
        _npcSystem.WakeNPC(uid, htn);
    }
    private void ApplyWanderOrder(EntityUid uid, SiriusFollowerComponent follower)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;
        _npcSystem.SleepNPC(uid, htn);
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        htn.Blackboard.Remove<EntityUid>("Target");
        htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
        htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
        htn.Blackboard.Remove<bool>("AttackMode");
        htn.Blackboard.Remove<bool>("ReturnToFollow");
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.MovementTarget);
        htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
        if (!string.IsNullOrEmpty(follower.OriginalRootTask))
        {
            htn.RootTask.Task = follower.OriginalRootTask;
        }
        else
        {
            htn.RootTask.Task = FollowCompoundId;
        }
        _htn.Replan(htn);
        _npcSystem.WakeNPC(uid, htn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SiriusFollowerComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var follower, out var htn))
        {
            if (htn.Blackboard.TryGetValue<bool>("ReturnToFollow", out var returnToFollow, EntityManager) && returnToFollow)
            {
                if (TryComp<FactionExceptionComponent>(uid, out var factionException))
                {
                    if (factionException.Hostiles.Count == 0)
                    {
                        htn.Blackboard.Remove<bool>("ReturnToFollow");
                        if (follower.IsFollowing && follower.Commander != null)
                        {
                            ApplyFollowOrder(uid, follower);
                        }
                    }
                }
                else
                {
                    htn.Blackboard.Remove<bool>("ReturnToFollow");
                    if (follower.IsFollowing && follower.Commander != null)
                    {
                        ApplyFollowOrder(uid, follower);
                    }
                }
            }
        }
    }

    public enum SiriusFollowerOrderType : byte
    {
        Follow,
        Wander
    }
}
