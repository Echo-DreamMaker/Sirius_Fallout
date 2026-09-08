using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Deathclaw;

/// <summary>
/// Shared player-QoL marker for both ordinary sentient Deathclaws and Bwonsamdi.
/// It must never be used as authorization for Bwonsamdi's supernatural abilities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SentientDeathclawComponent : Component;

/// <summary>
/// Marks the singular sentient Deathclaw role and owns tunable values shared by its abilities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BwonsamdiComponent : Component
{
    [DataField]
    public TimeSpan DeathSenseDebounce = TimeSpan.FromSeconds(3);
}
