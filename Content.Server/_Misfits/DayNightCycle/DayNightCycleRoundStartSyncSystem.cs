// #Misfits Add - Syncs the day/night cycle to round start so every round begins at dawn
// (cycle time 0) and the 4-hour cycle lines up exactly with the round (round end = dawn again).
using Content.Server.GameTicking;
using Content.Shared._NC14.DayNightCycle;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.DayNightCycle;

/// <summary>
/// On round start, stamps every <see cref="DayNightCycleComponent"/> with the server's current
/// <see cref="IGameTiming.CurTime"/> so the client-side cycle phase resets to dawn for the new round
/// (rounds are typically 4 hours, matching <see cref="DayNightCycleComponent.CycleDurationMinutes"/>).
/// </summary>
public sealed class DayNightCycleRoundStartSyncSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        var timestamp = (float) _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<DayNightCycleComponent>();
        while (query.MoveNext(out var uid, out var dayNight))
        {
            dayNight.RoundStartTimeSeconds = timestamp;
            // #Misfits Fix - RoundStartTimeSeconds is an AutoNetworkedField that
            // was mutated without a Dirty, so clients already present at round
            // start kept the previous (stale, usually zero) timestamp and
            // computed the cycle phase from the full server CurTime instead of
            // the round start.
            Dirty(uid, dayNight);
        }
    }
}
