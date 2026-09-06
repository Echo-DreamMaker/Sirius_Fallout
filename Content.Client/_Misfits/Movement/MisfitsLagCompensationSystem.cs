using Content.Shared._Misfits.Movement;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Timing;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Shared.Weapons.Ranged.Systems.SharedGunSystem;

namespace Content.Client._Misfits.Movement;

/// <summary>
/// Client-side lag compensation system. Reads the engine's last-confirmed real tick via
/// <see cref="IClientGameTiming.LastRealTick"/> and exposes it for client prediction code
/// (gun fire, action use) to stamp onto outgoing events.
///
/// The stamped tick is piggybacked on <c>RequestShootEvent</c> and <c>RequestPerformActionEvent</c>
/// which are already sent as predictive events — no separate periodic message is needed,
/// avoiding the "Got late MsgEntity" warning caused by tick-stamped entity events on a timer.
/// </summary>
public sealed partial class MisfitsLagCompensationSystem : SharedMisfitsLagCompensationSystem
{
    [Dependency] private IClientGameTiming _clientTiming = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IClientNetManager _netManager = default!;

    private readonly Dictionary<EntityUid, Queue<(GameTick Tick, EntityCoordinates Coordinates, Angle Angle)>> _positions = new();

    /// <summary>
    /// Returns the client's last confirmed engine tick. Stamp this onto any outgoing
    /// event that the server will use for lag-compensated range validation.
    /// </summary>
    public GameTick GetLastRealTick() => _clientTiming.LastRealTick;
    public Queue<AmmoProviderDirtyEvent> PredictTicks = default!;
    public uint LastConfirmedTick = 0;
    public uint LatestPredictedTick = 0;
    public const short TickTolerance = 11;

    public override void Initialize()
    {
        base.Initialize();
        PredictTicks = new Queue<AmmoProviderDirtyEvent>(10);

        _transform.OnGlobalMoveEvent += OnGlobalMove;
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<AmmoProviderDirtyEvent>(OnDirtyQueue);
        SubscribeLocalEvent<OnCompHandling>(OnHandleStateCheck);


    }
    // stolen from NcrIventSystem lawl. Might make this and other things into their
    // own like "helper" class to avoid repeating code
    public bool IsHeldInHands(EntityUid user, EntityUid item)
    {
        if (!TryComp(user, out HandsComponent? hands))
            return false;
        // entityCoords changes EntityId to nearest valid parent(ie... not in container, not held ect...)
        var parent = Transform(item).Coordinates.EntityId;
        if (parent == user) return true;

        foreach (var hand in hands.Hands.Values)
            if (hand.HeldEntity == parent)
                return true;

        return false;
    }
    public void OnDirtyQueue(ref AmmoProviderDirtyEvent ev)
    {

        if (ev.User is null || !IsHeldInHands(ev.User.Value, ev.Gun) || !_clientTiming.IsFirstTimePredicted)
            return;

        if (PredictTicks.TryPeek(out var prev) && prev.Gun.Id != ev.Gun.Id)
        {
            PredictTicks.Clear();
            DebugTools.Assert(DebugPredictNewEnt());
        }

        LatestPredictedTick = ev.Tick;
        DebugTools.Assert(DebugPredictTick());
        PredictTicks.Enqueue(ev);
    }
    private const int TotalResetMult = 4;
    private const int PredictionTolerance = 2;
    // TODO: very wip
    public void OnHandleStateCheck(OnCompHandling ev)
    {
        // cur not null, next null
        // Default reset from predicted state when given only current state
        // only switch back to cur if we are predicting too far
        // ie. the last confirmed state
        var ping = (_netManager.ServerChannel?.Ping ?? 0) / 1000f; // seconds.
        var lagTickCount = Math.Ceiling(_clientTiming.TickRate * ping / _clientTiming.TimeScale);

        var cur = ev.Cur;
        var next = ev.Next;
        DebugTools.Assert(DebugPredictHandling(LastConfirmedTick, lagTickCount));

        if (cur is BallisticAmmoState curstate && next is null
            && !PredictTicks.TryPeek(out var predictedState) &&
            Math.Abs(LatestPredictedTick - LastConfirmedTick) >= lagTickCount * TotalResetMult)
        {
            DebugTools.Assert(DebugPredictResetBack(curstate, predictedState));
            // reset
            LastConfirmedTick = LatestPredictedTick = curstate.FromTick;
            PredictTicks.Clear();
            ev.StateToApply = curstate;

            return;
        }
        // null check/early exit cases when cur is not null and next null, but we reject cur
        // also if we somehow got a late state but we already confirmed a later tick is correct
        // we ignore it
        if (next is not BallisticAmmoState nextState || nextState.FromTick < LastConfirmedTick)
        {
            DebugTools.Assert(DebugPredictWait());
            return;
        }

        // did we already predict next? Search sequentially
        while (PredictTicks.TryDequeue(out var state))
        {
            //lagTickCount
            //state.Tick > nextState.FromTick)
            if (Equals(state, nextState) && Math.Abs(state.Tick - nextState.FromTick) <= lagTickCount * PredictionTolerance)
            {
                DebugTools.Assert(DebugPredictSuccess(nextState));
                LastConfirmedTick = nextState.FromTick;
                return;
            }
        }
        // we didnt get any
        ev.StateToApply = nextState;
        LastConfirmedTick = LatestPredictedTick = nextState.FromTick;

        DebugTools.Assert(DebugPredictResetServer(nextState));
        return;
    }
    private static bool Equals(AmmoProviderDirtyEvent predictedState, BallisticAmmoState recievedState)
    {
        return
                        predictedState.AmmoIndex == recievedState.CurIndex &&
                        predictedState.AmmoSpawned == recievedState.SpawnedCountPredict &&
                        predictedState.AmmoUnspawned == recievedState.UnspawnedCount;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _transform.OnGlobalMoveEvent -= OnGlobalMove;
        _positions.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var bufferTicks = Math.Max(1, (int) Math.Ceiling(MaxCompensationMs / 1000f * _clientTiming.TickRate)) + 2;
        var earliestTick = _clientTiming.CurTick - (uint) bufferTicks;

        foreach (var history in _positions.Values)
        {
            while (history.TryPeek(out var pos) && pos.Tick < earliestTick)
            {
                history.Dequeue();
            }
        }
    }

    private void OnGlobalMove(ref MoveEvent args)
    {
        if (!args.NewPosition.EntityId.IsValid())
            return;

        var history = _positions.GetValueOrDefault(args.Sender);
        if (history == null)
        {
            history = new Queue<(GameTick Tick, EntityCoordinates Coordinates, Angle Angle)>();
            _positions[args.Sender] = history;
        }

        history.Enqueue((_clientTiming.CurTick, args.NewPosition, args.NewRotation));
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        _positions.Remove(args.Entity);
    }

    public override GameTick GetLastRealTick(ICommonSession? session)
    {
        return _clientTiming.LastRealTick;
    }

    public override (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid,
        GameTick tick,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform, false))
            return (EntityCoordinates.Invalid, Angle.Zero);

        if (!_positions.TryGetValue(uid, out var history) || history.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        var coordinates = xform.Coordinates;
        var angle = xform.LocalRotation;
        var found = false;

        foreach (var pos in history)
        {
            coordinates = pos.Coordinates;
            angle = pos.Angle;
            found = true;

            if (pos.Tick >= tick)
                break;
        }

        if (!found)
            return (xform.Coordinates, xform.LocalRotation);

        return (coordinates, angle);
    }
}
