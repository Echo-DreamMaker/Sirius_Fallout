using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._Misfits.Special;
using Content.Shared._Sirius.NPC;
using Content.Shared._Sirius.NPC.Components;
using Content.Shared._Sirius.Verbs;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
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

public sealed class TamingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private const string FollowCompoundId = "RuminantFollowCompound";
    private const string HoldPositionCompoundId = "HoldPositionCompound";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TameableComponent, GetVerbsEvent<Verb>>(OnTameableGetVerbs);
        SubscribeLocalEvent<TameableComponent, TamingDoAfterEvent>(OnTamingDoAfter);
        SubscribeLocalEvent<SiriusFollowerComponent, ComponentShutdown>(OnFollowerShutdown);
        SubscribeLocalEvent<SiriusFollowerComponent, AttackAttemptEvent>(OnFollowerAttackAttempt);
        SubscribeLocalEvent<SiriusFollowerComponent, EntityUnpausedEvent>(OnFollowerUnpaused);
        SubscribeLocalEvent<TameableComponent, MapInitEvent>(OnTameableMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateFollowerNoPathTimeout(frameTime);
        UpdateAutoHeldFollowers();
    }
    private void OnTameableMapInit(Entity<TameableComponent> ent, ref MapInitEvent args)
    {
        LoadTameablePreset(ent.Comp);
    }
    private void LoadTameablePreset(TameableComponent component)
    {
        if (string.IsNullOrEmpty(component.Preset))
            return;

        if (!_prototypeManager.TryIndex<TameablePresetPrototype>(component.Preset, out var preset))
        {
            Log.Error($"Tameable preset '{component.Preset}' not found!");
            return;
        }

        if (component.FavoriteFoods.Count == 0)
            component.FavoriteFoods = new List<string>(preset.FavoriteFoods);
        if (component.LikedFoods.Count == 0)
            component.LikedFoods = new List<string>(preset.LikedFoods);
        if (component.DislikedFoods.Count == 0)
            component.DislikedFoods = new List<string>(preset.DislikedFoods);

        if (component.BaseTameChance == 0.3f && preset.BaseTameChance != 0.3f)
            component.BaseTameChance = preset.BaseTameChance;
        if (component.FavoriteMultiplier == 2.0f && preset.FavoriteMultiplier != 2.0f)
            component.FavoriteMultiplier = preset.FavoriteMultiplier;
        if (component.LikedMultiplier == 1.5f && preset.LikedMultiplier != 1.5f)
            component.LikedMultiplier = preset.LikedMultiplier;
        if (component.DislikedMultiplier == 0.5f && preset.DislikedMultiplier != 0.5f)
            component.DislikedMultiplier = preset.DislikedMultiplier;
        if (component.MinCharisma == 3 && preset.MinCharisma != 3)
            component.MinCharisma = preset.MinCharisma;
        if (component.TamingTime == 3.0f && preset.TamingTime != 3.0f)
            component.TamingTime = preset.TamingTime;
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
    private void OnFollowerUnpaused(Entity<SiriusFollowerComponent> ent, ref EntityUnpausedEvent args)
    {
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
    private void OnTameableGetVerbs(Entity<TameableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == args.Target)
            return;

        if (!HasComp<ActorComponent>(args.User))
            return;

        var tameable = ent.Comp;

        if (TryComp<SiriusFollowerComponent>(ent, out var follower) && follower.IsTamed && follower.Tamer != null)
        {
            if (follower.Tamer == args.User)
                return;

            var ownedVerb = new Verb
            {
                Text = Loc.GetString("taming-verb-already-tamed"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Tame
            };
            args.Verbs.Add(ownedVerb);
            return;
        }

        if (!tameable.CanTame)
            return;

        if (TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState != MobState.Alive)
        {
            var deadVerb = new Verb
            {
                Text = Loc.GetString("taming-verb-dead"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Tame
            };
            args.Verbs.Add(deadVerb);
            return;
        }

        var charisma = _special.GetEffective(args.User, SpecialStat.Charisma);
        if (charisma < tameable.MinCharisma)
        {
            var lowCharismaVerb = new Verb
            {
                Text = Loc.GetString("taming-verb-low-charisma"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Tame,
                Message = Loc.GetString("taming-verb-charisma-needed", ("needed", tameable.MinCharisma))
            };
            args.Verbs.Add(lowCharismaVerb);
            return;
        }

        var heldEntity = GetHeldEntity(args.User);
        if (heldEntity == null)
        {
            var noFoodVerb = new Verb
            {
                Text = Loc.GetString("taming-verb-no-food"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Tame,
                Message = Loc.GetString("taming-verb-need-food")
            };
            args.Verbs.Add(noFoodVerb);
            return;
        }

        if (!TryComp<MetaDataComponent>(heldEntity.Value, out var meta) || meta.EntityPrototype == null)
        {
            var noFoodVerb = new Verb
            {
                Text = Loc.GetString("taming-verb-no-food"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Tame,
                Message = Loc.GetString("taming-verb-need-food")
            };
            args.Verbs.Add(noFoodVerb);
            return;
        }

        var foodId = meta.EntityPrototype.ID;
        if (string.IsNullOrEmpty(foodId))
        {
            var noFoodVerb = new Verb
            {
                Text = Loc.GetString("taming-verb-no-food"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Tame,
                Message = Loc.GetString("taming-verb-need-food")
            };
            args.Verbs.Add(noFoodVerb);
            return;
        }

        var preference = GetFoodPreference(tameable, foodId);
        if (preference == FoodPreference.Unknown)
        {
            var wrongFoodVerb = new Verb
            {
                Text = Loc.GetString("taming-verb-wrong-food"),
                Priority = 0,
                Disabled = true,
                Category = SiriusVerbCategory.Tame,
                Message = Loc.GetString("taming-verb-wrong-food-message")
            };
            args.Verbs.Add(wrongFoodVerb);
            return;
        }
        var user = args.User;
        var target = ent;
        var food = heldEntity.Value;
        var foodIdLocal = foodId;
        var verb = new Verb
        {
            Text = Loc.GetString("taming-verb-tame"),
            Priority = 1,
            Category = SiriusVerbCategory.Tame,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
            Act = () => StartTaming(target, user, food, foodIdLocal)
        };
        args.Verbs.Add(verb);
    }
    private void OnTamingDoAfter(Entity<TameableComponent> ent, ref TamingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (args.FoodEntity == null || args.FoodId == null)
        {
            _popup.PopupEntity(Loc.GetString("taming-failed-no-food"), ent, args.User, PopupType.Small);
            return;
        }

        var foodEntity = GetEntity(args.FoodEntity.Value);
        if (!Exists(foodEntity))
        {
            _popup.PopupEntity(Loc.GetString("taming-failed-no-food"), ent, args.User, PopupType.Small);
            return;
        }

        var tameable = ent.Comp;
        var charisma = _special.GetEffective(args.User, SpecialStat.Charisma);
        var foodPref = GetFoodPreference(tameable, args.FoodId);
        var baseChance = tameable.BaseTameChance;
        var multiplier = GetFoodMultiplier(tameable, foodPref);
        var finalChance = Math.Min(baseChance * multiplier + (charisma - tameable.MinCharisma) * 0.05f, 0.95f);
        Del(foodEntity);
        if (Random.Shared.NextDouble() <= finalChance)
        {
            TameAnimal(ent, args.User);
            _popup.PopupEntity(Loc.GetString("taming-success"), ent, args.User, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("taming-failed"), ent, args.User, PopupType.Small);
        }
    }
    private void GrantPetActions(EntityUid tamer, EntityUid pet, SiriusFollowerComponent follower)
    {
        RemovePetActions(tamer, follower);

        string[] actionPrototypes = new string[]
    {
        "ActionPetFollowStay",
        "ActionPetAttackCancel",
        "ActionPetRelease"
    };
        foreach (var proto in actionPrototypes)
        {
            EntityUid? actionId = null;
            if (_actions.AddAction(tamer, ref actionId, proto))
            {
                follower.PetActionEntities.Add(actionId.Value);
            }
        }
        Dirty(pet, follower);
    }
    private void RemovePetActions(EntityUid tamer, SiriusFollowerComponent follower)
    {
        foreach (var actionId in follower.PetActionEntities)
        {
            _actions.RemoveAction(tamer, actionId);
        }
        follower.PetActionEntities.Clear();
    }
    private void TameAnimal(EntityUid animal, EntityUid tamer)
    {
        if (!TryComp<SiriusFollowerComponent>(animal, out var follower))
            return;
        follower.IsTamed = true;
        follower.Tamer = tamer;
        follower.Commander = tamer;
        follower.IsFollowing = true;
        follower.IsStaying = false;
        follower.WasAutoHeld = false;
        follower.NoPathAccumulator = 0f;
        if (TryComp<HTNComponent>(animal, out var htn))
        {
            if (string.IsNullOrEmpty(follower.OriginalRootTask))
            {
                follower.OriginalRootTask = htn.RootTask.Task;
            }
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
            htn.Blackboard.Remove<EntityUid>("Target");
            htn.Blackboard.Remove<EntityCoordinates>("TargetCoordinates");
            htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
            htn.Blackboard.Remove<bool>("AttackMode");
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.MovementTarget);
            htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
            htn.Blackboard.SetValue(NPCBlackboard.FollowTarget,
                new EntityCoordinates(tamer, System.Numerics.Vector2.Zero));
            htn.RootTask.Task = FollowCompoundId;
            _htn.Replan(htn);
            _npcSystem.WakeNPC(animal, htn);
        }

        if (!TryComp<FactionExceptionComponent>(animal, out var factionException))
            factionException = EnsureComp<FactionExceptionComponent>(animal);
        _npcFaction.IgnoreEntity((animal, factionException), tamer);
        _npcFaction.AddFriendlyEntity(animal, tamer);
        EnsureComp<InputMoverComponent>(animal);
        GrantPetActions(tamer, animal, follower);
        Dirty(animal, follower);
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

    public void ReleasePet(Entity<SiriusFollowerComponent> ent, EntityUid owner)
    {
        var follower = ent.Comp;

        if (follower.Tamer != owner)
            return;

        RemovePetActions(owner, follower);

        if (TryComp<FactionExceptionComponent>(ent, out var factionException))
        {
            foreach (var hostile in factionException.Hostiles)
            {
                _npcFaction.DeAggroEntity((ent, factionException), hostile);
            }
            _npcFaction.UnignoreEntity((ent, factionException), owner);
        }

        _npcFaction.RemoveFriendlyEntity(ent, owner);

        if (TryComp<HTNComponent>(ent, out var htn))
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
        }

        follower.IsTamed = false;
        follower.Tamer = null;
        follower.Commander = null;
        follower.IsFollowing = false;
        follower.IsStaying = false;
        follower.WasAutoHeld = false;
        follower.NoPathAccumulator = 0f;
        Dirty(ent, follower);
        _popup.PopupEntity(Loc.GetString("follower-released"), ent, owner, PopupType.Small);
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
        htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);

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
        htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);

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

    private void StartTaming(Entity<TameableComponent> ent, EntityUid user, EntityUid food, string foodId)
    {
        if (TryComp<SiriusFollowerComponent>(ent, out var follower) && follower.IsTamed)
        {
            _popup.PopupEntity(Loc.GetString("taming-already-tamed"), ent, user, PopupType.Small);
            return;
        }

        var tameable = ent.Comp;
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            tameable.TamingTime,
            new TamingDoAfterEvent(GetNetEntity(food), foodId),
            ent,
            target: ent,
            used: food
        )
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            RequireCanInteract = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
            DistanceThreshold = 2.0f
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private EntityUid? GetHeldEntity(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return null;

        return hands.ActiveHandEntity;
    }

    private FoodPreference GetFoodPreference(TameableComponent tameable, string foodId)
    {
        if (tameable.FavoriteFoods.Contains(foodId))
            return FoodPreference.Favorite;

        if (tameable.LikedFoods.Contains(foodId))
            return FoodPreference.Liked;

        if (tameable.DislikedFoods.Contains(foodId))
            return FoodPreference.Disliked;

        return FoodPreference.Unknown;
    }

    private float GetFoodMultiplier(TameableComponent tameable, FoodPreference preference)
    {
        return preference switch
        {
            FoodPreference.Favorite => tameable.FavoriteMultiplier,
            FoodPreference.Liked => tameable.LikedMultiplier,
            FoodPreference.Disliked => tameable.DislikedMultiplier,
            _ => 0f
        };
    }

    private enum FoodPreference
    {
        Unknown,
        Favorite,
        Liked,
        Disliked
    }

    public enum SiriusFollowerOrderType : byte
    {
        Follow,
        HoldPosition
    }
}
