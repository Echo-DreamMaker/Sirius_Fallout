// #Misfits Add - Laser aiming module attachment behaviour.
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._Misfits.WeaponAttachments.Components;

/// <summary>
/// A laser aiming module. Toggling it on shows a visible red glow at the muzzle and tightens the weapon's spread.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeaponLaserSightComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public Angle MinAngleBonus = Angle.FromDegrees(-10);

    [DataField]
    public Angle MaxAngleBonus = Angle.FromDegrees(-20);

    [DataField]
    public Angle AngleIncreaseBonus = Angle.FromDegrees(-1);

    /// <summary>
    /// Max visible beam length in tiles (client-side visual only).
    /// </summary>
    [DataField]
    public float MaxLength = 20f;

    /// <summary>
    /// Vertical scale of the beam sprite (thinner = smaller).
    /// </summary>
    [DataField]
    public float BeamWidth = 0.5f;

    /// <summary>
    /// Beam tint; matches the attachment point light colour by default.
    /// </summary>
    [DataField]
    public Color BeamColor = Color.FromHex("#ff1010");

    [DataField]
    public string BeamSprite = "Objects/Weapons/Guns/Projectiles/projectiles.rsi";

    [DataField]
    public string BeamState = "beam";

    /// <summary>
    /// Sprite state shown at the end of the beam while it is stopped by an obstacle
    /// (reuses the laser hit effect).
    /// </summary>
    [DataField]
    public string ImpactState = "impact_laser";

    /// <summary>
    /// How long the impact flash stays after the contact point disappears.
    /// </summary>
    [DataField]
    public float ImpactLifetime = 0.25f;
}
