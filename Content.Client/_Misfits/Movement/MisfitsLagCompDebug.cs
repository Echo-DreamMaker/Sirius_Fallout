

using System.Diagnostics;
using Content.Shared._Misfits.Movement;
using Content.Shared.Weapons.Ranged.Systems;

using static Content.Shared.Weapons.Ranged.Systems.SharedGunSystem;
namespace Content.Client._Misfits.Movement;

public sealed partial class MisfitsLagCompensationSystem : SharedMisfitsLagCompensationSystem
{
    //[Conditional("DEBUG")]
    public bool DebugPredictTick()
    {
        Log.Debug($"Latest Prediction Tick!!! {LatestPredictedTick}");
        return true;
    }
    //[Conditional("DEBUG")]
    public bool DebugPredictHandling(uint lastConfirmedTick, double lagTickCount)
    {
        Log.Debug($"Handling state LastConfirmedTick: {lastConfirmedTick}");
        //Log.Debug($"Latest Prediction Tick!!! {latestPredictedTick}");
        Log.Debug($"Lag Tolerance: {lagTickCount}");
        return true;
    }
    //[Conditional("DEBUG")]
    public bool DebugPredictResetBack(BallisticAmmoState curstate, AmmoProviderDirtyEvent predictedState)
    {
        Log.Debug($"Previous tick: {curstate.FromTick} Has been applied");
        Log.Debug($"diff of {Math.Abs(_clientTiming.CurTick.Value - predictedState.Tick)} vs tolerance: {TickTolerance * 10}");
        Log.Debug($"earliest predicted tick is: {predictedState.Tick}");
        Log.Debug($"Last confirmed now is: {curstate.FromTick}");
        Log.Debug($"Resetting");
        return true;
    }
    //[Conditional("DEBUG")]
    public bool DebugPredictWait()
    {
        Log.Debug($"Waiting for Next server state");
        return true;
    }
    //[Conditional("DEBUG")]
    public bool DebugPredictSuccess(BallisticAmmoState nextState)
    {
        Log.Debug($"Predicted tick: {nextState.FromTick}");
        Log.Debug($"Increasing Last confirmed: {nextState.FromTick}");
        Log.Debug($"Had this many predictions left: {PredictTicks.Count}");
        return true;
    }
    //[Conditional("DEBUG")]
    public bool DebugPredictResetServer(BallisticAmmoState nextState)
    {
        Log.Debug($"CATCHING UP TO NEXT SERVER STATE!! {nextState.FromTick}");
        Log.Debug($"Increasing Last confirmed: {nextState.FromTick}");
        return true;
    }
    public bool DebugPredictNewEnt()
    {
        Log.Debug("new ent. clearing old predicted ticks");
        return true;
    }

}
