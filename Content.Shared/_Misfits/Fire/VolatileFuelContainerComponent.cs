using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Fire;

/// <summary>
/// Entity with a fuel solution that explodes when set on fire or shot.
/// Checks the configured solution for flammable reagents; if any are present,
/// rolls IgniteChance on ignition and ShotChance on projectile hit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VolatileFuelContainerComponent : Component
{
    /// <summary>
    /// Solution to check for flammable fuel. Defaults to first solution found.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? SolutionId;

    /// <summary>
    /// Chance [0-1] to explode when the entity catches fire.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float IgniteChance = 0.8f;

    /// <summary>
    /// Chance [0-1] to explode when hit by a projectile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShotChance = 0.3f;

    /// <summary>
    /// Explosion prototype to use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string ExplosionPrototype = "Default";

    /// <summary>
    /// Total explosion intensity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TotalIntensity = 80f;

    /// <summary>
    /// Explosion dropoff per tile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Dropoff = 20f;

    /// <summary>
    /// Max intensity per tile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxTileIntensity = 20f;

    /// <summary>
    /// Reagent IDs considered flammable fuel.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> FlammableReagents = new() { "WeldingFuel" };
}
