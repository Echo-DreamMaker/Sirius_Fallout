// #Misfits Add - one source of truth for the durability condition wording and
// colour ramp, shared by the examine readout and the weapon assembly station UI.
namespace Content.Shared._Sirius.Weapons.Durability;

public static class WeaponDurabilityCondition
{
    /// <summary>
    /// Localisation key for a condition ratio, from pristine (state-1) down to
    /// the worst state (state-7).
    /// </summary>
    public static string StateKey(float ratio)
    {
        if (ratio >= 0.999f)
            return "misfits-weapon-durability-state-1";
        if (ratio >= 0.90f)
            return "misfits-weapon-durability-state-2";
        if (ratio >= 0.75f)
            return "misfits-weapon-durability-state-3";
        if (ratio >= 0.50f)
            return "misfits-weapon-durability-state-4";
        if (ratio >= 0.30f)
            return "misfits-weapon-durability-state-5";
        if (ratio >= 0.15f)
            return "misfits-weapon-durability-state-6";
        return "misfits-weapon-durability-state-7";
    }

    /// <summary>
    /// Colour for a condition ratio: dark red (worst) up through red, orange,
    /// yellow, light green and green to azure (best). Mirrors <see cref="StateKey"/>.
    /// </summary>
    public static string StateColor(float ratio)
    {
        if (ratio >= 0.999f)
            return "#33A9FF"; // azure
        if (ratio >= 0.90f)
            return "#32CD32"; // green
        if (ratio >= 0.75f)
            return "#90EE90"; // light green
        if (ratio >= 0.50f)
            return "#FFD700"; // yellow
        if (ratio >= 0.30f)
            return "#FF8C00"; // orange
        if (ratio >= 0.15f)
            return "#E53935"; // red
        return "#8B0000";     // dark red
    }
}
