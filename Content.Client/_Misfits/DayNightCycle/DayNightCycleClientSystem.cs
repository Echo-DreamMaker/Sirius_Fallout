// #Misfits Change - Client-side day/night cycle rendering system.
// Uses IGameTiming.CurTime for deterministic, smooth ambient-light transitions
// without any server-to-client dirty spam.
using Content.Shared._NC14.DayNightCycle;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.DayNightCycle;

/// <summary>
/// Applies the day/night colour cycle to <see cref="MapLightComponent.AmbientLightColor"/>
/// every frame, driven entirely by <see cref="IGameTiming.CurTime"/> so that the result
/// is deterministic and jitter-free regardless of server tick rate.
/// </summary>
public sealed class DayNightCycleClientSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DayNightCycleComponent, MapLightComponent>();
        while (query.MoveNext(out _, out var dayNight, out var mapLight))
        {
            var cycleDurationSeconds = dayNight.CycleDurationMinutes * 60f;

            // #Misfits Change - Anchor the cycle to the round start time (set server-side on
            // RoundStartedEvent) so each round begins at cycle time 0 = dawn, and the cycle lines
            // up exactly with the round (4h cycle = 4h round, round end = dawn again).
            // Offset by StartOffset afterwards so it still supports a startup phase shift.
            var elapsedSeconds = (float) _timing.CurTime.TotalSeconds - dayNight.RoundStartTimeSeconds;
            if (elapsedSeconds < 0f)
                elapsedSeconds = 0f; // Before the first round start — no offset yet.

            var offsetSeconds = dayNight.StartOffset * cycleDurationSeconds;
            var rawSeconds = elapsedSeconds + offsetSeconds;

            var cycleTime = (rawSeconds % cycleDurationSeconds) / cycleDurationSeconds;

            var color = DayNightCycleSystem.GetInterpolatedColor(dayNight, cycleTime);

            // Misfits Fix: only write when the color actually changes to avoid redundant
            // state assignments every tick while the map light hasn't visually shifted.
            if (color != mapLight.AmbientLightColor)
                mapLight.AmbientLightColor = color;
        }
    }
}
