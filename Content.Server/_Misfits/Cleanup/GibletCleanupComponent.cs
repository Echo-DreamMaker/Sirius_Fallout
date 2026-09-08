using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Cleanup;

/// <summary>
/// Marks a dropped body part or organ (a "giblet") for automatic cleanup by
/// <see cref="MisfitsWorldCleanupSystem"/> after <see cref="Lifetime"/> seconds of lying detached.
/// </summary>
/// <remarks>
/// Cleanup is driven by <see cref="MisfitsWorldCleanupSystem.Update"/> rather than an attach event hook,
/// so a giblet that is attached to a body (e.g. recovered by a surgeon) simply has this component removed
/// and never despawns. // #Cythisiax Fixed - replaced the attach-event hook (owned by SharedBodySystem)
/// with an own-tick accumulator to fix the startup "Duplicate Subscriptions" crash.
/// </remarks>
[RegisterComponent]
public sealed partial class GibletCleanupComponent : Component
{
    /// <summary>Seconds a detached giblet persists before cleanup.</summary>
    public float Lifetime = 600f;

    /// <summary>Elapsed time since this giblet became a cleanup candidate.</summary>
    public float Accumulator;
}
