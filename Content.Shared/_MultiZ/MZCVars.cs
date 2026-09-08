// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._MultiZ;

[CVarDefs]
public sealed partial class MZCVars : CVars
{
    /// <summary>
    /// Master toggle for the entire multi-Z system.
    /// </summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("multi_z.enabled", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Client-side toggle for multi-Z rendering.
    /// </summary>
    public static readonly CVarDef<bool> RenderEnabled =
        CVarDef.Create("multi_z.render_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Ghost the level directly above at low alpha (rooftop awareness) without shifting aim.
    /// </summary>
    public static readonly CVarDef<bool> FaintUpperEnabled =
        CVarDef.Create("multi_z.faint_upper_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Opacity of the faint upper-level ghost (0 = invisible, 1 = solid).
    /// </summary>
    public static readonly CVarDef<float> FaintUpperAlpha =
        CVarDef.Create("multi_z.faint_upper_alpha", 0.14f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum Z-level depth difference to render.
    /// </summary>
    public static readonly CVarDef<int> MaxRenderDepth =
        CVarDef.Create("multi_z.max_render_depth", 8, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Enable blur overlay when looking up/down.
    /// </summary>
    public static readonly CVarDef<bool> BlurEnabled =
        CVarDef.Create("multi_z.blur_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Blur strength multiplier.
    /// </summary>
    public static readonly CVarDef<float> BlurStrength =
        CVarDef.Create("multi_z.blur_strength", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Zoom multiplier used when rendering the lower map from an empty sky/observation layer.
    /// Values above 1 show more ground and make the lower level read as farther away.
    /// </summary>
    public static readonly CVarDef<float> SkyAltitudeZoom =
        CVarDef.Create("multi_z.sky_altitude_zoom", 2f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Enable cross-Z-level audio propagation.
    /// </summary>
    public static readonly CVarDef<bool> CrossZAudio =
        CVarDef.Create("multi_z.cross_z_audio", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Enable visible entity indicators on adjacent Z-levels.
    /// </summary>
    public static readonly CVarDef<bool> VisibleEntityIndicators =
        CVarDef.Create("multi_z.visible_entity_indicators", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Server-side probe update frequency in Hz.
    /// </summary>
    public static readonly CVarDef<float> ProbeUpdateHz =
        CVarDef.Create("multi_z.probe_update_hz", 4.0f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum view probes per player.
    /// </summary>
    public static readonly CVarDef<int> MaxViewProbesPerPlayer =
        CVarDef.Create("multi_z.max_view_probes_per_player", 5, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Cull occluded dynamic sprites on other Z-levels.
    /// </summary>
    public static readonly CVarDef<bool> CullOccludedDynamicSprites =
        CVarDef.Create("multi_z.cull_occluded_dynamic_sprites", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Enable projected lighting from adjacent Z-levels.
    /// </summary>
    public static readonly CVarDef<bool> ProjectedLightingEnabled =
        CVarDef.Create("multi_z.projected_lighting", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
