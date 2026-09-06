using System.Collections.Generic;
using Content.Server.Actions;
using Content.Shared._Misfits.TribalHunt;
using Content.Shared._Misfits.Warcry;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.TribalHunt;

/// <summary>
/// Tribal hunt flow supporting elder-led legendary hunts and tribe-wide minor hunts.
/// Legendary hunts remain elder-only; minor hunts can be started by any tribal participant.
/// </summary>
public sealed class TribalHuntSystem : EntitySystem
{
    private enum TribalHuntStage : byte
    {
        Inactive,
        Gathering,
        Active,
    }

    private enum TribalHuntType : byte
    {
        None,
        Minor,
        Legendary,
    }

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LegendaryCreatureSpawnerSystem _huntSpawner = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private TribalHuntStage _stage = TribalHuntStage.Inactive;
    private TribalHuntType _huntType = TribalHuntType.None;
    private TimeSpan _gatheringEndsAt;
    private TimeSpan _huntEndsAt;
    private TimeSpan _configuredHuntDuration;
    private TimeSpan _configuredRewardDuration;
    private float _configuredRewardSpeedBonus;
    private readonly HashSet<EntityUid> _activeHuntTargets = new();
    private EntityUid? _activeHuntSessionId;
    private EntityUid? _huntCaller;
    private string _targetDepartment = "Tribe";
    private readonly HashSet<EntityUid> _joinedHunters = new();
    private int _requiredTargets;
    private int _defeatedTargets;
    private string _activeTargetName = string.Empty;
    private TimeSpan _lastLocationBroadcast;
    private TimeSpan _lastUiHeartbeat;
    private string _lastKnownCoordinates = string.Empty;
    private bool _hasKnownCoordinates;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TribalHuntLeaderComponent, ComponentStartup>(OnLeaderStartup);
        SubscribeLocalEvent<TribalHuntLeaderComponent, ComponentShutdown>(OnLeaderShutdown);
        SubscribeLocalEvent<TribalHuntLeaderComponent, PerformTribalStartHuntActionEvent>(OnStartLegendaryHuntAction);

        SubscribeLocalEvent<TribalHuntParticipantComponent, ComponentStartup>(OnParticipantStartup);
        SubscribeLocalEvent<TribalHuntParticipantComponent, ComponentShutdown>(OnParticipantShutdown);
        SubscribeLocalEvent<TribalHuntParticipantComponent, PerformTribalStartMinorHuntActionEvent>(OnStartMinorHuntAction);
        SubscribeLocalEvent<TribalHuntParticipantComponent, PerformTribalToggleHuntGuiActionEvent>(OnToggleHuntGuiAction);

