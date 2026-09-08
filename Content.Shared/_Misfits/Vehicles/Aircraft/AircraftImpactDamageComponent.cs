using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared._Misfits.Vehicles.Aircraft;

/// <summary>
/// Converts hard aircraft collisions into hull damage. Movement and flight
/// state remain owned by the reusable Vertibird flight component.
/// </summary>
[RegisterComponent]
public sealed partial class AircraftImpactDamageComponent : Component
{
    /// <summary>Collision-only integrity displayed in the aircraft console.</summary>
    [DataField]
    public float MaxIntegrity = 100f;

    /// <summary>Closing speed along the collision normal before damage begins.</summary>
    [DataField]
    public float MinimumSpeed = 4f;

    /// <summary>Damage applied at the minimum damaging speed.</summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Additional damage multiplier per unit of closing speed above MinimumSpeed.
    /// </summary>
    [DataField]
    public float SpeedDamageFactor = 0.5f;

    /// <summary>
    /// #Misfits Add - Ported from CMU. Multiplier converting impact speed squared into
    /// Blunt damage when smashing through a destructible obstruction. Zero disables
    /// momentum-based wall smashing and keeps the original impact behaviour.
    /// </summary>
    [DataField]
    public float ObstacleDamageMultiplier = 150f;

    /// <summary>Fraction of tangential drift retained after the impact.</summary>
    [DataField]
    public float VelocityRetention = 0.25f;

    [DataField]
    public float DamageCooldown = 0.65f;

    [DataField]
    public bool GroundLevelOnly = true;

    [DataField]
    public SoundSpecifier? ImpactSound = new SoundCollectionSpecifier("ShuttleImpactSound");

    [DataField]
    public string PilotWarning = "aircraft-impact-warning";

    [DataField]
    public string ExplosionType = "Default";

    [DataField]
    public float ExplosionTotalIntensity = 60f;

    [DataField]
    public float ExplosionSlope = 4f;

    [DataField]
    public float ExplosionMaxTileIntensity = 10f;

    [ViewVariables]
    public TimeSpan LastImpactAt;

    [ViewVariables]
    public bool Destroyed;
}
