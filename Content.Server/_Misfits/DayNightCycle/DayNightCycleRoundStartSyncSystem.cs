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
        while (query.MoveNext(out _, out var dayNight))
        {
            dayNight.RoundStartTimeSeconds = timestamp;
        }
    }
}
