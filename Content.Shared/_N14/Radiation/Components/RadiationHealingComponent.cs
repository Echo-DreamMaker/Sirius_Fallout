using Robust.Shared.GameStates;

namespace Content.Shared._N14.Radiation.Components;

/// <summary>
/// Allows entities to heal when exposed to radiation and slows them based on exposure.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RadiationHealingComponent : Component
{
    /// <summary>
    /// Amount of damage healed for each rad of radiation consumed.
    /// Lower values mean slower healing.
    /// </summary>
    [DataField("healFactor")] public float HealFactor = 0.1f;

    /// <summary>
    /// Movement speed reduction per rad of exposure.
    /// </summary>
    [DataField("slowFactor")] public float SlowFactor = 0.5f;

    /// <summary>
    /// Rate that radiation exposure decays each second when not irradiated.
    /// </summary>
    [DataField("decayRate")] public float DecayRate = 0.25f;

    /// <summary>
    /// Maximum total damage the entity can have before radiation healing stops.
    /// Defaults to 90 (matches human health scale). Scale up for tougher species.
    /// </summary>
    [DataField("healCap")] public float HealCap = 90f;

    /// <summary>
    /// Amount of damage healed in each healable type per point of incoming Radiation damage.
    ///
    /// Replacement for the previous method of spreading evenly across each damage type.
    /// </summary>
    [DataField("healPerRad")]
    public float HealPerRad = 0.25f;

    /// <summary>
    /// Damage types that radiation can heal. Shared between client and server so the lists can't desync.
    /// </summary>
    [DataField("healableTypes")]
    public string[] HealableTypes = new[] { "Blunt", "Slash", "Piercing", "Heat", "Cold", "Caustic" };

    /// <summary>
    /// Current accumulated radiation intensity in rads per second.
    /// </summary>
    [AutoNetworkedField] public float CurrentExposure;
}
