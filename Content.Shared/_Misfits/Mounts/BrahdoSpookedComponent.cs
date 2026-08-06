using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Mounts;

/// <summary>
/// Tracks spook state on a Brahdo when the rider fires a gun.
/// Spooked Brahdos ignore rider input, wander randomly via NPC AI,
/// and may buck the rider off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BrahdoSpookedComponent : Component
{
    /// <summary>
    /// How long the Brahdo stays spooked after the last gunshot, in seconds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpookDuration = 5f;

    /// <summary>
    /// Chance (0-1) per gunshot to buck the rider off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BuckChance = 0.15f;

    /// <summary>
    /// Tracks remaining spook time. Server-only, not networked.
    /// </summary>
    public float Accumulator;
}
