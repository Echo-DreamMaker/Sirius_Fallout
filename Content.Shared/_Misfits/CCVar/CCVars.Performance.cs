using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Misfits.CCVar;

/// <summary>
/// CVars for Misfits performance systems: lag compensation, NPC proximity wake, etc.
/// </summary>
[CVarDefs]
public sealed class PerformanceCVars : CVars
{
    /// <summary>
    /// Maximum lag compensation window in milliseconds.
    /// Also controls how much historical movement data the authoritative rewind buffer retains.
    /// Actions and shots from clients this many ms behind the server are still accepted,
    /// with <see cref="LagCompensationMarginTiles"/> added to their range checks.
    /// </summary>
    public static readonly CVarDef<int> LagCompensationMs =
        CVarDef.Create("misfits.lag_compensation_ms", 750, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Extra range margin (tiles) applied during lag-compensated range checks when a
    /// client's last confirmed tick is behind the current server tick.
    /// </summary>
    public static readonly CVarDef<float> LagCompensationMarginTiles =
        CVarDef.Create("misfits.lag_compensation_margin_tiles", 0.35f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Enables client side gun projectile prediction and the matching server side validation path.
    /// </summary>
    public static readonly CVarDef<bool> GunPrediction =
        CVarDef.Create("misfits.gun_prediction", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Rejects authoritative projectile collisions that are implausible against a target's lag compensated history.
    /// </summary>
    public static readonly CVarDef<bool> GunPredictionPreventCollision =
        CVarDef.Create("misfits.gun_prediction_prevent_collision", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Logs whether predicted projectile hit claims were accepted or rejected by the server validator.
    /// </summary>
    public static readonly CVarDef<bool> GunPredictionLogHits =
        CVarDef.Create("misfits.gun_prediction_log_hits", false, CVar.SERVERONLY);

    /// <summary>
    /// How far a client reported target coordinate may deviate from the rewound server coordinate
    /// and still be trusted for projectile hit validation.
    /// </summary>
    public static readonly CVarDef<float> GunPredictionCoordinateDeviation =
        CVarDef.Create("misfits.gun_prediction_coordinate_deviation", 0.75f, CVar.SERVERONLY);

    /// <summary>
    /// Alternate lower bound deviation used when the validator falls back to the oldest plausible
    /// rewound target coordinate inside the ping window.
    /// </summary>
    public static readonly CVarDef<float> GunPredictionLowestCoordinateDeviation =
        CVarDef.Create("misfits.gun_prediction_lowest_coordinate_deviation", 0.5f, CVar.SERVERONLY);

    /// <summary>
    /// Extra AABB inflation applied when validating lag compensated projectile and hitscan collisions.
    /// </summary>
    public static readonly CVarDef<float> GunPredictionAabbEnlargement =
        CVarDef.Create("misfits.gun_prediction_aabb_enlargement", 0.3f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Extra search padding for server side hitscan rewind validation so nearby lag compensated targets
    /// are still considered even when the current state lookup box is slightly stale.
    /// </summary>
    public static readonly CVarDef<float> GunPredictionHitscanSearchPadding =
        CVarDef.Create("misfits.gun_prediction_hitscan_search_padding", 1.5f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// How often (seconds) the proximity NPC system scans for nearby players.
    /// Higher values are cheaper but increase the delay before an NPC wakes.
    /// </summary>
    public static readonly CVarDef<float> ProximityNPCCheckInterval =
        CVarDef.Create("misfits.proximity_npc_check_interval", 5f, CVar.SERVER | CVar.SERVERONLY);

    /// <summary>
    /// Whether the atmos tile simulation runs on grids. When false, the 9-phase
    /// processing loop (tile equalization, active tiles, hotspots, pipe nets,
    /// atmos devices) is skipped entirely. Breathing, temperature, and smoke
    /// continue working via the static <c>MapAtmosphereComponent</c> mixture.
    /// Reclaims ~2-3ms of tick budget on maps with no functional HVAC.
    /// </summary>
    public static readonly CVarDef<bool> AtmosSimulated =
        CVarDef.Create("misfits.atmos_simulated", false, CVar.SERVERONLY);

    /// <summary>
    /// Whether barotrauma (pressure damage) is applied to entities.
    /// When false, <c>BarotraumaSystem</c> skips all processing — no low or high
    /// pressure HP damage is ever dealt. Safe to disable on maps that use a static
    /// <c>MapAtmosphereComponent</c> rather than live atmos simulation, where tile
    /// pressure data may be stale or absent.
    /// </summary>
    public static readonly CVarDef<bool> PressureDamage =
        CVarDef.Create("misfits.pressure_damage", false, CVar.SERVERONLY);

    /// <summary>
    /// Whether respiratory suffocation (gasping, asphyxiation damage) is active.
    /// When false, <c>RespiratorSystem.Update()</c> returns early — no oxygen
    /// saturation drain, no gasp popups, and no suffocation damage are ever applied.
    /// Safe to disable alongside <c>AtmosSimulated</c> and <c>PressureDamage</c> on
    /// maps that don't need breathing mechanics.
    /// </summary>
    // #Misfits Add - CVar to disable all respiratory suffocation
    public static readonly CVarDef<bool> Suffocation =
        CVarDef.Create("misfits.suffocation", false, CVar.SERVERONLY);

    /// <summary>
    /// Whether the Misfits disease system processes ticks. When false,
    /// <c>DiseaseSystem.Update()</c> returns immediately — no per-carrier iteration,
    /// no stage progression, no airborne spread. Defaults to false because no
    /// in-game assets currently produce diseases; re-enable if disease content is added.
    /// </summary>
    // #Misfits Add - CVar to disable the disease tick system entirely
    public static readonly CVarDef<bool> DiseaseEnabled =
        CVarDef.Create("misfits.disease.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// How many times per second the HTN planner evaluates NPC plans.
    /// Upstream default is 5 Hz; higher values reduce combat response delay at
    /// the cost of CPU. At 150+ pop on constrained VPS hardware, 5 Hz is the
    /// safer baseline — raise only if NPC reaction time becomes a complaint.
    /// </summary>
    // #Misfits Add - CVar gate for HTN replan rate (was const 7f, reverted to 5f default)
    public static readonly CVarDef<float> HTNReplanRate =
        CVarDef.Create("misfits.htn_replan_rate", 5f, CVar.SERVERONLY);
}
