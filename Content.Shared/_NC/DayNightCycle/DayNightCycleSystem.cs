// #Misfits Change - Reworked to use IGameTiming for deterministic, jitter-free day/night cycle
// Time is computed from absolute game time on the client; no per-frame dirty calls.
using System.Linq;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._NC14.DayNightCycle
{
    public sealed class DayNightCycleSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DayNightCycleComponent, MapInitEvent>(OnMapInit);
        }

        private void OnMapInit(EntityUid uid, DayNightCycleComponent component, MapInitEvent args)
        {
            if (component.TimeEntries.Count < 2)
            {
                // #Misfits Change - Fallback curve kept in sync with DayNightCycleComponent defaults:
                // 4h dawn-to-dawn cycle (0 = dawn/round start, 1 = dawn/round end, darker nights).
                component.TimeEntries = new List<TimeEntry>
                {
                    new() { Time = 0.00f, ColorHex = "#A87A4E" },  // Dawn (round start)
                    new() { Time = 0.06f, ColorHex = "#CDA06C" },  // Sunrise
                    new() { Time = 0.15f, ColorHex = "#E2BE89" },  // Early morning
                    new() { Time = 0.25f, ColorHex = "#EED3A0" },  // Morning
                    new() { Time = 0.375f, ColorHex = "#F7DDB0" }, // Late morning
                    new() { Time = 0.50f, ColorHex = "#FAE3B8" },  // Noon (peak)
                    new() { Time = 0.56f, ColorHex = "#EFD09A" },  // Early afternoon
                    new() { Time = 0.63f, ColorHex = "#D6AC74" },  // Late afternoon
                    new() { Time = 0.66f, ColorHex = "#9E6C45" },  // Sunset
                    new() { Time = 0.71f, ColorHex = "#5C4650" },  // Twilight
                    new() { Time = 0.75f, ColorHex = "#372C40" },  // Night falls
                    new() { Time = 0.82f, ColorHex = "#241C30" },  // Night
                    new() { Time = 0.89f, ColorHex = "#151021" }, // Deep night
                    new() { Time = 0.94f, ColorHex = "#2B2234" },  // Pre-dawn first light
                    new() { Time = 0.965f, ColorHex = "#523A3A" },  // Dawn glow
                    new() { Time = 0.985f, ColorHex = "#7E5C3C" }, // Dawn
                    new() { Time = 1.00f, ColorHex = "#A87A4E" }   // Full dawn (round end)
                };
            }
        }

        /// <summary>
        /// Returns the interpolated ambient light color for <paramref name="time"/> (0–1 normalized
        /// position within the cycle). Used by the client-side rendering system.
        /// </summary>
        public static Color GetInterpolatedColor(DayNightCycleComponent component, float time)
        {
            var entries = component.TimeEntries;

            for (int i = 0; i < entries.Count - 1; i++)
            {
                if (time >= entries[i].Time && time <= entries[i + 1].Time)
                {
                    var t = (time - entries[i].Time) / (entries[i + 1].Time - entries[i].Time);
                    return InterpolateHexColors(entries[i].ColorHex, entries[i + 1].ColorHex, t);
                }
            }

            // Wrap between the last and first entry
            var lastEntry = entries.Last();
            var firstEntry = entries.First();
            var wrappedT = (time - lastEntry.Time) / (1f + firstEntry.Time - lastEntry.Time);
            return InterpolateHexColors(lastEntry.ColorHex, firstEntry.ColorHex, wrappedT);
        }

        private static Color InterpolateHexColors(string hexColor1, string hexColor2, float t)
        {
            var color1 = Color.FromHex(hexColor1);
            var color2 = Color.FromHex(hexColor2);
            return new Color(
                color1.R + (color2.R - color1.R) * t,
                color1.G + (color2.G - color1.G) * t,
                color1.B + (color2.B - color1.B) * t);
        }
    }
}
