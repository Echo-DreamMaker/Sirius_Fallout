// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
// Ported to misfits-14 _MultiZ/
// #Cythisiax Ported — Multi-Z PVS expansion for adjacent levels

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._MultiZ;
using Content.Shared._MultiZ.Core.Components;
using Content.Shared._MultiZ.Core.EntitySystems;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._MultiZ.Core;

/// <summary>
/// Expands a player's PVS to include the current Z-level and its immediate neighbors.
/// This keeps the rendered adjacent level populated with entities rather than just map art.
/// </summary>
public sealed partial class MZPvsSystem : MZSharedSystem
{
    private const float RelayMoveThreshold = 0.5f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    private readonly HashSet<ICommonSession> _attachedSessions = new();
    private readonly HashSet<ICommonSession> _trackedSessions = new();
    private readonly Dictionary<ICommonSession, EntityUid> _lowerViewRelays = new();
    private readonly Queue<ICommonSession> _refreshQueue = new();
    private readonly Dictionary<EntityUid, EntityUid> _gridMaps = new();
    private readonly Dictionary<EntityUid, int> _gridCountsByMap = new();
    private readonly HashSet<EntityUid> _knownGridMaps = new();

    private float _refreshBudget;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ActorComponent, EntParentChangedMessage>(OnActorParentChanged);
        SubscribeLocalEvent<GridStartupEvent>(OnGridStartup);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
        SubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>(OnGridParentChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var probeHz = _cfg.GetCVar(MZCVars.ProbeUpdateHz);
        if (probeHz <= 0f)
        {
            ClearAllRelays();
            _refreshBudget = 0f;
            return;
        }

        if (_trackedSessions.Count == 0)
        {
            _refreshBudget = 0f;
            return;
        }

        _refreshBudget += frameTime * probeHz * _trackedSessions.Count;
        var refreshCount = Math.Min((int) _refreshBudget, _refreshQueue.Count);
        if (refreshCount == 0)
            return;

        _refreshBudget -= refreshCount;
        for (var i = 0; i < refreshCount; i++)
        {
            var session = _refreshQueue.Dequeue();
            if (!_trackedSessions.Contains(session))
                continue;

            if (session.Status == SessionStatus.Disconnected)
            {
                ClearSession(session);
                _trackedSessions.Remove(session);
                RemoveFromRefreshQueue(session);
                continue;
            }

            RefreshSession(session);
            _refreshQueue.Enqueue(session);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        _attachedSessions.Add(ev.Player);
        RefreshSession(ev.Player);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        _attachedSessions.Remove(ev.Player);
        StopTracking(ev.Player);
    }

    private void OnActorParentChanged(Entity<ActorComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshSession(ent.Comp.PlayerSession);
    }

    private void OnGridStartup(GridStartupEvent args)
    {
        UpdateGridMap(args.EntityUid, Transform(args.EntityUid).MapUid);
    }

    private void OnGridRemoved(GridRemovalEvent args)
    {
        UpdateGridMap(args.EntityUid, null);
    }

    private void OnGridParentChanged(Entity<MapGridComponent> ent, ref EntParentChangedMessage args)
    {
        UpdateGridMap(ent.Owner, args.Transform.MapUid);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ClearAllRelays();
        _attachedSessions.Clear();
        _trackedSessions.Clear();
        _refreshQueue.Clear();
        _refreshBudget = 0f;
    }

    /// <summary>
    /// Immediately reconciles a session's lower-map relay. Vehicles call this after moving a
    /// buckled pilot's ancestor between maps, which does not change the pilot's direct parent.
    /// </summary>
    public void RefreshSession(ICommonSession session)
    {
        if (session.AttachedEntity is not { Valid: true } attached ||
            !TryComp(attached, out TransformComponent? xform) ||
            xform.MapUid is not { } mapUid ||
            !TryComp<MZMapComponent>(mapUid, out var zMap))
        {
            StopTracking(session);
            return;
        }

        if (HasRenderableGrids(mapUid) ||
            !TryMapDown((mapUid, zMap), out var belowMap) ||
            !TryComp<MapComponent>(belowMap.Value.Owner, out var belowMapComp))
        {
            StopTracking(session);
            return;
        }

        StartTracking(session);

        if (_cfg.GetCVar(MZCVars.ProbeUpdateHz) <= 0f)
            return;

        var playerPos = _transform.GetMapCoordinates(xform).Position;
        var pvsScale = 1f;
        if (TryComp<EyeComponent>(attached, out var eye))
        {
            playerPos += eye.Offset;
            pvsScale = eye.PvsScale;
        }

        var relayCoords = new MapCoordinates(playerPos, belowMapComp.MapId);

        EnsureLowerViewRelay(session, relayCoords, pvsScale);
    }

    private void EnsureLowerViewRelay(ICommonSession session, MapCoordinates coordinates, float pvsScale)
    {
        if (_lowerViewRelays.TryGetValue(session, out var relay) &&
            !TerminatingOrDeleted(relay))
        {
            SetRelayPvsScale(relay, pvsScale);
            var relayCoordinates = _transform.GetMapCoordinates(relay);
            if (relayCoordinates.MapId == coordinates.MapId &&
                Vector2.DistanceSquared(relayCoordinates.Position, coordinates.Position) <
                RelayMoveThreshold * RelayMoveThreshold)
            {
                return;
            }

            _transform.SetMapCoordinates(relay, coordinates);
            return;
        }

        relay = Spawn(null, coordinates);
        SetRelayPvsScale(relay, pvsScale);
        _lowerViewRelays[session] = relay;
        _viewSubscriber.AddViewSubscriber(relay, session);
    }

    private void SetRelayPvsScale(EntityUid relay, float pvsScale)
    {
        var relayEye = EnsureComp<EyeComponent>(relay);
        _eye.SetPvsScale((relay, relayEye), MathF.Max(pvsScale, 1f));
    }

    private void StartTracking(ICommonSession session)
    {
        if (_trackedSessions.Add(session))
            _refreshQueue.Enqueue(session);
    }

    private void StopTracking(ICommonSession session)
    {
        _trackedSessions.Remove(session);
        RemoveFromRefreshQueue(session);
        ClearSession(session);
    }

    private void ClearSession(ICommonSession session)
    {
        if (!_lowerViewRelays.Remove(session, out var relay))
            return;

        _viewSubscriber.RemoveViewSubscriber(relay, session);

        if (!TerminatingOrDeleted(relay))
            QueueDel(relay);
    }

    private void ClearAllRelays()
    {
        foreach (var session in _lowerViewRelays.Keys.ToArray())
            ClearSession(session);
    }

    private void RemoveFromRefreshQueue(ICommonSession session)
    {
        var count = _refreshQueue.Count;
        for (var i = 0; i < count; i++)
        {
            var queued = _refreshQueue.Dequeue();
            if (queued != session)
                _refreshQueue.Enqueue(queued);
        }
    }

    private bool HasRenderableGrids(EntityUid mapUid)
    {
        // Grid startup normally populates the cache. Scan once as a defensive fallback for maps that existed before
        // this system initialized; subsequent probes are constant-time, including for intentionally empty sky maps.
        if (_knownGridMaps.Add(mapUid))
        {
            var count = 0;
            var query = EntityQueryEnumerator<TransformComponent, MapGridComponent>();
            while (query.MoveNext(out var grid, out var xform, out _))
            {
                if (xform.MapUid != mapUid)
                    continue;

                _gridMaps[grid] = mapUid;
                count++;
            }

            if (count > 0)
                _gridCountsByMap[mapUid] = count;
        }

        return _gridCountsByMap.GetValueOrDefault(mapUid) > 0;
    }

    private void UpdateGridMap(EntityUid grid, EntityUid? newMap)
    {
        if (_gridMaps.TryGetValue(grid, out var oldMap))
        {
            if (newMap == oldMap)
                return;

            _gridMaps.Remove(grid);
            if (_gridCountsByMap.TryGetValue(oldMap, out var oldCount))
            {
                if (oldCount <= 1)
                    _gridCountsByMap.Remove(oldMap);
                else
                    _gridCountsByMap[oldMap] = oldCount - 1;
            }

            RefreshAttachedSessionsOnMap(oldMap);
        }

        if (newMap is not { Valid: true } map)
            return;

        _knownGridMaps.Add(map);
        _gridMaps[grid] = map;
        _gridCountsByMap[map] = _gridCountsByMap.GetValueOrDefault(map) + 1;
        RefreshAttachedSessionsOnMap(map);
    }

    private void RefreshAttachedSessionsOnMap(EntityUid mapUid)
    {
        foreach (var session in _attachedSessions.ToArray())
        {
            if (session.AttachedEntity is not { Valid: true } attached ||
                !TryComp(attached, out TransformComponent? xform) ||
                xform.MapUid != mapUid)
            {
                continue;
            }

            RefreshSession(session);
        }
    }
}