        SubscribeLocalEvent<LegendaryCreatureComponent, LegendaryCreatureKilledEvent>(OnLegendaryCreatureKilled);
        SubscribeLocalEvent<LegendaryCreatureComponent, ComponentShutdown>(OnLegendaryCreatureShutdown);
        SubscribeLocalEvent<MinorHuntCreatureComponent, MobStateChangedEvent>(OnMinorHuntCreatureKilled);
        SubscribeLocalEvent<MinorHuntCreatureComponent, ComponentShutdown>(OnMinorHuntCreatureShutdown);
        SubscribeNetworkEvent<TribalHuntJoinRequestEvent>(OnJoinRequest);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_stage == TribalHuntStage.Inactive)
            return;

        if (_stage == TribalHuntStage.Gathering && _timing.CurTime >= _gatheringEndsAt)
        {
            BeginActiveHunt();
            return;
        }

        if (_stage == TribalHuntStage.Active)
        {
            PruneDeletedTargets();

            if (_activeHuntTargets.Count == 0)
            {
                EndHunt(GetFailureText());
                return;
            }

            if (_timing.CurTime >= _huntEndsAt)
            {
                EndHunt(GetFailureText());
                return;
            }

            if (_timing.CurTime >= _lastLocationBroadcast + TimeSpan.FromSeconds(5))
            {
                _lastLocationBroadcast = _timing.CurTime;
                UpdateCreatureLocation();
            }
        }

        if (_timing.CurTime >= _lastUiHeartbeat + TimeSpan.FromSeconds(1))
        {
            _lastUiHeartbeat = _timing.CurTime;

            // Prune hunters that no longer exist or are dead — prevents stale UID accumulation.
            PruneStaleHunters();

            BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
        }
    }

    /// <summary>
    /// Remove deleted or dead entities from the joined hunters set so we don't
    /// waste time sending UI updates or awarding buffs to garbage UIDs.
    /// </summary>
    private void PruneStaleHunters()
    {
        _joinedHunters.RemoveWhere(uid => !Exists(uid) || _mobState.IsDead(uid));
    }

    private void PruneDeletedTargets()
    {
        _activeHuntTargets.RemoveWhere(uid => !Exists(uid));
    }

    private void OnLeaderStartup(EntityUid uid, TribalHuntLeaderComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.StartActionEntity, component.StartAction);
    }

    private void OnLeaderShutdown(EntityUid uid, TribalHuntLeaderComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.StartActionEntity);
    }

    private void OnParticipantStartup(EntityUid uid, TribalHuntParticipantComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.OpenTrackerActionEntity, component.OpenTrackerAction);
        _actions.AddAction(uid, ref component.StartMinorHuntActionEntity, component.StartMinorHuntAction);

        if (_stage != TribalHuntStage.Inactive)
            SendUiUpdate(uid, BuildStatusText());
    }

    private void OnParticipantShutdown(EntityUid uid, TribalHuntParticipantComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.OpenTrackerActionEntity);
        _actions.RemoveAction(uid, component.StartMinorHuntActionEntity);
    }

    private void OnToggleHuntGuiAction(EntityUid uid, TribalHuntParticipantComponent component, PerformTribalToggleHuntGuiActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        RaiseNetworkEvent(new TribalHuntToggleWindowEvent(), actor.PlayerSession);
    }

    private void OnStartLegendaryHuntAction(EntityUid uid, TribalHuntLeaderComponent component, PerformTribalStartHuntActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanLeadHunt(uid, component))
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-cannot-lead"));
            return;
        }

        if (_stage != TribalHuntStage.Inactive)
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-already-active"));
            return;
        }

        StartGathering(
            uid,
            component.TargetDepartment,
            TribalHuntType.Legendary,
            component.HuntDuration,
            component.RewardDuration,
            component.RewardSpeedBonus);
    }

    private void OnStartMinorHuntAction(EntityUid uid, TribalHuntParticipantComponent component, PerformTribalStartMinorHuntActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!IsInDepartment(uid, component.TargetDepartment))
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-cannot-start-minor"));
            return;
        }

        if (_stage != TribalHuntStage.Inactive)
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-already-active"));
            return;
        }

        StartGathering(
            uid,
            component.TargetDepartment,
            TribalHuntType.Minor,
            component.MinorHuntDuration,
            component.MinorRewardDuration,
            component.MinorRewardSpeedBonus);
    }

    private void StartGathering(
        EntityUid caller,
        string targetDepartment,
        TribalHuntType huntType,
        TimeSpan huntDuration,
        TimeSpan rewardDuration,
        float rewardSpeedBonus)
    {
        _stage = TribalHuntStage.Gathering;
        _huntType = huntType;
        _targetDepartment = targetDepartment;
        _huntCaller = caller;
        _activeHuntSessionId = caller;
        _joinedHunters.Clear();
        _joinedHunters.Add(caller);
        _activeHuntTargets.Clear();
        _gatheringEndsAt = _timing.CurTime + TimeSpan.FromMinutes(2);
        _configuredHuntDuration = huntDuration;
        _configuredRewardDuration = rewardDuration;
        _configuredRewardSpeedBonus = rewardSpeedBonus;
        _requiredTargets = 0;
        _defeatedTargets = 0;
        _activeTargetName = string.Empty;
        _lastLocationBroadcast = _timing.CurTime;
        _lastUiHeartbeat = TimeSpan.Zero;
        _lastKnownCoordinates = Loc.GetString("tribal-hunt-gui-coordinate-pending");
        _hasKnownCoordinates = false;

        BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
    }

    private void OnJoinRequest(TribalHuntJoinRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (_stage != TribalHuntStage.Gathering)
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-join-closed"));
            return;
        }

        if (!IsInDepartment(uid, _targetDepartment))
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-not-tribe"));
            return;
        }

        if (!_joinedHunters.Add(uid))
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-already-joined"));
            return;
        }

        BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
    }

    private void BeginActiveHunt()
    {
        if (_stage != TribalHuntStage.Gathering || _huntCaller == null)
        {
            EndHunt(GetFailureText());
            return;
        }

        if (!TryComp(_huntCaller.Value, out TransformComponent? callerXform) || callerXform.MapID == MapId.Nullspace)
        {
            EndHunt(GetFailureText());
            return;
        }

        var callerMapCoords = _transform.GetMapCoordinates(_huntCaller.Value, callerXform);
        _activeHuntTargets.Clear();
        _requiredTargets = 0;
        _defeatedTargets = 0;
        _activeTargetName = string.Empty;

        switch (_huntType)
        {
            case TribalHuntType.Legendary:
                var legendary = _huntSpawner.TrySpawnLegendaryCreature(
                    "N14MobDeathclaw",
                    _activeHuntSessionId ?? _huntCaller.Value,
                    callerMapCoords);

                if (legendary == null)
                {
                    EndHunt(GetFailureText());
                    return;
                }

                _activeHuntTargets.Add(legendary.Value);
                _requiredTargets = 1;
                _activeTargetName = "Deathclaw";
                break;

            case TribalHuntType.Minor:
                var pack = _huntSpawner.TrySpawnMinorHuntPack(
                    _activeHuntSessionId ?? _huntCaller.Value,
                    callerMapCoords,
                    out var creatureName);

                if (pack == null || pack.Count == 0)
                {
                    EndHunt(GetFailureText());
                    return;
                }

                foreach (var uid in pack)
                {
                    if (Exists(uid))
                        _activeHuntTargets.Add(uid);
                }

                if (_activeHuntTargets.Count == 0)
                {
                    EndHunt(GetFailureText());
                    return;
                }

                _requiredTargets = _activeHuntTargets.Count;
                _activeTargetName = creatureName;
                break;

            default:
                EndHunt(GetFailureText());
                return;
        }

        _stage = TribalHuntStage.Active;
        _huntEndsAt = _timing.CurTime + _configuredHuntDuration;
        _lastLocationBroadcast = TimeSpan.Zero;
        UpdateCreatureLocation();
        BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
    }

    private void UpdateCreatureLocation()
    {
        var target = GetTrackedTarget();
        if (target == null)
            return;

        var mapCoords = _transform.GetMapCoordinates(target.Value);
        var position = mapCoords.Position;
        _lastKnownCoordinates = Loc.GetString("tribal-hunt-gui-coordinate-format",
            ("x", MathF.Round(position.X, 1)),
            ("y", MathF.Round(position.Y, 1)));
        _hasKnownCoordinates = true;
    }

    private EntityUid? GetTrackedTarget()
    {
        foreach (var uid in _activeHuntTargets)
        {
            if (Exists(uid))
                return uid;
        }

        return null;
    }

    private void CompleteHunt()
    {
        var expiresAt = _timing.CurTime + _configuredRewardDuration;
        var participants = EntityQueryEnumerator<TribalHuntParticipantComponent>();

        while (participants.MoveNext(out var uid, out _))
        {
            if (!_joinedHunters.Contains(uid))
                continue;

            if (!IsInDepartment(uid, _targetDepartment))
                continue;

            if (_mobState.IsDead(uid) || !HasComp<MovementSpeedModifierComponent>(uid))
                continue;

            var buff = EnsureComp<WarcryBuffComponent>(uid);
            buff.SpeedBonus = Math.Max(buff.SpeedBonus, _configuredRewardSpeedBonus);
            if (expiresAt > buff.ExpiresAt)
                buff.ExpiresAt = expiresAt;

            Dirty(uid, buff);
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
        }

        var completionText = _huntType == TribalHuntType.Minor
            ? Loc.GetString("tribal-hunt-popup-minor-complete",
                ("seconds", (int) Math.Ceiling(_configuredRewardDuration.TotalSeconds)))
            : Loc.GetString("tribal-hunt-popup-complete",
                ("seconds", (int) Math.Ceiling(_configuredRewardDuration.TotalSeconds)));

        EndHunt(completionText, cleanupTargets: false);
    }

    private void EndHunt(string statusText, bool cleanupTargets = true)
    {
        var department = _targetDepartment;
        var targets = new List<EntityUid>(_activeHuntTargets);

        _stage = TribalHuntStage.Inactive;
        _huntType = TribalHuntType.None;
        _activeHuntTargets.Clear();
        _activeHuntSessionId = null;
        _huntCaller = null;
        _requiredTargets = 0;
        _defeatedTargets = 0;
        _activeTargetName = string.Empty;
        _lastKnownCoordinates = string.Empty;
        _hasKnownCoordinates = false;
        _joinedHunters.Clear();

        if (cleanupTargets)
        {
            foreach (var target in targets)
            {
                if (Exists(target))
                    QueueDel(target); // Deferred delete — prevents physics-state churn mid-iteration.
            }
        }

        BroadcastUiToDepartment(department, statusText);
    }

    private string BuildStatusText()
    {
        return (_stage, _huntType) switch
        {
            (TribalHuntStage.Gathering, TribalHuntType.Minor) => Loc.GetString("tribal-hunt-gui-status-gathering-minor"),
            (TribalHuntStage.Gathering, _) => Loc.GetString("tribal-hunt-gui-status-gathering"),
            (TribalHuntStage.Active, TribalHuntType.Minor) => Loc.GetString("tribal-hunt-gui-status-active-minor", ("prey", _activeTargetName)),
            (TribalHuntStage.Active, _) => Loc.GetString("tribal-hunt-gui-status-active"),
            _ => Loc.GetString("tribal-hunt-gui-status-idle"),
        };
    }

    private string GetFailureText()
    {
        return _huntType == TribalHuntType.Minor
            ? Loc.GetString("tribal-hunt-popup-minor-failed")
            : Loc.GetString("tribal-hunt-popup-failed");
    }

    private bool CanLeadHunt(EntityUid uid, TribalHuntLeaderComponent component)
    {
        if (!IsInDepartment(uid, component.TargetDepartment))
            return false;

        if (component.ActivatorJobs == null || component.ActivatorJobs.Count == 0)
            return true;

        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out _, out var prototype))
            return false;

        return component.ActivatorJobs.Contains(prototype.ID);
    }

    // #Misfits Fix - resolved against the target department's role list so dual-citizenship tribe jobs
    // (SuperMutantTribal, SyntheticProtectronTribal) count as tribe members for hunts.
    private bool IsInDepartment(EntityUid uid, string departmentId)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out _, out var jobPrototype))
            return false;

        return _prototypes.TryIndex<DepartmentPrototype>(departmentId, out var department)
            && department.Roles.Contains(jobPrototype.ID);
    }

    private void BroadcastUiToDepartment(string departmentId, string statusText)
    {
        if (_stage == TribalHuntStage.Active)
        {
            // During Active hunt, only joined hunters need updates — skip the expensive
            // full participant query + IsInDepartment call (3 component lookups each).
            foreach (var uid in _joinedHunters)
            {
                SendUiUpdate(uid, statusText);
            }
            return;
        }

        // During Gathering (or other stages), broadcast to all tribe participants
        // so they can see the join window.
        var query = EntityQueryEnumerator<TribalHuntParticipantComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!IsInDepartment(uid, departmentId))
                continue;

            SendUiUpdate(uid, statusText);
        }
    }

    private void SendUiUpdate(EntityUid uid, string statusText)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        if (_mobState.IsDead(uid))
            return;

        var remaining = _stage switch
        {
            TribalHuntStage.Gathering => Math.Max(0, (int) Math.Ceiling((_gatheringEndsAt - _timing.CurTime).TotalSeconds)),
            TribalHuntStage.Active => Math.Max(0, (int) Math.Ceiling((_huntEndsAt - _timing.CurTime).TotalSeconds)),
            _ => 0,
        };

        var isJoined = _joinedHunters.Contains(uid);

        // Only call IsInDepartment for the CanJoin flag during Gathering — during Active,
        // all recipients are already validated joined hunters so skip the 3x component lookup.
        var canJoin = _stage == TribalHuntStage.Gathering
                      && !isJoined
                      && IsInDepartment(uid, _targetDepartment);

        var state = new TribalHuntUiState
        {
            Active = _stage == TribalHuntStage.Active,
            Offered = _defeatedTargets,
            Required = _requiredTargets,
            SecondsRemaining = remaining,
            StatusText = statusText,
            CoordinatesKnown = _stage == TribalHuntStage.Active && _hasKnownCoordinates,
            CoordinatesText = _lastKnownCoordinates,
            JoinWindowOpen = _stage == TribalHuntStage.Gathering,
            CanJoin = canJoin,
            IsJoined = isJoined,
            JoinedHunters = _joinedHunters.Count,
        };

        RaiseNetworkEvent(new TribalHuntUiUpdateEvent { State = state }, actor.PlayerSession);
    }

    private void OnLegendaryCreatureKilled(EntityUid uid, LegendaryCreatureComponent component, LegendaryCreatureKilledEvent args)
    {
        if (_stage != TribalHuntStage.Active || _huntType != TribalHuntType.Legendary || !_activeHuntTargets.Remove(uid))
            return;

        _defeatedTargets = _requiredTargets;
        CompleteHunt();
    }

    private void OnLegendaryCreatureShutdown(EntityUid uid, LegendaryCreatureComponent component, ComponentShutdown args)
    {
        if (_stage != TribalHuntStage.Active || _huntType != TribalHuntType.Legendary || !_activeHuntTargets.Contains(uid))
            return;

        EndHunt(GetFailureText(), cleanupTargets: false);
    }

    private void OnMinorHuntCreatureKilled(EntityUid uid, MinorHuntCreatureComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (_stage != TribalHuntStage.Active || _huntType != TribalHuntType.Minor || !_activeHuntTargets.Remove(uid))
            return;

        _defeatedTargets = Math.Min(_requiredTargets, _defeatedTargets + 1);

        if (_activeHuntTargets.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(component.CreatureName))
                _activeTargetName = component.CreatureName;

            CompleteHunt();
            return;
        }

        BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
    }

    private void OnMinorHuntCreatureShutdown(EntityUid uid, MinorHuntCreatureComponent component, ComponentShutdown args)
    {
        if (_stage != TribalHuntStage.Active || _huntType != TribalHuntType.Minor || !_activeHuntTargets.Contains(uid))
            return;

        EndHunt(GetFailureText(), cleanupTargets: false);
    }
}
