using Robust.Shared.Maths;

namespace Content.Shared._Sirius.Wet;

/// <summary>
/// #Sirius Add: marker component of the "Wet" status effect. Halves ignition and fire damage,
/// changes incoming Heat/Cold/Shock damage, darkens the sprite and allows using soap on the target.
/// </summary>
[RegisterComponent]
public sealed partial class WetComponent : Component
{
    /// <summary>
    /// Original FireStackIncreaseMultiplier, restored when the effect ends.
    /// </summary>
    [ViewVariables]
    public float SavedFireStackIncreaseMultiplier = 1f;

    [ViewVariables]
    public bool SavedFireStackMultiplier;

    // #Sirius Add: sprite colour before the wet tint, restored when the effect ends.
    [ViewVariables]
    public Color SavedSpriteColor = Color.White;

    [ViewVariables]
    public bool SavedSpriteColorSet;
}

/// <summary>
/// #Sirius Add: marker of the washing effect produced by soap or soap water.
/// </summary>
[RegisterComponent]
public sealed partial class SiriusWashedComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float Rate = 2f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float Cap = 35f;
}
