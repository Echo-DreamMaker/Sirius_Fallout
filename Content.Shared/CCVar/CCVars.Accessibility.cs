using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Chat window opacity slider, controlling the alpha of the chat window background.
    ///     Goes from to 0 (completely transparent) to 1 (completely opaque)
    /// </summary>
    public static readonly CVarDef<float> ChatWindowOpacity =
        CVarDef.Create("accessibility.chat_window_transparency", 0.85f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Toggle for visual effects that may potentially cause motion sickness.
    ///     Where reasonable, effects affected by this CVar should use an alternate effect.
    ///     Please do not use this CVar as a bandaid for effects that could otherwise be made accessible without issue.
    /// </summary>
    public static readonly CVarDef<bool> ReducedMotion =
        CVarDef.Create("accessibility.reduced_motion", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ChatEnableColorName =
        CVarDef.Create("accessibility.enable_color_name",
            true,
            CVar.CLIENTONLY | CVar.ARCHIVE,
            "Toggles displaying names with individual colors.");

    /// <summary>
    /// Semicolon-separated words or phrases to emphasize in local chat. When empty, the
    /// local character's first and last names are used.
    /// </summary>
    public static readonly CVarDef<string> ChatHighlightTerms =
        CVarDef.Create("accessibility.chat_highlight_terms", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// The color used for locally highlighted chat terms.
    /// </summary>
    public static readonly CVarDef<string> ChatHighlightColor =
        CVarDef.Create("accessibility.chat_highlight_color", "#FFD700", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Screen shake intensity slider, controlling the intensity of the CameraRecoilSystem.
    ///     Goes from 0 (no recoil at all) to 1 (regular amounts of recoil)
    /// </summary>
    public static readonly CVarDef<float> ScreenShakeIntensity =
        CVarDef.Create("accessibility.screen_shake_intensity", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     A generic toggle for various visual effects that are color sensitive.
    ///     As of 2/16/24, only applies to progress bar colors.
    /// </summary>
    public static readonly CVarDef<bool> AccessibilityColorblindFriendly =
        CVarDef.Create("accessibility.colorblind_friendly", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Disables all vision filters for species like Vulpkanin or Harpies. There are good reasons someone might want to disable these.
    /// </summary>
    public static readonly CVarDef<bool> NoVisionFilters =
        CVarDef.Create("accessibility.no_vision_filters", false, CVar.CLIENTONLY | CVar.ARCHIVE); // Corvax-Change

    /// <summary>
    /// If enabled, censors character nudity by forcing undergarment markings on characters for this client.
    /// Both this and <see cref="AccessibilityServerCensorNudity"/> must be false to display nudity.
    /// </summary>
    public static readonly CVarDef<bool> AccessibilityClientCensorNudity =
        CVarDef.Create("accessibility.censor_nudity", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// If enabled, censors character nudity by forcing undergarment markings on characters for all clients.
    /// Both this and <see cref="AccessibilityClientCensorNudity"/> must be false to display nudity.
    /// </summary>
    public static readonly CVarDef<bool> AccessibilityServerCensorNudity =
        CVarDef.Create("accessibility.server_censor_nudity", false, CVar.ARCHIVE | CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<int> SpentCartridgeVisual =
        CVarDef.Create("accessibility.cartridge_visual", 3, CVar.CLIENTONLY | CVar.ARCHIVE);
}
